// tools/comfy/claude-workflow.mjs
// -----------------------------------------------------------------------------
// The "Claude" workflow of ComfyUI: the one graph used to make enemy art (docs/plan-comfyui-enemy-workflow.md).
// It lives HERE as data; Comfy's copy (workflows/Claude.json) is written from it, and the same graph is
// what gets queued through the API — see comfy-graph.mjs.
//
//   node claude-workflow.mjs install            write workflows/Claude.json into the running ComfyUI
//   node claude-workflow.mjs run [options]      queue it, wait, print the files it saved
//       --prompt "…"        what to draw (default: the example parts sheet below)
//       --ref a.png,b.png   reference images (files of Comfy's input folder, or "x.png [output]");
//                           the first one is image_1: with --keep-size the result is an EDIT of it
//       --keep-size         canvas = image_1's size (an edit) instead of --size
//       --size 2048x2048    canvas when not editing (Qwen Image 2.1 is native up to 2048)
//       --seed N  --steps N
//       --upscale           ×4 with the upscale model, then down to 16 MP, before cutting
//       --no-cut            skip the background removal
//       --lora file.safetensors [--lora-strength 0.8]   style LoRA (file in Comfy's models/loras)
//       --name prefix       file prefix under output/claude/ (default: "sheet")
//   node claude-workflow.mjs finish --ref image --name prefix [--megapixels 4]
//                           an approved image → upscaled (<prefix>_up) and with its background cut (<prefix>_cut)
//   env COMFY_URL (default http://127.0.0.1:8188). Comfy Desktop must be open.
//
// The chain:  models → [references] → prompt → sample → decode → save raw → [upscale] → cut background → save cut
// One model does everything: Qwen Image 2.1 draws from text AND takes reference images (style, or an
// image to edit), so there is no LoRA / ControlNet / IP-Adapter in it on purpose.

import { toUi, toApi } from './comfy-graph.mjs';

const COMFY = process.env.COMFY_URL || 'http://127.0.0.1:8188';

// The house style, as the user has been prompting it. Kept apart so every request shares it.
export const STYLE = 'Painted digital illustration, stylized semi-realistic, slight texture, soft painted shading, dark fantasy. Clean crisp hard silhouette with a 2px near-black outline all around and a slightly darker contour line where pieces meet. '
  + 'Glow only as a lighter fill inside the outline. Zone palette + glowing crystals: build the full prompt with tools/enemy-prompt.mjs --target comfy.';
export const BACKDROP = 'Pure flat white background, no shadow, no ground, no glow outside the outline, no particles, no text, no labels, nothing else.';

const EXAMPLE = 'A parts sheet for a 2D game enemy, side view facing right: an ice golem taken apart, its body parts laid out separately '
  + 'with wide empty gaps between them, none touching or overlapping: one head, one torso, one upper arm, one forearm with fist, '
  + 'one leg, each drawn complete with a rounded end where it joins the body. Pale blue ice with darker blue facets and a glowing cyan core. '
  + `${STYLE} ${BACKDROP}`;

/** The graph. `o` = what changes between runs; everything else is the recipe. */
export function claudeGraph(o = {}) {
  const refs = o.refs || [];
  const [width, height] = (o.size || '2048x2048').split('x').map(Number);
  const prefix = `claude/${o.name || 'sheet'}`;
  const blue = { fg: '#223', bg: '#335' };

  const refNodes = [0, 1, 2].map((i) => ({
    id: 20 + i, type: 'LoadImage', pos: [40 + i * 330, 760], size: [310, 330],
    title: `Referencia ${i + 1}${i === 0 ? ' (image_1: la que se edita)' : ''}`,
    bypass: !refs[i],
    ...(refs[i] ? { values: { image: refs[i] } } : {}),
  }));
  // Always wired: a reference that is switched off (bypassed) simply gives nothing.
  const refLinks = Object.fromEntries([0, 1, 2].map((i) => [`images.image_${i + 1}`, [20 + i, 0]]));

  return {
    nodes: [
      { id: 1, type: 'MarkdownNote', pos: [-520, 0], size: [500, 1080], title: 'Claude — cómo se usa', text: NOTE },

      // ---- 1 · models
      { id: 10, type: 'UNETLoader', pos: [40, 60], size: [420, 90], values: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors' } },
      { id: 11, type: 'CLIPLoader', pos: [40, 200], size: [420, 120], values: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image' } },
      { id: 12, type: 'VAELoader', pos: [40, 370], size: [420, 70], values: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
      // Optional style LoRA (--lora file [--lora-strength 0.8]): between the model and its cache.
      ...(o.lora ? [{ id: 14, type: 'LoraLoaderModelOnly', pos: [40, 620], size: [420, 100], title: 'LoRA de estilo', values: { lora_name: o.lora, strength_model: o.loraStrength ?? 0.8 }, links: { model: [10, 0] } }] : []),
      { id: 13, type: 'QwenImage21Cache', pos: [40, 490], size: [420, 100], links: { model: o.lora ? [14, 0] : [10, 0] } },

      // ---- 2 · references (off until one is needed)
      ...refNodes,

      // ---- 3 · prompt and canvas
      { id: 30, type: 'TextEncodeQwenImage21', pos: [1080, 60], size: [560, 560], color: blue, title: 'PROMPT',
        values: { prompt: o.prompt || EXAMPLE, negative_prompt: '', resolution: 1024 },
        links: { clip: [11, 0], vae: [12, 0], ...refLinks } },
      { id: 31, type: 'EmptyLatentImage', pos: [1080, 690], size: [270, 120], title: 'Tamaño propio', values: { width, height, batch_size: 1 } },
      { id: 32, type: 'ComfySwitchNode', pos: [1380, 690], size: [260, 100], matchType: 'LATENT',
        title: 'true = tamaño propio · false = el de image_1 (editar)',
        values: { switch: !o.keepSize }, links: { on_false: [30, 2], on_true: [31, 0] } },

      // ---- 4 · generate
      { id: 40, type: 'KSampler', pos: [1720, 60], size: [300, 280], seedControl: 'randomize',
        values: { seed: o.seed ?? 1, steps: o.steps ?? 25, cfg: 1, sampler_name: 'euler', scheduler: 'simple', denoise: 1 },
        links: { model: [13, 0], positive: [30, 0], negative: [30, 1], latent_image: [32, 0] } },
      { id: 41, type: 'VAEDecode', pos: [1720, 400], size: [300, 60], links: { samples: [40, 0], vae: [12, 0] } },
      { id: 42, type: 'SaveImage', pos: [2100, 60], size: [520, 560], title: 'Guardar · tal cual sale', values: { filename_prefix: `${prefix}_raw` }, links: { images: [41, 0] } },
      // Qwen 2.1 decodes to RGBA with an opaque alpha; the upscale model and the cutter want RGB.
      { id: 43, type: 'SplitImageWithAlpha', pos: [1720, 520], size: [300, 60], title: 'Sólo RGB', links: { image: [41, 0] } },

      // ---- 5 · upscale (off by default: 2048 px is already more than the game's parts need)
      { id: 50, type: 'UpscaleModelLoader', pos: [1720, 760], size: [300, 60], bypass: !o.upscale, values: { model_name: 'RealESRGAN_x4plus_anime_6B.pth' } },
      { id: 51, type: 'ImageUpscaleWithModel', pos: [1720, 880], size: [300, 60], bypass: !o.upscale, links: { upscale_model: [50, 0], image: [43, 0] } },
      { id: 52, type: 'ImageScaleToTotalPixels', pos: [1720, 1000], size: [300, 110], bypass: !o.upscale, title: 'Bajar a 16 MP (4096²)',
        values: { upscale_method: 'lanczos', megapixels: 16, resolution_steps: 1 }, links: { image: [51, 0] } },

      // ---- 6 · cut the background
      { id: 60, type: 'RMBG', pos: [2100, 760], size: [340, 360], bypass: !!o.noCut,
        values: { model: 'RMBG-2.0', sensitivity: 1, process_res: 2048, mask_blur: 0, mask_offset: -2, invert_output: false, refine_foreground: false, background: 'Alpha' },
        links: { image: [52, 0] } },
      { id: 61, type: 'SaveImage', pos: [2480, 760], size: [520, 560], bypass: !!o.noCut, title: 'Guardar · recortado (PNG con alfa)', values: { filename_prefix: `${prefix}_cut` }, links: { images: [60, 0] } },
    ],
    groups: [
      { title: '1 · Modelos (Qwen Image 2.1)', bounding: [20, -20, 460, 640], color: '#3f789e' },
      { title: '2 · Referencias — opcional: selecciona una y Ctrl+B para activarla', bounding: [20, 680, 1010, 430], color: '#8a8' },
      { title: '3 · Prompt y tamaño', bounding: [1060, -20, 600, 850], color: '#a1309b' },
      { title: '4 · Generar', bounding: [1700, -20, 940, 660], color: '#b58b2a' },
      { title: '5 · Ampliar ×4 — opcional (Ctrl+B)', bounding: [1700, 680, 340, 450], color: '#88a' },
      { title: '6 · Recortar fondo', bounding: [2080, 680, 940, 660], color: '#b06634' },
    ],
  };
}

/**
 * Finishing an approved image: upscale it, then cut its background. No generation.
 * Saves `<name>_up` (upscaled, still on its background) and `<name>_cut` (with alpha), both `megapixels` big.
 */
export function finishGraph({ image, name, megapixels = 4 }) {
  return {
    nodes: [
      { id: 1, type: "LoadImage", pos: [0, 0], size: [310, 330], values: { image } },
      { id: 2, type: "SplitImageWithAlpha", pos: [0, 0], size: [300, 60], links: { image: [1, 0] } },
      { id: 3, type: "UpscaleModelLoader", pos: [0, 0], size: [300, 60], values: { model_name: "RealESRGAN_x4plus_anime_6B.pth" } },
      { id: 4, type: "ImageUpscaleWithModel", pos: [0, 0], size: [300, 60], links: { upscale_model: [3, 0], image: [2, 0] } },
      { id: 5, type: "ImageScaleToTotalPixels", pos: [0, 0], size: [300, 110], values: { upscale_method: "lanczos", megapixels, resolution_steps: 1 }, links: { image: [4, 0] } },
      { id: 6, type: "SaveImage", pos: [0, 0], size: [300, 300], values: { filename_prefix: `claude/${name}_up` }, links: { images: [5, 0] } },
      { id: 7, type: "RMBG", pos: [0, 0], size: [340, 360],
        values: { model: "RMBG-2.0", sensitivity: 1, process_res: 2048, mask_blur: 0, mask_offset: -2, invert_output: false, refine_foreground: false, background: "Alpha" },
        links: { image: [5, 0] } },
      { id: 8, type: "SaveImage", pos: [0, 0], size: [300, 300], values: { filename_prefix: `claude/${name}_cut` }, links: { images: [7, 0] } },
    ],
  };
}

const NOTE = `## Qué es
El flujo con el que Claude hace el arte de los enemigos. **No lo edites a mano para que quede**: se
reescribe desde \`Web/RedMagicWeb/tools/comfy/claude-workflow.mjs\` (\`node claude-workflow.mjs install\`).
Para probar cosas, cámbialo aquí y dile a Claude lo que funcionó.

## La cadena
modelos → [referencias] → **PROMPT** → generar → guardar tal cual → [ampliar] → recortar fondo → guardar recortado

Salida: \`output/claude/<nombre>_raw_*.png\` y \`<nombre>_cut_*.png\` (PNG con alfa).

## Tres usos, el mismo flujo
1. **Hoja de piezas desde texto**: sin referencias, interruptor en *true*, tamaño 2048×2048.
2. **Con estilo de referencia**: activa una Referencia (selecciónala, Ctrl+B) con una pieza o un enemigo
   que ya esté en el juego, y nómbrala en el prompt como \`<image1>\` ("same art style as <image1>").
   Interruptor en *true*.
3. **Editar / aislar una pieza**: Referencia 1 = la imagen a cambiar, interruptor en *false* (el lienzo
   es el de la imagen), y el prompt es la instrucción ("Keep everything identical, change only…",
   "Only the left arm of <image1>, alone, centred, complete, with a rounded shoulder end").

## Ajustes que ya están puestos
- Qwen Image 2.1: euler / simple, 25 pasos, cfg 1 (el prompt negativo no cuenta con cfg 1).
- Nativo hasta 2048 px: una hoja a 2048 da piezas de 400–900 px, el tamaño de las que ya hay en el
  juego. Por eso **Ampliar está apagado**.
- Ampliar: RealESRGAN x4 anime (el mismo "digital-art" de Upscayl) y bajada a 4096.
- Recorte: RMBG-2.0 a 2048, borde encogido 2 px.

## Lo que NO hay, a propósito
LoRA, ControlNet, IP-Adapter: las referencias de Qwen 2.1 hacen ese trabajo. Si la consistencia de
estilo no llega, el siguiente paso es un LoRA entrenado con las piezas del juego, no más nodos.`;

// ============================================================ CLI

async function api(path, init) {
  const res = await fetch(`${COMFY}${path}`, init).catch(() => { throw new Error(`ComfyUI does not answer at ${COMFY}: open Comfy Desktop`); });
  if (!res.ok) throw new Error(`${path} → ${res.status} ${await res.text()}`);
  return res;
}

function options(argv) {
  const o = {};
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = () => argv[++i];
    if (flag === '--prompt') o.prompt = value();
    else if (flag === '--ref') o.refs = value().split(',').map((s) => s.trim()).filter(Boolean);
    else if (flag === '--keep-size') o.keepSize = true;
    else if (flag === '--size') o.size = value();
    else if (flag === '--seed') o.seed = Number(value());
    else if (flag === '--steps') o.steps = Number(value());
    else if (flag === '--upscale') o.upscale = true;
    else if (flag === '--no-cut') o.noCut = true;
    else if (flag === '--name') o.name = value();
    else if (flag === '--lora') o.lora = value();
    else if (flag === '--lora-strength') o.loraStrength = Number(value());
    else if (flag === '--megapixels') o.megapixels = Number(value());
    else throw new Error(`unknown option ${flag}`);
  }
  return o;
}

async function main() {
  const [command, ...rest] = process.argv.slice(2);
  const defs = await (await api('/object_info')).json();

  if (command === 'install') {
    const ui = toUi(claudeGraph(), defs);
    await api('/userdata/workflows%2FClaude.json?overwrite=true', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(ui) });
    console.log(`✓ workflows/Claude.json written (${ui.nodes.length} nodes, ${ui.links.length} links). Reopen it in Comfy to see it.`);
    return;
  }

  if (command === 'run' || command === 'finish') {
    const o = options(rest);
    if (o.seed === undefined) o.seed = Math.floor(Math.random() * 2 ** 48);
    const prompt = toApi(command === 'finish' ? finishGraph({ image: o.refs[0], name: o.name, megapixels: o.megapixels }) : claudeGraph(o), defs);
    const started = Date.now();
    const { prompt_id: id } = await (await api('/prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt, client_id: 'claude-workflow' }) })).json();
    console.log(`queued ${id} (seed ${o.seed})`);
    for (;;) {
      await new Promise((r) => setTimeout(r, 2000));
      const entry = (await (await api(`/history/${id}`)).json())[id];
      if (!entry) continue;
      if (entry.status?.status_str === 'error') {
        const failure = entry.status.messages?.find((m) => m[0] === 'execution_error')?.[1];
        throw new Error(`run failed in ${failure?.node_type}: ${failure?.exception_message}`);
      }
      if (!entry.status?.completed) continue;
      const files = Object.values(entry.outputs).flatMap((out) => (out.images || []).map((f) => `${f.subfolder}/${f.filename}`));
      console.log(`✓ ${((Date.now() - started) / 1000).toFixed(0)} s → output/${files.join('\n  output/')}`);
      return;
    }
  }

  console.log('usage: node claude-workflow.mjs install | run [--prompt … --ref … --keep-size --size WxH --seed N --steps N --upscale --no-cut --name prefix]');
}

if (process.argv[1]?.endsWith('claude-workflow.mjs')) {
  main().catch((err) => { console.error(`✗ ${err.message}`); process.exit(1); });
}
