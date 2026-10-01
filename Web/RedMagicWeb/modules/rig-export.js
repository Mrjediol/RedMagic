// modules/rig-export.js
// -----------------------------------------------------------------------------
// Rig composer state → `rig-config.json` (docs/schemas/rig-config.schema.json) and the .zip that
// Unity's Tools ▸ Web ▸ Rig ▸ Importar rig reads (RigBundleImporter.cs):
//
//   rig-config.json
//   Parts/<pieza>.png, Parts/<pieza>__asleep.png, Parts/<pieza>__stage1.png …   (cropped art)
//   enemy-config.json            (kind enemy, optional: the behaviour Enemy Creator saved on this rig)
//   Library/<libraryId>/…        (its projectile's art, same as a combined enemy bundle)
//   boss-config.json + Art/…     (kind boss, optional: a linked Boss Creator entry, without 'body')
//
// Decisions that mirror the importer (see docs/web-tools-guide.md ▸ Rig):
//  - each part's joint IS its sprite's pivot (written in `sprites`), so RigPart.pivot exports as 0;
//  - muzzle and grips become ordinary nodes ('Muzzle' under the aim part, 'Grip' under each limb)
//    with rigPart:false;
//  - `gestures` is only written when the document has them (today: the ones a body template brings,
//    rig-templates.js). A present list replaces the gestures authored in Unity, an absent key keeps them;
//  - a gesture's effects (rig-fx.js) go out as `gestures[].phases[].fx`, each pointing at an entry of
//    the document's own `artAssets` (one per library effect and per loop / one-shot); their frames
//    travel in Art/<libraryId>/, next to the deck's art. `phases` is always written with the gestures
//    (empty = no effects), so removing one in the web removes it in Unity;
//  - a template slot still empty is left out (unless a filled part hangs from it: then it stays as
//    an empty node), and a required one is reported as a problem.

import { byUid, pathOf, pixelToLocal, normalizedPivot, variantPivot, isOpenSlot, feetY } from './rig-model.js';
import { embedReferencedLibraryEntries, embedAttackArt } from './enemy-bundle.js';
import { writeLanes } from './boss-bundle.js';
import { FX_PHASES, fxArtId, fxArtAsset } from './rig-fx.js';

const r5 = (v) => Math.round(v * 1e5) / 1e5;
const v2 = (v) => ({ x: r5(v.x), y: r5(v.y) });
const scaleOut = (s) => (Math.abs(s.x - s.y) < 1e-6 ? r5(s.x) : v2(s));
const safe = (name) => name.replace(/[^A-Za-z0-9_-]/g, '_') || 'pieza';

/**
 * @param {object} state
 * @param {{entriesById?: Map<string, object>}} [ctx] - library records: needed to export the effects
 *        hooked to gestures (an effect whose entry is not there is reported and left out).
 * @returns {{ doc: object, files: Array<{path: string, dataURL: string}>, problems: string[], notes: string[] }}
 */
export function buildRigExport(state, ctx = {}) {
  const problems = [];
  const notes = [];
  const files = [];
  const sprites = {};
  const ppu = state.pixelsPerUnit;
  const roles = state.roles;
  const used = new Set();

  if (!state.name.trim()) problems.push('Ponle nombre (será el del prefab).');
  if (state.parts.length === 0) problems.push('Añade al menos una pieza.');

  // Template slots without art: out of the export, unless something filled hangs from them.
  const gestures = state.gestures;
  const hasContent = (part) => !isOpenSlot(part) || state.parts.some((c) => c.parent === part.uid && hasContent(c));
  const emptyOptional = [];
  state.parts.filter(isOpenSlot).forEach((part) => {
    if (!part.slot.optional) problems.push(`Hueco de la plantilla sin rellenar: "${part.slot.label}".`);
    else if (hasContent(part)) notes.push(`"${part.slot.label}" está vacío pero otras piezas cuelgan de él: se exporta como pivote vacío.`);
    else emptyOptional.push(part.slot.label);
  });
  if (emptyOptional.length) notes.push(`Huecos opcionales vacíos (no se exportan): ${emptyOptional.join(', ')}.`);
  state = { ...state, parts: state.parts.filter(hasContent) };

  function addFile(stem, image, pivot) {
    let path = `Parts/${stem}.png`;
    for (let n = 2; used.has(path); n++) path = `Parts/${stem}_${n}.png`;
    used.add(path);
    files.push({ path, dataURL: image.dataURL });
    sprites[path] = { pixelsPerUnit: ppu, pivot: v2(normalizedPivot(image, pivot)) };
    return path;
  }

  const partPaths = new Map(state.parts.map((p) => [p.uid, pathOf(state, p)]));
  const nodes = [];
  const swaps = [];

  state.parts.forEach((part, index) => {
    const node = { name: part.name, parent: part.parent ? partPaths.get(part.parent) : '' };
    if (part.image) {
      node.sprite = addFile(safe(part.name), part.image, part.pivot);
      node.sortingOrder = index + 1;   // 0 is behind the scene's background: the back-most part is 1
    }
    node.position = v2(part.position);
    node.rotation = r5(part.rotation);
    node.scale = scaleOut(part.scale);
    node.rigPart = part.rigPart;
    if (part.rigPart) {
      node.pivot = { x: 0, y: 0 };
      node.center = part.center ? v2(pixelToLocal(state, part, part.center)) : { x: 0, y: 0 };
      node.pointAngle = r5(part.pointAngle);
      node.leanFollow = r5(part.leanFollow);
      node.sway = {
        move: v2(part.sway.move), rotate: r5(part.sway.rotate), scale: v2(part.sway.scale),
        speed: r5(part.sway.speed), phase: r5(part.sway.phase),
      };
      node.breaksOnDeath = part.breaksOnDeath;
    }
    nodes.push(node);

    const v = part.variants || {};
    // Named states: alert / hurt / dead, and one per gesture that still exists.
    const gestureNames = new Set((Array.isArray(gestures) ? gestures : []).map((g) => g.name));
    const named = [
      ...['alert', 'hurt', 'dead'].filter((k) => v.named?.[k]).map((k) => ({ state: k, image: v.named[k], suffix: k })),
      ...Object.entries(v.gestures || {}).filter(([name, img]) => img && gestureNames.has(name))
        .map(([name, img]) => ({ state: 'gesture', gesture: name, image: img, suffix: `gesture_${safe(name)}`, window: v.gestureWindows?.[name] || null })),
    ];
    const orphan = Object.keys(v.gestures || {}).filter((name) => v.gestures[name] && !gestureNames.has(name));
    if (orphan.length) notes.push(`"${part.name}": tiene sprite para gestos que ya no existen (${orphan.join(', ')}); no se exportan.`);
    const hasStates = v.asleep || v.awake || v.attacking || named.length;
    const stages = (v.stages || []).filter(Boolean);
    if ((hasStates || stages.length) && !part.image) problems.push(`"${part.name}": los sprites por estado necesitan una imagen base.`);
    if ((hasStates || stages.length) && part.image) {
      const ref = (img, suffix) => (img ? addFile(`${safe(part.name)}__${suffix}`, img, variantPivot(part, img)) : null);
      const swap = { part: partPaths.get(part.uid), asleep: ref(v.asleep, 'asleep'), awake: ref(v.awake, 'awake'), attacking: ref(v.attacking, 'attacking'), stages: [] };
      // Stage 0 = the base sprite (the torso intact); variants.stages[i] = stage i+1 (phase i+2).
      if (stages.length) swap.stages = [node.sprite, ...v.stages.map((s, i) => (s ? ref(s, `stage${i + 1}`) : node.sprite))];
      if (named.length) swap.named = named.map((n) => ({ state: n.state, ...(n.gesture ? { gesture: n.gesture } : {}),
        ...(n.window ? { from: r5(n.window.from), to: r5(n.window.to) } : {}), sprite: ref(n.image, n.suffix) }));
      swaps.push(swap);
      [v.asleep, v.awake, v.attacking, ...(v.stages || []), ...named.map((n) => n.image)].forEach((img) => {
        if (img && (img.origW !== part.image.origW || img.origH !== part.image.origH))
          notes.push(`"${part.name}": un sprite de estado no tiene el tamaño de archivo de la pieza; se alinea por su centro.`);
      });
    }
  });

  // --- helper nodes: the muzzle under the aim part, a grip under each limb.
  const aim = byUid(state, roles.aim);
  let muzzlePath = null;
  if (aim && roles.muzzle) {
    const local = pixelToLocal(state, aim, roles.muzzle);
    nodes.push({ name: 'Muzzle', parent: partPaths.get(aim.uid), position: v2(local), rotation: 0, scale: 1, rigPart: false });
    muzzlePath = `${partPaths.get(aim.uid)}/Muzzle`;
  } else if (aim) notes.push('Hay pieza que apunta pero sin boca: el disparo saldrá del centro del enemigo.');

  const gripPaths = [];
  roles.limbs.forEach((uid) => {
    const limb = byUid(state, uid);
    const pt = roles.grips?.[uid];
    if (!limb || !pt) return;
    nodes.push({ name: 'Grip', parent: partPaths.get(uid), position: v2(pixelToLocal(state, limb, pt)), rotation: 0, scale: 1, rigPart: false });
    gripPaths.push(`${partPaths.get(uid)}/Grip`);
  });

  // --- rig block: scalar fields + the roles (Transform fields by path).
  const rig = {};
  Object.entries(state.rig).forEach(([k, v]) => {
    rig[k] = typeof v === 'number' ? r5(v) : v && typeof v === 'object' && !Array.isArray(v) ? v2(v) : v;
  });
  rig.wakeFlickers = Math.max(0, Math.round(state.rig.wakeFlickers || 0));
  rig.aimPart = aim ? partPaths.get(aim.uid) : null;
  rig.muzzle = muzzlePath;
  const stretch = byUid(state, roles.stretch);
  rig.stretchPart = stretch ? partPaths.get(stretch.uid) : null;
  rig.stretchRiders = roles.riders.filter((uid) => byUid(state, uid)).map((uid) => partPaths.get(uid));
  if (stretch) roles.riders.forEach((uid) => {
    const rider = byUid(state, uid);
    if (rider && rider.parent !== stretch.parent) problems.push(`"${rider.name}" (pasajero) tiene que colgar del mismo padre que "${stretch.name}" (la que se estira).`);
  });
  if (!stretch && roles.riders.length) notes.push('Hay pasajeros pero ninguna pieza que se estire: no se pliega.');

  const doc = {
    kind: state.kind,
    driver: state.driver,
    name: state.name.trim(),
    pixelsPerUnit: ppu,
    sprites,
    // The root is the pivot = the centre of the body; a boss says where its feet are (BossController.feet).
    root: state.kind === 'boss' ? { scale: r5(state.rootScale), feet: { x: 0, y: r5(feetY(state)) } } : { scale: r5(state.rootScale) },
    collider: state.collider.type === 'circle'
      ? { type: 'circle', radius: r5(state.collider.radius || 0.5), offset: v2(state.collider.offset) }
      : { type: state.collider.type, size: v2(state.collider.size), offset: v2(state.collider.offset) },
    corpse: { linger: r5(state.corpse.linger), fadeDuration: r5(state.corpse.fadeDuration) },
    visual: { position: { x: 0, y: 0 }, rotation: 0, scale: 1 },
    parts: nodes,
    rig,
    spriteSwaps: swaps,
  };

  if (Array.isArray(gestures)) {
    const poses = (list) => (list || [])
      .map((p) => ({ part: p.part === 'Visual' ? 'Visual' : partPaths.get(p.part), move: v2(p.move), rotate: r5(p.rotate), scale: v2(p.scale) }))
      .filter((p) => p.part);   // poses of a part that is not there (empty optional slot, deleted) drop out
    const artAssets = {};
    const fxOut = (g, f) => {
      const entry = ctx.entriesById?.get(f.fx);
      const asset = entry ? fxArtAsset(entry, f.loop, ppu) : null;
      if (!asset) { problems.push(`Gesto "${g.name}": un efecto ya no está en la biblioteca (o no tiene frames); quítalo o elige otro.`); return null; }
      const id = fxArtId(entry, f.loop);
      artAssets[id] = asset;
      const out = { fx: { art: id }, at: r5(Math.min(1, Math.max(0, f.at || 0))), anchor: f.anchor };
      if (f.anchor === 'part') {
        out.part = partPaths.get(f.part) || null;
        if (!out.part) { out.anchor = 'body'; delete out.part; notes.push(`Gesto "${g.name}": la pieza de un efecto no se exporta; el efecto cuelga del cuerpo.`); }
      }
      if (f.anchor === 'muzzle' && !muzzlePath) notes.push(`Gesto "${g.name}": un efecto cuelga de la boca pero no hay boca puesta; saldrá de la pieza que apunta o del cuerpo.`);
      out.offset = v2(f.offset || { x: 0, y: 0 });
      out.scale = r5(f.scale > 0 ? f.scale : 1);
      out.followsPart = f.followsPart !== false;
      out.loop = !!f.loop;
      return out;
    };
    doc.gestures = gestures.map((g) => ({
      name: g.name, windup: poses(g.windup), strike: poses(g.strike),
      strikeTime: r5(g.strikeTime), recover: r5(g.recover), shake: r5(g.shake),
      phases: FX_PHASES
        .map((phase) => ({ phase, fx: (g.fx || []).filter((f) => f.phase === phase).map((f) => fxOut(g, f)).filter(Boolean) }))
        .filter((p) => p.fx.length),
    }));
    if (Object.keys(artAssets).length) doc.artAssets = artAssets;

    // Idle gestures travel with the gestures they name (a name that is gone drops out).
    const idle = state.idle || {};
    doc.idleGestures = {
      gestures: (idle.gestures || []).filter((name) => gestures.some((g) => g.name === name)),
      interval: r5(Math.max(0.2, idle.interval || 4)),
      chance: r5(Math.min(1, Math.max(0, idle.chance ?? 0.6))),
      windup: r5(Math.max(0.05, idle.windup || 0.5)),
    };
    if (doc.gestures.length) notes.push(`Gestos (${doc.gestures.map((g) => g.name).join(', ')}): al reimportar sustituyen a los del prefab.`);
    if (rig.moveGesture && !doc.gestures.some((g) => g.name === rig.moveGesture))
      notes.push(`El gesto al desplazarse "${rig.moveGesture}" no está entre los gestos exportados.`);
  }

  if (state.kind === 'boss') {
    doc.bossLimbs = {
      limbs: roles.limbs.filter((uid) => byUid(state, uid)).map((uid) => partPaths.get(uid)),
      orbiters: roles.orbiters.filter((uid) => byUid(state, uid)).map((uid) => partPaths.get(uid)),
      orbiterRadius: r5(state.bossLimbs.orbiterRadius),
      grips: gripPaths,
      contactRadius: r5(state.bossLimbs.contactRadius),
    };
    const body = { ...state.bossBody };
    const bodyPart = byUid(state, body.bodyRenderer);
    delete body.bodyRenderer;
    // An existing Unity BossDefinition (asset path); the linked Boss Creator entry wins over it.
    body.definition = (body.definition || '').trim();
    if (!body.definition) delete body.definition;
    if (!body.definition && !state.bossConfigId)
      problems.push('Jefe sin ataques: enlaza un jefe de 👑 Boss Creator o escribe la ruta de una BossDefinition existente (inspector ▸ Ataques y fases). Sin ella, en Unity se queda quieto y sin barra de vida.');
    if (bodyPart?.image) body.bodyRenderer = partPaths.get(bodyPart.uid);
    else problems.push('Jefe: elige la pieza "cuerpo" (la que copia el aura de aviso).');
    doc.bossBody = body;
    if (gripPaths.length < roles.limbs.length) notes.push('Alguna extremidad no tiene agarre: los objetos se cogerán por su centro.');
  } else if (roles.limbs.length || roles.orbiters.length) {
    notes.push('Extremidades y orbitales sólo cuentan en un jefe (kind boss); en un enemigo se ignoran.');
  }

  return { doc, files, problems, notes };
}

/** The Enemy Creator config authored for this rig, trimmed to what a part-based enemy reads (tuning + tier). */
export function companionEnemyConfig(enemyConfig, name) {
  if (!enemyConfig) return null;
  const cfg = JSON.parse(JSON.stringify(enemyConfig));
  ['art', 'anchor', 'presence', 'projectileArt', 'prefabFolder'].forEach((k) => delete cfg[k]);
  cfg.kind = 'enemy';
  cfg.enemyName = name;
  return cfg;
}

/**
 * The .zip for Unity.
 * @param {object} state
 * @param {object} links - { enemyConfig: EnemyConfig from Enemy Creator (the rig record's `enemyConfig`),
 *                          bossEntry, entriesById: Map } (library records)
 */
export async function buildRigBundle(state, { enemyConfig = null, bossEntry = null, entriesById = new Map() } = {}) {
  const { doc, files } = buildRigExport(state, { entriesById });
  const zip = new window.JSZip();
  zip.file('rig-config.json', JSON.stringify(doc, null, 2));
  files.forEach((f) => zip.file(f.path, f.dataURL.split(',')[1], { base64: true }));
  await embedAttackArt(zip, doc);   // frames of the gestures' effects → Art/<libraryId>/

  if (state.kind === 'enemy') {
    const cfg = companionEnemyConfig(enemyConfig, doc.name);
    if (cfg) {
      zip.file('enemy-config.json', JSON.stringify(cfg, null, 2));
      await embedReferencedLibraryEntries(zip, cfg);
      await embedAttackArt(zip, cfg);   // art of the deck's attacks → Art/<libraryId>/
    }
  } else if (bossEntry?.config) {
    const cfg = JSON.parse(JSON.stringify(bossEntry.config));
    delete cfg.body;   // the rig IS the body
    zip.file('boss-config.json', JSON.stringify(cfg, null, 2));
    const written = new Set();
    for (const asset of Object.values(cfg.artAssets || {})) {
      if (written.has(asset.libraryId)) continue;
      written.add(asset.libraryId);
      const entry = entriesById.get(asset.libraryId);
      if (entry?.sprite) await writeLanes(zip.folder('Art').folder(asset.libraryId), entry);
    }
  }

  return zip.generateAsync({ type: 'blob' });
}
