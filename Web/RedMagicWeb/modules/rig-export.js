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
// A second, PARTIAL bundle (`buildGesturesBundle`, the "Gestos y ataques" tab): only what that tab
// edits, for an enemy Unity already has — rig-config.json with `"partial": true` and just `gestures` /
// `idleGestures` / `spriteSwaps` (+ their PNGs and effects), and an enemy-config.json with just the
// deck (`attacks`, `tuning.attacks`, `tuning.defences`). RigBundleImporter.ImportPartial applies it
// over the existing prefab and touches nothing else (never the body, never the Inspector's tuning).
//
// Decisions that mirror the importer (see docs/web-tools-guide.md ▸ Rig):
//  - each part's joint IS its sprite's pivot (written in `sprites`), so RigPart.pivot exports as 0;
//  - muzzle and grips become ordinary nodes ('Muzzle' under the aim part, 'Grip' under each limb)
//    with rigPart:false;
//  - `gestures` is only written when the document has them. A present list replaces the gestures
//    authored in Unity, an absent key keeps them. Each one is a TIMELINE — `{ name, steps[],
//    releases[], recover, fx[] }`, the shape RigConfigExporter writes — and `readRigGestures` reads
//    that shape (and the classic windup / strike one) back into a document;
//  - a gesture's effects (rig-fx.js) go out as `gestures[].fx`, each pointing at an entry of the
//    document's own `artAssets` (one per library effect and per loop / one-shot); their frames
//    travel in Art/<libraryId>/, next to the deck's art. `fx` is always written with the gesture
//    (empty = no effects), so removing one in the web removes it in Unity;
//  - a template slot still empty is left out (unless a filled part hangs from it: then it stays as
//    an empty node), and a required one is reported as a problem.

import { byUid, pathOf, pixelToLocal, normalizedPivot, variantPivot, isOpenSlot, feetY, setPivot, worldMatrix, multiply, invert, apply, loadImage } from './rig-model.js';
import { createDefaultRigState, createPart } from './rig-defaults.js';
import { embedReferencedLibraryEntries, embedAttackArt } from './enemy-bundle.js';
import { writeLanes } from './boss-bundle.js';
import { fxArtId, fxArtAsset, createFx } from './rig-fx.js';
import { normalizeGesture, createRelease, EASES } from './rig-gestures.js';
import { isLibraryClip, libraryClipId, soundFileName } from './sound-library.js';

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
  // A body read back from Unity is only good for gestures and attacks: it lost its original canvases.
  if (state.unityBody) problems.push('Este cuerpo se leyó de Unity para editar sus gestos y ataques: no se exporta entero (el prefab ya tiene su cuerpo). Usa "Guardar en Unity" o el export de sólo gestos y ataques.');
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
    // Read from Unity (readRigDocument): the sprite is already in the project — its reference, no PNG.
    if (image.unityRef) return image.unityRef;
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
      let ref = f.prefab || null;   // a prefab Unity already has (read back from its export): as it came
      if (!ref) {
        const entry = ctx.entriesById?.get(f.fx);
        const asset = entry ? fxArtAsset(entry, f.loop, ppu) : null;
        if (!asset) { problems.push(`Gesto "${g.name}": un efecto ya no está en la biblioteca (o no tiene frames); quítalo o elige otro.`); return null; }
        const id = fxArtId(entry, f.loop);
        artAssets[id] = asset;
        ref = { art: id };
      }
      const count = g.steps.length;
      const step = Math.min(count, Math.max(0, Math.round(f.step || 0)));
      const out = { fx: ref, step, at: r5(Math.min(1, Math.max(0, f.at || 0))), anchor: f.anchor };
      if (f.anchor === 'part') {
        out.part = partPaths.get(f.part) || null;
        if (!out.part) { out.anchor = 'body'; delete out.part; notes.push(`Gesto "${g.name}": la pieza de un efecto no se exporta; el efecto cuelga del cuerpo.`); }
      }
      if (f.anchor === 'muzzle' && !muzzlePath) notes.push(`Gesto "${g.name}": un efecto cuelga de la boca pero no hay boca puesta; saldrá de la pieza que apunta o del cuerpo.`);
      out.offset = v2(f.offset || { x: 0, y: 0 });
      out.scale = r5(f.scale > 0 ? f.scale : 1);
      out.followsPart = f.followsPart !== false;
      out.loop = !!f.loop;
      out.untilStep = f.loop && f.untilStep >= 0 ? Math.min(count, Math.round(f.untilStep)) : -1;
      out.facing = f.facing || 'direction';
      out.angle = r5(f.angle || 0);
      out.layer = f.layer || 'front';
      return out;
    };
    const releaseOut = (g, r) => {
      const out = { step: Math.min(g.steps.length - 1, Math.max(0, Math.round(r.step || 0))), at: r5(Math.min(1, Math.max(0, r.at ?? 1))) };
      let offset = r.offset || { x: 0, y: 0 };
      if (r.point) {
        const path = partPaths.get(r.point);
        if (path) out.point = path;
        else {   // its offset was measured on that part: it means nothing on the muzzle
          offset = { x: 0, y: 0 };
          notes.push(`Gesto "${g.name}": la pieza de la que salía una suelta no se exporta; sale de la boca.`);
        }
      }
      out.offset = v2(offset);
      return out;
    };
    // A clip of the library (`lib:<id>`) travels in the bundle as Audio/<file>; one of the project, by its path.
    const sounds = ctx.soundsById || ctx.entriesById;
    const clipOut = (g, clip) => {
      if (!isLibraryClip(clip)) return clip;
      const record = sounds?.get(libraryClipId(clip));
      if (!record?.draft?.dataURL) { problems.push(`Gesto "${g.name}": un sonido ya no está en la biblioteca; elige otro o quítalo.`); return null; }
      const path = `Audio/${soundFileName(record)}`;
      if (!files.some((f) => f.path === path)) files.push({ path, dataURL: record.draft.dataURL });
      return path;
    };
    doc.gestures = gestures.map((g) => ({
      name: g.name,
      steps: g.steps.map((s) => ({ name: s.name || '', duration: r5(Math.max(0.01, s.duration || 0.3)), ease: EASES.includes(s.ease) ? s.ease : 'inout',
        stretch: !!s.stretch, shake: r5(s.shake || 0), aim: !!s.aim, poses: poses(s.poses) })),
      releases: (g.releases || []).map((r) => releaseOut(g, r)),
      recover: r5(Math.max(0.01, g.recover)),
      fx: (g.fx || []).map((f) => fxOut(g, f)).filter(Boolean),
      // Its own sound: only when the document knows it (read from Unity, or set here). null = it has none.
      ...(g.sound === undefined ? {} : { sound: g.sound && g.sound.clips.length ? {
        clips: g.sound.clips.map((c) => clipOut(g, c)).filter(Boolean), volume: r5(g.sound.volume ?? 1), pitchMin: r5(g.sound.pitchMin ?? 1), pitchMax: r5(Math.max(g.sound.pitchMin ?? 1, g.sound.pitchMax ?? 1)),
        step: Math.min(g.steps.length - 1, Math.max(0, Math.round(g.sound.step || 0))), at: r5(Math.min(1, Math.max(0, g.sound.at || 0))),
      } : null }),
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
    // …and so do the transition ones (between attacks).
    const transition = state.transition || {};
    doc.transitionGestures = {
      gestures: (transition.gestures || []).filter((name) => gestures.some((g) => g.name === name)),
      chance: r5(Math.min(1, Math.max(0, transition.chance ?? 0.5))),
      windup: r5(Math.max(0.05, transition.windup || 0.3)),
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

// ============================================================ rig-config → document (gestures)

/**
 * The gestures of a rig-config — the one Unity exports (RigConfigExporter) or one written here —
 * as document gestures over the body in `state`: parts found by their path under Visual. Both JSON
 * shapes are read: the timeline (`steps` / `releases` / `fx`) and the classic one (`windup` /
 * `strike` / `phases`), converted like the importer converts it.
 *
 * An effect comes back as a library hook when its `artAssets` entry names a library effect that is
 * in `ctx.entriesById`; otherwise it keeps the prefab path Unity gave (`prefab`) and is exported as
 * it came — with a preview when `ctx.artLibrary` (prefab path → library id) knows which entry it was
 * built from. `ctx.muzzlePath` = the rig's muzzle node when it is not "<aim part>/Muzzle".
 *
 * @returns {{ gestures: object[], idle: object | null, notes: string[] }}
 */
export function readRigGestures(doc, state, ctx = {}) {
  const notes = [];
  const uidOf = new Map(state.parts.map((p) => [pathOf(state, p), p.uid]));
  const aim = byUid(state, state.roles.aim);
  const muzzlePath = ctx.muzzlePath !== undefined ? ctx.muzzlePath : aim ? `${pathOf(state, aim)}/Muzzle` : null;
  const vec = (v) => ({ x: Number(v?.x) || 0, y: Number(v?.y) || 0 });
  const missing = new Set();
  // A helper node (Muzzle, Grip) is not a part of the document: what hung from it hangs from its part.
  const partOf = (path) => {
    for (let p = path; p; p = p.includes('/') ? p.slice(0, p.lastIndexOf('/')) : '') if (uidOf.has(p)) return uidOf.get(p);
    return null;
  };

  const poses = (list) => (Array.isArray(list) ? list : []).map((p) => {
    const part = p.part === 'Visual' ? 'Visual' : uidOf.get(p.part);
    if (!part) { missing.add(p.part); return null; }
    return { part, move: vec(p.move), rotate: Number(p.rotate) || 0, scale: vec(p.scale) };
  }).filter(Boolean);

  const hook = (item, gestureName) => {
    const artId = item.fx && typeof item.fx === 'object' ? item.fx.art : null;
    const asset = artId ? doc.artAssets?.[artId] : null;
    const prefab = typeof item.fx === 'string' ? item.fx : null;
    // A prefab of the project whose library entry is known (the bridge's artLibrary): that entry is its
    // preview, and the prefab path stays — it is exported as it came.
    const previewId = prefab ? ctx.artLibrary?.[prefab] : null;
    const libraryId = asset?.libraryId && ctx.entriesById?.has(asset.libraryId) ? asset.libraryId
      : previewId && ctx.entriesById?.has(previewId) ? previewId : null;
    if (!libraryId && !prefab) { notes.push(`Gesto "${gestureName}": un efecto no está en la biblioteca ni trae prefab; se omite.`); return null; }
    const f = createFx(libraryId, 0, Math.min(1, Math.max(0, Number(item.at) || 0)), item.anchor || 'body');
    if (prefab) f.prefab = prefab;
    if (f.anchor === 'part') {
      f.part = partOf(item.part || '');
      if (!f.part) { f.anchor = 'body'; missing.add(item.part); }
    }
    f.offset = vec(item.offset);
    f.scale = item.scale > 0 ? item.scale : 1;
    f.followsPart = item.followsPart !== false;
    f.loop = asset ? asset.kind === 'fxLoop' : !!item.loop;
    if (Number.isInteger(item.untilStep)) f.untilStep = item.untilStep;
    if (item.facing) f.facing = item.facing;
    f.angle = Number(item.angle) || 0;
    if (item.layer) f.layer = item.layer;
    return f;
  };

  const gestures = (Array.isArray(doc.gestures) ? doc.gestures : []).filter((g) => g?.name).map((g) => {
    let gesture;
    if (Array.isArray(g.steps)) {
      gesture = {
        name: g.name, recover: g.recover,
        steps: g.steps.map((s) => ({ name: s.name || '', duration: s.duration, ease: s.ease, stretch: !!s.stretch, shake: s.shake || 0, aim: !!s.aim, poses: poses(s.poses) })),
        releases: (g.releases || []).map((r) => {
          const release = createRelease(r.step || 0, r.at ?? 1);
          if (r.point && r.point !== muzzlePath) {
            release.point = partOf(r.point);
            if (!release.point) missing.add(r.point);
          }
          release.offset = vec(r.offset);
          return release;
        }),
        fx: (g.fx || []).map((item) => {
          const f = hook(item, g.name);
          if (f) f.step = Math.max(0, Math.round(item.step || 0));
          return f;
        }).filter(Boolean),
      };
    } else {
      gesture = {
        name: g.name, recover: g.recover, strikeTime: g.strikeTime ?? 0.08, shake: g.shake ?? 1.5,
        windup: poses(g.windup), strike: poses(g.strike),
        fx: (g.phases || []).flatMap((ph) => (ph.fx || []).map((item) => {
          const f = hook(item, g.name);
          if (f) { f.phase = ph.phase; f.facing = item.facing || 'anchor'; }
          return f;
        })).filter(Boolean),
      };
    }
    normalizeGesture(gesture);
    return gesture;
  });

  if (missing.size) notes.push(`Piezas del archivo que este cuerpo no tiene (sus poses se omiten): ${[...missing].filter(Boolean).join(', ')}.`);
  const names = new Set(gestures.map((g) => g.name));
  const idle = doc.idleGestures ? {
    gestures: (doc.idleGestures.gestures || []).filter((n) => names.has(n)),
    interval: doc.idleGestures.interval ?? 4, chance: doc.idleGestures.chance ?? 0.6, windup: doc.idleGestures.windup ?? 0.5,
  } : null;
  const transition = doc.transitionGestures ? {
    gestures: (doc.transitionGestures.gestures || []).filter((n) => names.has(n)),
    chance: doc.transitionGestures.chance ?? 0.5, windup: doc.transitionGestures.windup ?? 0.3,
  } : null;
  return { gestures, idle, transition, notes };
}

// ============================================================ rig-config → document (the whole body)

/**
 * A rig-config written by UNITY (RigConfigExporter, through the bridge) → a composer document, for
 * the "Gestos y ataques" tab: enough to DRAW and POSE the body (nodes, sprites, pivots, draw order,
 * roles, rig fields, state sprites) and its gestures. The inverse of `buildRigExport` for what focus
 * mode needs; the body is read-only there, so this never has to round-trip one — the document is
 * marked `unityBody` and a full export of it is refused (`buildRigExport`).
 *
 * Every image keeps the project sprite it came from (`image.unityRef`): the partial export writes
 * that reference back instead of a new PNG, so a state sprite nobody changed is not re-imported.
 *
 * @param {object} doc - the rig-config.
 * @param {object} o
 * @param {object} o.sprites - per sprite reference: { file, rect, pivot, ppu, texW, texH } (WebBridgeRoutes ▸ SpriteInfo).
 * @param {(file: string) => Promise<string>} o.fetchDataURL - a project PNG as a data URL.
 * @param {Map<string, object>} [o.entriesById] - library effects (gesture effects that have a preview).
 * @param {object} [o.artLibrary] - effect prefab path → library id (the preview of a Unity prefab).
 * @returns {Promise<{ state: object, notes: string[] }>}
 */
export async function readRigDocument(doc, { sprites = {}, fetchDataURL, entriesById = new Map(), artLibrary = {} } = {}) {
  const notes = [];
  const state = createDefaultRigState();
  state.name = doc.name || '';
  state.kind = doc.kind === 'boss' ? 'boss' : 'enemy';
  state.unityBody = true;
  const vec = (v) => ({ x: Number(v?.x) || 0, y: Number(v?.y) || 0 });
  const scaleIn = (s) => (typeof s === 'number' ? { x: s, y: s } : s && typeof s === 'object' ? { x: s.x ?? 1, y: s.y ?? 1 } : { x: 1, y: 1 });

  // ---- images: one decode per file, one crop per sprite.
  const files = new Map();
  const fileImage = (file) => {
    if (!files.has(file)) files.set(file, fetchDataURL(file).then(loadImage));
    return files.get(file);
  };
  const nodes = Array.isArray(doc.parts) ? doc.parts : [];
  // The document has ONE pixels-per-unit: the first sprite's. Any other is resampled to it.
  let ppu = 0;
  for (const n of nodes) {
    const info = n.sprite ? sprites[n.sprite] : null;
    if (!info) continue;
    const img = await fileImage(info.file).catch(() => null);
    if (img) { ppu = info.ppu * (img.naturalWidth / info.texW); break; }
  }
  state.pixelsPerUnit = ppu > 0 ? ppu : 100;

  /** A project sprite as a part image + where its own pivot is in it (pixels, y down). */
  async function spriteImage(ref, look = {}) {
    const info = ref ? sprites[ref] : null;
    if (!info) return null;
    let img;
    try { img = await fileImage(info.file); } catch { notes.push(`No se pudo leer "${info.file}".`); return null; }
    const kx = img.naturalWidth / info.texW, ky = img.naturalHeight / info.texH;   // the PNG may be bigger than its import
    const k = state.pixelsPerUnit / (info.ppu * kx);
    const sw = info.rect.w * kx, sh = info.rect.h * ky;
    const w = Math.max(1, Math.round(sw * k)), h = Math.max(1, Math.round(sh * k));
    const canvas = document.createElement('canvas');
    canvas.width = w; canvas.height = h;
    const ctx = canvas.getContext('2d');
    ctx.save();
    ctx.translate(look.flipX ? w : 0, look.flipY ? h : 0);
    ctx.scale(look.flipX ? -1 : 1, look.flipY ? -1 : 1);
    ctx.drawImage(img, info.rect.x * kx, (info.texH - info.rect.y - info.rect.h) * ky, sw, sh, 0, 0, w, h);
    ctx.restore();
    if (look.tint && !/^#?f{6}(ff)?$/i.test(look.tint)) {   // SpriteRenderer.color multiplies the sprite
      const plain = document.createElement('canvas');
      plain.width = w; plain.height = h;
      plain.getContext('2d').drawImage(canvas, 0, 0);
      ctx.globalCompositeOperation = 'multiply';
      ctx.fillStyle = look.tint.slice(0, 7);
      ctx.fillRect(0, 0, w, h);
      ctx.globalCompositeOperation = 'destination-in';   // the multiply filled the transparent pixels too
      ctx.drawImage(plain, 0, 0);
      ctx.globalCompositeOperation = 'source-over';
    }
    let px = info.pivot.x * kx * k, py = h - info.pivot.y * ky * k;
    if (look.flipX) px = w - px;
    if (look.flipY) py = h - py;
    return {
      image: { dataURL: canvas.toDataURL('image/png'), w, h, origW: w, origH: h, cropX: 0, cropY: 0, unityRef: ref },
      pivot: { x: px, y: py },
    };
  }

  // ---- nodes → parts (same hierarchy, same names: the paths of the export are Unity's).
  const pathOfNode = (n) => (n.parent ? `${n.parent}/${n.name}` : n.name);
  const partAt = new Map();      // path -> part
  const origin = new Map();      // part uid -> its transform origin in the image (pixels) = the sprite's pivot
  const looks = new Map();       // part uid -> { flipX, flipY, tint }
  const joints = new Map();      // part uid -> RigPart.pivot (local units), applied at the end
  const order = new Map();       // part uid -> sortingOrder
  const swapped = new Set((doc.spriteSwaps || []).map((s) => s.part));
  for (const [i, n] of nodes.entries()) {
    const part = createPart({ name: n.name, index: i });
    part.rigPart = n.rigPart !== false;
    part.position = vec(n.position);
    part.rotation = Number(n.rotation) || 0;
    part.scale = scaleIn(n.scale);
    if (typeof n.sortingOrder === 'number') order.set(part.uid, n.sortingOrder);
    const look = { flipX: !!n.flipX, flipY: !!n.flipY, tint: n.color || null };
    looks.set(part.uid, look);
    // A node switched off in the prefab is not drawn — unless it has state sprites (they need its image).
    const loaded = n.sprite && (n.active !== false || swapped.has(pathOfNode(n))) ? await spriteImage(n.sprite, look) : null;
    part.image = loaded?.image || null;
    part.pivot = loaded?.pivot || { x: 0, y: 0 };
    origin.set(part.uid, { ...part.pivot });
    if (part.rigPart) {
      const c = vec(n.center);
      part.center = c.x || c.y ? { x: part.pivot.x + c.x * state.pixelsPerUnit, y: part.pivot.y - c.y * state.pixelsPerUnit } : null;
      part.pointAngle = Number(n.pointAngle) || 0;
      part.leanFollow = Number(n.leanFollow) || 0;
      if (n.sway) part.sway = { move: vec(n.sway.move), rotate: Number(n.sway.rotate) || 0, scale: vec(n.sway.scale), speed: Number(n.sway.speed) || 0, phase: Number(n.sway.phase) || 0 };
      part.breaksOnDeath = n.breaksOnDeath !== false;
      joints.set(part.uid, vec(n.pivot));
    } else {
      part.sway = { move: { x: 0, y: 0 }, rotate: 0, scale: { x: 0, y: 0 }, speed: 0, phase: 0 };
    }
    partAt.set(pathOfNode(n), part);
    state.parts.push(part);
  }
  for (const n of nodes) partAt.get(pathOfNode(n)).parent = partAt.get(n.parent)?.uid || null;

  const uidAt = (path) => partAt.get(path)?.uid || null;
  /** Where a node sits, as a pixel of another part's image. */
  const pixelIn = (host, node) => {
    const p = apply(multiply(invert(worldMatrix(state, host)), worldMatrix(state, node)), { x: 0, y: 0 });
    const o = origin.get(host.uid);
    return { x: o.x + p.x * state.pixelsPerUnit, y: o.y - p.y * state.pixelsPerUnit };
  };
  /** A helper node (a point: no art, not a rig part, nothing hanging from it) is not a part of the document. */
  const dropHelper = (part) => {
    if (part.image || part.rigPart || state.parts.some((c) => c.parent === part.uid)) return;
    state.parts = state.parts.filter((p) => p !== part);
  };

  // ---- rig block: scalars by name, the Transform fields as roles.
  const rig = doc.rig || {};
  Object.keys(state.rig).forEach((k) => { if (rig[k] !== undefined && rig[k] !== null) state.rig[k] = rig[k]; });
  const roles = state.roles;
  const aim = partAt.get(rig.aimPart);
  const muzzle = partAt.get(rig.muzzle);
  if (aim) roles.aim = aim.uid;
  if (aim && muzzle) { roles.muzzle = pixelIn(aim, muzzle); dropHelper(muzzle); }
  roles.stretch = uidAt(rig.stretchPart);
  roles.riders = (rig.stretchRiders || []).map(uidAt).filter(Boolean);

  if (doc.bossLimbs) {
    roles.limbs = (doc.bossLimbs.limbs || []).map(uidAt).filter(Boolean);
    roles.orbiters = (doc.bossLimbs.orbiters || []).map(uidAt).filter(Boolean);
    (doc.bossLimbs.grips || []).forEach((path) => {
      const grip = partAt.get(path);
      const limb = grip ? state.parts.find((p) => p.uid === grip.parent) : null;
      if (!limb || !roles.limbs.includes(limb.uid)) return;
      roles.grips[limb.uid] = pixelIn(limb, grip);
      dropHelper(grip);
    });
    state.bossLimbs = { contactRadius: doc.bossLimbs.contactRadius ?? state.bossLimbs.contactRadius, orbiterRadius: doc.bossLimbs.orbiterRadius ?? state.bossLimbs.orbiterRadius };
  }
  if (doc.bossBody) {
    const { bodyRenderer, movement, ...numbers } = doc.bossBody;
    state.bossBody = { ...state.bossBody, ...numbers, bodyRenderer: uidAt(bodyRenderer) };
  }

  // ---- state sprites: each one lined up on the part's transform origin (its own pivot in Unity).
  for (const swap of doc.spriteSwaps || []) {
    const part = partAt.get(swap.part);
    if (!part?.image) continue;
    const o = origin.get(part.uid);
    const variant = async (ref) => {
      const loaded = await spriteImage(ref, looks.get(part.uid));
      if (!loaded) return null;
      return { ...loaded.image, origW: part.image.origW, origH: part.image.origH, cropX: o.x - loaded.pivot.x, cropY: o.y - loaded.pivot.y };
    };
    const v = part.variants;
    v.asleep = await variant(swap.asleep);
    v.awake = await variant(swap.awake);
    v.attacking = await variant(swap.attacking);
    // Stage 0 is the part's own sprite; variants.stages[i] = stage i + 1.
    v.stages = [];
    for (const ref of (swap.stages || []).slice(1)) v.stages.push(ref && ref !== part.image.unityRef ? await variant(ref) : null);
    for (const n of swap.named || []) {
      const image = await variant(n.sprite);
      if (!image) continue;
      if (n.state === 'gesture') {
        if (!n.gesture) continue;
        v.gestures[n.gesture] = image;
        const from = Number(n.from) || 0, to = n.to ?? 99;
        if (from > 0 || to < 99) v.gestureWindows[n.gesture] = { from, to };
      } else if (n.state in v.named) v.named[n.state] = image;
    }
  }

  // ---- the real joint (RigPart.pivot): moved there now that roles and variants are placed.
  state.parts.forEach((part) => {
    const j = joints.get(part.uid);
    if (!j || (!j.x && !j.y)) return;
    const o = origin.get(part.uid);
    setPivot(state, part, { x: o.x + j.x * state.pixelsPerUnit, y: o.y - j.y * state.pixelsPerUnit });
  });

  // ---- draw order = Unity's sorting order (a node without art stays next to what precedes it).
  let last = -Infinity;
  const keyed = state.parts.map((part, i) => {
    if (part.image && order.has(part.uid)) last = order.get(part.uid);
    return { part, key: part.image && order.has(part.uid) ? order.get(part.uid) : last, i };
  });
  keyed.sort((a, b) => a.key - b.key || a.i - b.i);
  state.parts = keyed.map((k) => k.part);

  // ---- root: the body is drawn in Visual's space; the collider is the root's.
  const rootScale = scaleIn(doc.root?.scale);
  const visual = { position: vec(doc.visual?.position), scale: scaleIn(doc.visual?.scale) };
  const vs = Math.abs(visual.scale.x) || 1;
  state.rootScale = (Math.abs(rootScale.x) || 1) * vs;
  const inVisual = (p) => ({ x: (p.x - visual.position.x) / vs, y: (p.y - visual.position.y) / vs });
  if (doc.collider) {
    const c = doc.collider;
    state.collider = c.type === 'circle'
      ? { type: 'circle', radius: (c.radius || 0.5) / vs, size: { x: 1, y: 1 }, offset: inVisual(vec(c.offset)) }
      : { type: c.type === 'capsule' ? 'capsule' : 'box', size: { x: (c.size?.x || 1) / vs, y: (c.size?.y || 1) / vs }, offset: inVisual(vec(c.offset)) };
  }
  if (doc.corpse) state.corpse = { linger: doc.corpse.linger ?? state.corpse.linger, fadeDuration: doc.corpse.fadeDuration ?? state.corpse.fadeDuration };
  if (doc.root?.feet) state.feetY = inVisual(vec(doc.root.feet)).y;

  // ---- gestures, over the body just built.
  const read = readRigGestures(doc, state, { entriesById, artLibrary, muzzlePath: rig.muzzle || null });
  state.gestures = read.gestures;
  if (read.idle) state.idle = read.idle;
  if (read.transition) state.transition = read.transition;
  notes.push(...read.notes);
  return { state, notes };
}

// ============================================================ only gestures + attacks (partial)

/**
 * The part of the rig-config the "Gestos y ataques" tab owns: gestures, idle gestures, state sprites.
 * @returns {{ doc: object, files: Array<{path, dataURL}>, problems: string[], notes: string[] }}
 */
export function buildGesturesExport(state, ctx = {}) {
  const full = buildRigExport(state, ctx);
  const swaps = full.doc.spriteSwaps || [];
  const used = new Set();
  swaps.forEach((s) => [s.asleep, s.awake, s.attacking, ...(s.stages || []), ...(s.named || []).map((n) => n.sprite)]
    .forEach((ref) => { if (ref) used.add(ref); }));

  const doc = { partial: true, kind: full.doc.kind, name: full.doc.name, pixelsPerUnit: full.doc.pixelsPerUnit };
  doc.sprites = Object.fromEntries(Object.entries(full.doc.sprites).filter(([path]) => used.has(path)));
  doc.spriteSwaps = swaps;
  ['gestures', 'idleGestures', 'transitionGestures', 'artAssets'].forEach((k) => { if (full.doc[k] !== undefined) doc[k] = full.doc[k]; });

  // Only what concerns this bundle: the body's own problems (an empty slot, no collider…) are not its business.
  const problems = full.problems.filter((p) => /^Gesto |^Ponle nombre/i.test(p));
  if (!Array.isArray(doc.gestures)) full.notes.push('Este documento no exporta gestos: Unity conserva los que tenga el prefab.');
  return { doc, files: full.files.filter((f) => used.has(f.path) || f.path.startsWith('Audio/')), problems, notes: full.notes.filter((n) => /gest|sprite/i.test(n)) };
}

/** The deck part of an EnemyConfig: its attacks, the cards of the draw, the defences. Null = it has none. */
export function partialEnemyConfig(enemyConfig, name) {
  const t = enemyConfig?.tuning;
  if (!t || (!Array.isArray(t.attacks) && !Array.isArray(t.defences))) return null;
  const cfg = { kind: 'enemy', partial: true, enemyName: name, tuning: {} };
  if (enemyConfig.attacks) cfg.attacks = JSON.parse(JSON.stringify(enemyConfig.attacks));
  if (enemyConfig.artAssets) cfg.artAssets = JSON.parse(JSON.stringify(enemyConfig.artAssets));
  ['attacks', 'builtInAttack', 'builtInWeight', 'defences'].forEach((k) => { if (t[k] !== undefined) cfg.tuning[k] = JSON.parse(JSON.stringify(t[k])); });
  return cfg;
}

/** The partial .zip: only gestures (+ state sprites, effects) and the enemy's attacks. */
export async function buildGesturesBundle(state, { enemyConfig = null, entriesById = new Map() } = {}) {
  const { doc, files } = buildGesturesExport(state, { entriesById });
  const zip = new window.JSZip();
  zip.file('rig-config.json', JSON.stringify(doc, null, 2));
  files.forEach((f) => zip.file(f.path, f.dataURL.split(',')[1], { base64: true }));
  await embedAttackArt(zip, doc);   // frames of the gestures' effects → Art/<libraryId>/

  const cfg = state.kind === 'enemy' ? partialEnemyConfig(enemyConfig, doc.name) : null;
  if (cfg) {
    zip.file('enemy-config.json', JSON.stringify(cfg, null, 2));
    await embedAttackArt(zip, cfg);   // art of the deck's attacks → Art/<libraryId>/
  }
  return zip.generateAsync({ type: 'blob' });
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
