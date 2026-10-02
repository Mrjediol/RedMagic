// tools/comfy/make-enemy.mjs
// -----------------------------------------------------------------------------
// A part-based enemy without the browser: the finished part PNGs + a small "brief" (JSON) → the same
// .zip 🧬 Enemy Studio exports, ready for Unity's Tools ▸ Web ▸ Rig ▸ Importar rig.
// Nothing here knows how a rig, an attack or an effect is exported: it drives the web app's own
// modules (rig-templates, rig-export, enemy-export, vfx-presets), so the zip is what the Studio would
// have written for the same choices — and it can still be opened and adjusted there.
//
//   node make-enemy.mjs <brief.json>          → <brief folder>/out/<name>.zip  (+ the JSONs, to read)
//
// The brief (see enemies/*.json):
//   name          prefab name
//   template      a body template of rig-templates.js ('floatingCaster', 'foldingSentry', …)
//   parts         folder with the final PNGs (finish-parts.mjs), relative to Comfy's output or absolute.
//                 `<Slot name>.png` fills the slot; `<Slot name>__<state>.png` is that part's sprite for
//                 a state (asleep | awake | alert | attacking | hurt | dead | gesture_<Name>).
//   height        how tall the enemy is in the world (units); the art is scaled to it
//   slots         optional per slot id: { pivot: [x, y], muzzle: [x, y] } normalised on the part's own
//                 box (x from the left, y from the top), when the template's guess does not fit the art
//   tuning        EnemyTuning overrides on top of the template's behaviour
//   effects       VFX Lab presets baked for this enemy: { <key>: { name, preset, params } }
//   projectile    the built-in shot: { effect: <key>, …ProjectileSpec overrides }
//   attacks       the deck: [{ id, type, effect?, worldWidth?, common: {…}, params: {…}, deck: { minRange, maxRange } }]
//                 (types and fields = Unity's attack catalog, boss-catalog.js)
//   gestures      gestures of its own, as TIMELINES (any number of steps): [{ name, recover,
//                 steps: [{ name, duration, ease: 'inout'|'out'|'in'|'linear', stretch, shake,
//                           poses: [[slotId | 'Visual', [moveX, moveY], rotate]] }],
//                 releases: [{ step, at, point: slotId | 'Muzzle', offset: [x, y] }] }]
//                 A step's pose is measured from rest; moves and release offsets are in the template's
//                 units (they scale with the body). `stretch` = that step lasts the attack's telegraph.
//                 A release = the instant the attack leaves (several = it fires several times); none =
//                 end of the last step.
//   releases      the same, for a gesture of the template: { <Gesture>: [{ step, at, point }] }
//                 (a template gesture is step 0 = wind-up, step 1 = strike; default: end of step 0)
//   gestureFx     effects per gesture: { <Gesture>: [{ effect: <key>, step, at, loop, untilStep,
//                 size (world units), anchor: 'muzzle'|'body'|'feet', facing: 'direction'|'anchor'|'world',
//                 layer: 'front'|'behind', angle, followsPart }] }   (step = number of steps → the way back to rest)
//                 A gesture with an effect has no placeholder flash: the effect IS the flash.
//   builtIn       the enemy's own shot / melee launched with a gesture: [{ gesture, weight }]
//   defences      EnemyDefence entries (enemy-defaults.js createDefence): how it gets out of the way

import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { inOutput } from './contact-sheet.mjs';

const require = createRequire(import.meta.url);
const { createCanvas, loadImage } = require('@napi-rs/canvas');
const JSZip = require('jszip');

// The web modules draw on a canvas they ask the page for; here the page is node.
globalThis.document = { createElement: (tag) => { if (tag !== 'canvas') throw new Error(`no <${tag}> outside the browser`); return createCanvas(1, 1); } };

const here = path.dirname(fileURLToPath(import.meta.url));
const mod = (name) => import(pathToFileURL(path.join(here, '../../modules', name)).href);
const T = await mod('rig-templates.js');
const M = await mod('rig-model.js');
const { buildRigExport, companionEnemyConfig } = await mod('rig-export.js');
const { buildEnemyExport } = await mod('enemy-export.js');
const { createDefaultEnemyConfig, createDefence } = await mod('enemy-defaults.js');
const { BOSS_CATALOG } = await mod('boss-catalog.js');
const { newAttack } = await mod('attack-cards.js');
const { populateSpriteZip, cropToDataURL } = await mod('export-manifest.js');
const { framedLanes, laneFolder } = await mod('boss-export.js');
const V = await mod('vfx-presets.js');

const briefPath = path.resolve(process.argv[2] || '');
if (!fs.existsSync(briefPath)) { console.error('usage: node make-enemy.mjs <brief.json>'); process.exit(1); }
const brief = JSON.parse(fs.readFileSync(briefPath, 'utf8'));
const problems = [], notes = [];

// ============================================================ the part images

/** A PNG → the trimmed image record the composer keeps ({ dataURL, w, h, origW, origH, cropX, cropY }). */
async function imageRecord(file) {
  const img = await loadImage(file);
  const full = createCanvas(img.width, img.height);
  const ctx = full.getContext('2d');
  ctx.drawImage(img, 0, 0);
  const d = ctx.getImageData(0, 0, img.width, img.height).data;
  let x0 = img.width, y0 = img.height, x1 = -1, y1 = -1;
  for (let y = 0; y < img.height; y++) for (let x = 0; x < img.width; x++) {
    if (d[(y * img.width + x) * 4 + 3] > 10) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  }
  if (x1 < 0) return null;
  const w = x1 - x0 + 1, h = y1 - y0 + 1;
  const cut = createCanvas(w, h);
  cut.getContext('2d').drawImage(full, x0, y0, w, h, 0, 0, w, h);
  return { dataURL: cut.toDataURL('image/png'), w, h, origW: img.width, origH: img.height, cropX: x0, cropY: y0 };
}

const partsDir = inOutput(brief.parts);
const pngs = fs.readdirSync(partsDir).filter((f) => f.endsWith('.png') && !f.startsWith('_'));

// ============================================================ the body

const baseTemplate = T.templateById(brief.template);
if (!baseTemplate) { console.error(`✗ no template '${brief.template}'`); process.exit(1); }
// The brief's own gestures join the template's, as template gestures (timelines whose poses name a
// slot id, or 'Visual'): the document — and so the export — carries them like any other.
const point = (id) => (!id || id === 'Muzzle' ? null : id);
const briefRelease = (r) => ({ step: r.step ?? 0, at: r.at ?? 1, point: point(r.point), offset: r.offset || [0, 0] });
const template = { ...baseTemplate, gestures: [...(baseTemplate.gestures || []),
  ...(brief.gestures || []).map((g) => ({
    name: g.name, recover: g.recover ?? 0.3,
    steps: g.steps.map((st) => ({ name: st.name || '', duration: st.duration ?? 0.3, ease: st.ease || 'inout', stretch: !!st.stretch, shake: st.shake || 0,
      poses: (st.poses || []).map(([part, move = [0, 0], rotate = 0, scale = [0, 0]]) => ({ part, move, rotate, scale })) })),
    releases: (g.releases || []).map(briefRelease),
  }))] };
// A slot id (or a part's name) the brief names that this template does not have.
const known = new Set(['Visual', ...baseTemplate.slots.flatMap((s) => [s.id, s.name])]);
const unknown = (id, where) => { if (id && !known.has(id)) problems.push(`${where}: no part '${id}'`); };
(brief.gestures || []).forEach((g) => {
  g.steps.forEach((st) => (st.poses || []).forEach(([part]) => unknown(part, `gesture ${g.name}`)));
  (g.releases || []).forEach((r) => {
    unknown(point(r.point), `gesture ${g.name}`);
    if ((r.step ?? 0) >= g.steps.length) problems.push(`gesture ${g.name}: a release on step ${r.step}, it has ${g.steps.length}`);
  });
});
const state = T.instantiateTemplate(template);
state.name = brief.name;
// Releases the brief gives to a gesture of the template (default: the end of its wind-up).
const uidOfSlot = (id) => state.parts.find((p) => p.slot?.id === id || p.name === id)?.uid || null;
for (const [gestureName, list] of Object.entries(brief.releases || {})) {
  const gesture = state.gestures.find((g) => g.name === gestureName);
  if (!gesture) { problems.push(`releases: no gesture '${gestureName}'`); continue; }
  gesture.releases = list.map((r) => {
    unknown(point(r.point), `releases ${gestureName}`);
    if ((r.step ?? 0) >= gesture.steps.length) problems.push(`gesture ${gestureName}: a release on step ${r.step}, it has ${gesture.steps.length}`);
    const out = briefRelease(r);
    return { ...out, point: out.point ? uidOfSlot(out.point) : null, offset: { x: out.offset[0], y: out.offset[1] } };
  });
}

// The slot that sizes the schematic goes first (the body), then the rest land where their canvas puts them.
const slots = state.parts.filter((p) => p.slot);
const order = [...slots].sort((a, b) => (b.slot.id === 'torso' || b.slot.id === 'base') - (a.slot.id === 'torso' || a.slot.id === 'base'));
for (const part of order) {
  const file = pngs.find((f) => f.toLowerCase() === `${part.name.toLowerCase()}.png`);
  if (!file) {
    if (!part.slot.optional) problems.push(`slot '${part.slot.id}' (${part.name}.png) is required and missing`);
    continue;
  }
  const custom = brief.slots?.[part.slot.id] || {};
  if (custom.pivot) part.slot.pivot = custom.pivot;
  if (custom.muzzle) part.slot.muzzle = custom.muzzle;
  T.fillSlot(state, part, await imageRecord(path.join(partsDir, file)));

  for (const variant of pngs.filter((f) => f.startsWith(`${part.name}__`))) {
    const stateName = variant.slice(part.name.length + 2, -4);
    const id = stateName.startsWith('gesture_') ? M.gestureState(stateName.slice(8)) : stateName;
    if (!id.startsWith('gesture:') && !M.SPRITE_STATES.some((s) => s.id === id)) { problems.push(`${variant}: unknown state '${stateName}'`); continue; }
    M.setVariant(part, id, await imageRecord(path.join(partsDir, variant)));
  }
}
// Optional slots with no art leave the document: an empty slot is not a part.
state.parts = state.parts.filter((p) => p.image || !p.slot || !p.slot.optional);

/** The box around every drawn part at rest, in the document's units. */
function bounds() {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const part of state.parts) {
    if (!part.image) continue;
    const { cropX: x, cropY: y, w, h } = part.image;
    const world = M.worldMatrix(state, part);
    for (const px of [{ x, y }, { x: x + w, y }, { x, y: y + h }, { x: x + w, y: y + h }]) {
      const p = M.apply(world, M.pixelToLocal(state, part, px));
      minX = Math.min(minX, p.x); maxX = Math.max(maxX, p.x); minY = Math.min(minY, p.y); maxY = Math.max(maxY, p.y);
    }
  }
  return { minX, minY, maxX, maxY, w: maxX - minX, h: maxY - minY };
}

// What the composer does when the last required slot is filled: lengths to the art's size, the body
// centred on the root (an enemy's origin is its body centre), the collider around it.
const r2 = (v) => Math.round(v * 100) / 100;
let b = bounds();
T.fitLengths(state, b.h);   // template units → this body's units (sway, gesture moves, release offsets)
const centre = { x: (b.minX + b.maxX) / 2, y: (b.minY + b.maxY) / 2 };
state.parts.forEach((p) => { if (!p.parent) p.position = { x: p.position.x - centre.x, y: p.position.y - centre.y }; });
b = bounds();
state.collider.size = { x: r2(b.w * (brief.colliderFill?.[0] ?? 0.6)), y: r2(b.h * (brief.colliderFill?.[1] ?? 0.85)) };
state.collider.offset = { x: 0, y: 0 };
state.rootScale = Math.round((brief.height / b.h) * 1e4) / 1e4;
Object.assign(state.rig, brief.rig || {});

// ============================================================ effects (VFX Lab presets, baked here)

const entriesById = new Map();
const effectId = {};
for (const [key, fx] of Object.entries(brief.effects || {})) {
  const preset = V.presetById(fx.preset);
  if (!preset) { problems.push(`effect '${key}': no preset '${fx.preset}'`); continue; }
  const baked = V.bakePreset(preset, V.normalizeParams(preset, fx.params || {}), (w, h) => createCanvas(w, h));
  // A stable id: importing again rebuilds the same prefab instead of adding another.
  const id = `claude-${brief.name}-${key}`.toLowerCase();
  effectId[key] = id;
  entriesById.set(id, { id, enemyName: fx.name || `${brief.name}${key}`, kind: preset.kind, baked,
    sprite: { lanes: baked.lanes, boxes: baked.boxes, width: baked.width, height: baked.height, density: baked.density } });
}

// Effects on the gestures: the charge at the wand's tip while it winds up, the flash when it lets go.
// `size` is in world units; the hook's scale is worked out from the effect's own frame.
for (const [gestureName, hooks] of Object.entries(brief.gestureFx || {})) {
  const gesture = (state.gestures || []).find((g) => g.name === gestureName);
  if (!gesture) { problems.push(`gestureFx: no gesture '${gestureName}'`); continue; }
  gesture.fx = gesture.fx || [];
  for (const hook of hooks) {
    const id = effectId[hook.effect];
    const entry = entriesById.get(id);
    if (!entry) { problems.push(`gestureFx ${gestureName}: no effect '${hook.effect}'`); continue; }
    const nominal = entry.sprite.boxes[0].w / (entry.sprite.density || 1) / state.pixelsPerUnit;   // units at scale 1
    gesture.fx.push({ fx: id, step: Math.min(hook.step ?? 0, gesture.steps.length), at: hook.at ?? 0, anchor: hook.anchor || 'muzzle', part: null,
      offset: { x: 0, y: 0 }, scale: Math.round(((hook.size ?? 1) / state.rootScale / nominal) * 100) / 100,
      followsPart: hook.followsPart !== false, loop: !!hook.loop, untilStep: hook.untilStep ?? -1, facing: hook.facing || 'direction',
      angle: hook.angle || 0, layer: hook.layer || 'front' });
  }
}

// ============================================================ behaviour, projectile, deck

const cfg = createDefaultEnemyConfig();
cfg.enemyName = brief.name;
Object.assign(cfg.tuning, JSON.parse(JSON.stringify(template.behaviour?.tuning || {})), brief.tuning || {});
if (template.behaviour?.projectile || brief.projectile) {
  const { effect, ...spec } = brief.projectile || {};
  cfg.tuning.projectileMode = 'inline';
  Object.assign(cfg.tuning.projectileSpec, JSON.parse(JSON.stringify(template.behaviour?.projectile || {})), spec);
  if (effect) {
    if (effectId[effect]) cfg.tuning.projectileSpec.libraryId = effectId[effect];
    else problems.push(`projectile: no effect '${effect}'`);
  }
}

const gestureNames = (state.gestures || []).map((g) => g.name);
for (const a of brief.attacks || []) {
  const card = newAttack(BOSS_CATALOG, a.type, cfg.deck.map((c) => c.id));
  if (card.type !== a.type) { problems.push(`attack '${a.id}': no type '${a.type}' in the catalog`); continue; }
  card.id = a.id;
  Object.assign(card.common, a.common || {});
  Object.keys(a.common || {}).forEach((k) => { card.edited[k] = true; });
  for (const [k, v] of Object.entries(a.params || {})) {
    if (v && typeof v === 'object' && !Array.isArray(v) && card.params[k] && typeof card.params[k] === 'object') {
      Object.assign(card.params[k], v);
      Object.keys(v).forEach((sub) => { card.edited[`${k}.${sub}`] = true; });
    } else { card.params[k] = v; card.edited[k] = true; }
  }
  if (a.effect) {
    const id = effectId[a.effect];
    if (!id) problems.push(`attack '${a.id}': no effect '${a.effect}'`);
    else {
      card.params.projectile.prefab = id;
      cfg.attackArt[`${id}:projectile`] = { worldWidth: a.worldWidth ?? 1, colliderScale: 0.7, faceTravelDirection: true };
    }
  }
  card.deck = { minRange: 0, maxRange: 0, targetHeight: 'Any', ...(a.deck || {}) };
  cfg.deck.push(card);
}
cfg.deckTouched = true;

// The enemy's own attack launched with a gesture (so its effects play instead of the placeholder flash).
if (brief.builtIn) {
  cfg.builtInVariants = [].concat(brief.builtIn).map((v) => ({ gesture: v.gesture, weight: v.weight ?? 1, minRange: v.minRange ?? 0, maxRange: v.maxRange ?? 0, targetHeight: 'Any' }));
  cfg.tuning.builtInAttack = false;   // only through its variants
}
if (brief.defences) {
  cfg.defences = brief.defences.map((d) => ({ ...createDefence(), ...d }));
  cfg.defencesTouched = true;
}

const enemy = buildEnemyExport(cfg, { catalog: BOSS_CATALOG, entriesById, gestureNames });
const rig = buildRigExport(state, { entriesById });
problems.push(...(rig.problems || []), ...(enemy.problems || []));
notes.push(...(rig.notes || []), ...(enemy.notes || []));
const enemyDoc = companionEnemyConfig(enemy.doc, rig.doc.name);

// ============================================================ the zip

const zip = new JSZip();
zip.file('rig-config.json', JSON.stringify(rig.doc, null, 2));
rig.files.forEach((f) => zip.file(f.path, f.dataURL.split(',')[1], { base64: true }));
zip.file('enemy-config.json', JSON.stringify(enemyDoc, null, 2));

const frames = (folder, entry) => framedLanes(entry).forEach((lane) => {
  const dir = folder.folder(laneFolder(lane.name));
  lane.frameBoxIndices.forEach((box, i) => dir.file(`frame_${String(i).padStart(3, '0')}.png`,
    cropToDataURL(entry.baked.canvas, entry.sprite.boxes[box]).split(',')[1], { base64: true }));
});
// Art of the deck's attacks and of the gestures' effects → Art/<libraryId>/<Lane>/.
const artIds = new Set([...Object.values(enemyDoc.artAssets || {}), ...Object.values(rig.doc.artAssets || {})].map((a) => a.libraryId));
artIds.forEach((id) => { if (entriesById.has(id)) frames(zip.folder('Art').folder(id), entriesById.get(id)); });
// The built-in projectile is a library entry → Library/<id>/ (manifest + frames): the import builds its prefab.
const shot = enemyDoc.tuning?.projectile?.libraryId;
if (shot && entriesById.has(shot)) {
  const entry = entriesById.get(shot);
  const folder = zip.folder('Library').folder(shot);
  folder.file('manifest.json', JSON.stringify(populateSpriteZip(folder, { enemyName: entry.enemyName, kind: entry.kind, libraryId: shot,
    lanes: entry.sprite.lanes, boxes: entry.sprite.boxes, source: entry.baked.canvas }), null, 2));
}

const outDir = path.join(path.dirname(briefPath), 'out');
fs.mkdirSync(outDir, { recursive: true });
const zipPath = path.join(outDir, `${brief.name}.zip`);
fs.writeFileSync(zipPath, await zip.generateAsync({ type: 'nodebuffer' }));
fs.writeFileSync(path.join(outDir, `${brief.name}.rig-config.json`), JSON.stringify(rig.doc, null, 2));
fs.writeFileSync(path.join(outDir, `${brief.name}.enemy-config.json`), JSON.stringify(enemyDoc, null, 2));

const drawn = state.parts.filter((p) => p.image);
console.log(`${brief.name}: ${drawn.length} parts (${drawn.map((p) => p.name).join(', ')}), ${rig.files.length} sprites, `
  + `${(state.gestures || []).length} gestures, ${Object.keys(enemyDoc.attacks || {}).length} deck attack(s), ${entriesById.size} effect(s)`);
console.log(`body ${b.w.toFixed(1)} x ${b.h.toFixed(1)} units → root scale ${state.rootScale} (${brief.height} units tall), aim art angle ${state.rig.aimArtAngle}°`);
notes.forEach((n) => console.log(`  · ${n}`));
problems.forEach((p) => console.log(`  ✗ ${p}`));
console.log(`${problems.length ? '✗' : '✓'} ${zipPath}`);
process.exit(problems.length ? 1 : 0);
