// modules/rig-fx.js
// -----------------------------------------------------------------------------
// EFFECTS HOOKED TO A GESTURE (RigFx in ProceduralRig.cs), independent of any DOM.
//
// A gesture is a timeline of steps (rig-gestures.js) and carries effects at any instant of it:
//
//   { fx: <library id>, step, at: 0..1, anchor: 'part' | 'body' | 'muzzle' | 'feet', part: <uid>,
//     offset: {x, y}, scale, followsPart, loop, untilStep, facing, angle, layer }
//
// `step` + `at` is the instant it appears (step number "how many steps" = the way back to rest; at 0 =
// the start of the step, 1 = its end). `offset` is in the anchor's own local space (units). `loop` =
// it repeats until the end of `untilStep` (-1 = its own step); otherwise it plays once.
// `facing`: 'direction' (default — at the muzzle, where the weapon points; on the body / feet, where
// the body faces), 'anchor' (turns with its part) or 'world' (upright); `angle` adds to it.
// `layer`: 'front' | 'behind' the body.
//
// The effect itself is a library entry (kind fx or projectile, usually baked in ✨ VFX Lab). Which
// of its lanes plays is decided here once, for the preview AND the export, so both agree:
// pickLane(). In Unity it becomes a pooled prefab built by BossArtBuilder from the `artAssets`
// block this module writes (kind `fx` = one shot, `fxLoop` = loops until the driver stops it).
// A hook read back from a rig-config that Unity exported has no library entry: it carries the
// prefab's path in `prefab` (`fx` null) and goes out again as it came — it cannot be previewed here.
//
// Timing mirrors RigDriver.FireFx / EndFxStep: keep both in step.

import { framedLanes, laneFolder } from './boss-export.js';
import { timeline, segment, timeOf } from './rig-gestures.js';

export const FX_ANCHORS = ['part', 'body', 'muzzle', 'feet'];
export const FX_FACINGS = ['direction', 'anchor', 'world'];
export const FX_FACING_LABELS = { direction: 'Hacia donde va la acción', anchor: 'Gira con su ancla', world: 'Siempre derecho' };
export const FX_LAYERS = ['front', 'behind'];

/** A new hook with RigFx's defaults. */
export function createFx(libraryId, step = 0, at = 0, anchor = 'body', partUid = null) {
  return { fx: libraryId, step, at, anchor, part: anchor === 'part' ? partUid : null,
    offset: { x: 0, y: 0 }, scale: 1, followsPart: true, loop: false, untilStep: -1, facing: 'direction', angle: 0, layer: 'front' };
}

/** The hooks of a gesture (documents saved before effects existed have none). */
export function fxList(gesture) {
  return gesture.fx || (gesture.fx = []);
}

/** The step at the end of which a looping hook goes out — RigFx.EndStep, capped at the way back. */
export function fxEndStep(f, count) {
  return Math.min(count, f.untilStep >= 0 ? Math.max(f.untilStep, f.step) : f.step);
}

// ============================================================ which frames an effect plays

/**
 * The lane of the entry a hook plays: a loop prefers the sustained lane (`Loop`, a projectile's
 * `Move`), a one-shot the burst (`Spawn`, a projectile's `Impact`); otherwise the first with frames.
 */
export function pickLane(entry, loop) {
  const lanes = framedLanes(entry);
  const wanted = loop ? ['Loop', 'Move'] : ['Spawn', 'Impact'];
  return lanes.find((l) => wanted.includes(l.name)) || lanes[0] || null;
}

/**
 * How many sheet pixels make one pixel of the effect's nominal size. VFX Lab bakes its effects finer
 * than their size (`sprite.density`, the "Nitidez" parameter) so they stay sharp next to high
 * resolution parts; an effect's size on the body never depends on how fine it was baked.
 */
export function fxDensity(entry) {
  return entry?.sprite?.density > 0 ? entry.sprite.density : 1;
}

/** Frames of the lane a hook plays: `{ lane, boxes, fps, duration }`, or null (no art). */
export function fxFrames(entry, loop) {
  const lane = entry ? pickLane(entry, loop) : null;
  if (!lane) return null;
  const fps = Math.max(0.1, lane.fps || 10);
  const boxes = lane.frameBoxIndices.map((i) => entry.sprite.boxes[i]).filter(Boolean);
  if (!boxes.length) return null;
  return { lane, boxes, fps, duration: boxes.length / fps };
}

// ============================================================ timing (RigDriver.FireFx / EndFxStep)

/**
 * The hooks alive `t` seconds into the gesture, with the frame each one shows.
 * @param {(fx: object) => ({boxes: object[], fps: number, duration: number} | null)} framesOf
 * @returns {Array<{fx: object, index: number, frame: number}>}
 */
export function activeFx(gesture, t, lead, framesOf, tl = timeline(gesture, lead)) {
  const out = [];
  (gesture.fx || []).forEach((fx, index) => {
    const frames = framesOf(fx);
    if (!frames) return;
    const age = t - timeOf(tl, fx.step, fx.at || 0);
    if (age < 0) return;
    if (fx.loop) {
      const end = segment(tl, fxEndStep(fx, tl.count));
      if (t >= end.start + end.length) return;           // a loop dies with its last step
      out.push({ fx, index, frame: Math.floor(age * frames.fps) % frames.boxes.length });
    } else if (age < frames.duration) {
      out.push({ fx, index, frame: Math.min(frames.boxes.length - 1, Math.floor(age * frames.fps)) });
    }
  });
  return out;
}

// ============================================================ export (rig-config `artAssets`)

/**
 * A stable artAssets id for (entry, loop). It names the prefab in Unity (Fx_<enemy>_<id>), so it
 * reads: the effect's name + the start of its library id (two effects may share a name).
 */
export function fxArtId(entry, loop) {
  const name = String(entry.enemyName || '').normalize('NFD').replace(/[^A-Za-z0-9]/g, '').slice(0, 24) || 'fx';
  const short = String(entry.id).replace(/[^A-Za-z0-9]/g, '').slice(0, 8);
  return `gfx_${name}_${short}${loop ? '_loop' : ''}`;
}

/**
 * The artAssets entry of a hook's effect. `worldWidth` = the first frame's NOMINAL width (its pixels
 * over its density) at the rig's pixels-per-unit, so an effect keeps its size against the parts
 * however fine it was baked.
 */
export function fxArtAsset(entry, loop, pixelsPerUnit) {
  const frames = fxFrames(entry, loop);
  if (!frames) return null;
  const folder = laneFolder(frames.lane.name);
  return {
    kind: loop ? 'fxLoop' : 'fx',
    folder: `Art/${entry.id}`,
    libraryId: entry.id,
    entryName: entry.enemyName,
    states: { [folder]: { folder, fps: frames.fps, loop: !!loop } },
    worldWidth: Math.round((frames.boxes[0].w / fxDensity(entry) / pixelsPerUnit) * 1e4) / 1e4,
  };
}
