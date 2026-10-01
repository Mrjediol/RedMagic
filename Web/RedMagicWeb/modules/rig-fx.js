// modules/rig-fx.js
// -----------------------------------------------------------------------------
// EFFECTS HOOKED TO A GESTURE (RigFx in ProceduralRig.cs), independent of any DOM.
//
// A gesture has three phases — `windup` (lasts the attack's telegraph), `strike` (strikeTime; zero
// when the gesture has no strike pose) and `recover` — and each can carry effects:
//
//   { fx: <library id>, phase, at: 0..1, anchor: 'part' | 'body' | 'muzzle' | 'feet', part: <uid>,
//     offset: {x, y}, scale, followsPart, loop }
//
// `at` is the moment inside the phase (0 = its start, 1 = its end). `offset` is in the anchor's own
// local space (units). `loop` = it repeats until the phase ends; otherwise it plays once.
//
// The effect itself is a library entry (kind fx or projectile, usually baked in ✨ VFX Lab). Which
// of its lanes plays is decided here once, for the preview AND the export, so both agree:
// pickLane(). In Unity it becomes a pooled prefab built by BossArtBuilder from the `artAssets`
// block this module writes (kind `fx` = one shot, `fxLoop` = loops until the driver stops it).
//
// Timing mirrors RigDriverCode.GestureRoutine: keep both in step.

import { framedLanes, laneFolder } from './boss-export.js';

export const FX_PHASES = ['windup', 'strike', 'recover'];
export const FX_PHASE_LABELS = { windup: 'Carga', strike: 'Golpe', recover: 'Vuelta' };
export const FX_ANCHORS = ['part', 'body', 'muzzle', 'feet'];

/** A new hook with RigFx's defaults. */
export function createFx(libraryId, phase, anchor = 'body', partUid = null) {
  return { fx: libraryId, phase, at: 0, anchor, part: anchor === 'part' ? partUid : null,
    offset: { x: 0, y: 0 }, scale: 1, followsPart: true, loop: false };
}

/** The hooks of a gesture (documents saved before effects existed have none). */
export function fxList(gesture) {
  return gesture.fx || (gesture.fx = []);
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

/** Frames of the lane a hook plays: `{ lane, boxes, fps, duration }`, or null (no art). */
export function fxFrames(entry, loop) {
  const lane = pickLane(entry, loop);
  if (!lane) return null;
  const fps = Math.max(0.1, lane.fps || 10);
  const boxes = lane.frameBoxIndices.map((i) => entry.sprite.boxes[i]).filter(Boolean);
  if (!boxes.length) return null;
  return { lane, boxes, fps, duration: boxes.length / fps };
}

// ============================================================ timing (RigDriverCode.GestureRoutine)

/** Start and length (seconds) of each phase for a telegraph of `telegraph` seconds. */
export function phaseTimes(gesture, telegraph) {
  const windup = Math.max(0.01, telegraph);
  const strike = gesture.strike?.length ? Math.max(0.01, gesture.strikeTime) : 0;
  const recover = Math.max(0.01, gesture.recover);
  return {
    windup: { start: 0, length: windup },
    strike: { start: windup, length: strike },
    recover: { start: windup + strike, length: recover },
  };
}

/**
 * The hooks alive `t` seconds into the gesture, with the frame each one shows.
 * @param {(fx: object) => ({boxes: object[], fps: number, duration: number} | null)} framesOf
 * @returns {Array<{fx: object, index: number, frame: number}>}
 */
export function activeFx(gesture, t, telegraph, framesOf) {
  const times = phaseTimes(gesture, telegraph);
  const out = [];
  (gesture.fx || []).forEach((fx, index) => {
    const phase = times[fx.phase];
    const frames = framesOf(fx);
    if (!phase || !frames) return;
    const start = phase.start + Math.min(1, Math.max(0, fx.at)) * phase.length;
    const age = t - start;
    if (age < 0) return;
    if (fx.loop) {
      if (t >= phase.start + phase.length) return;       // a loop dies with its phase
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
 * The artAssets entry of a hook's effect. `worldWidth` = the first frame's width at the rig's
 * pixels-per-unit, so an effect drawn next to the parts keeps its size against them.
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
    worldWidth: Math.round((frames.boxes[0].w / pixelsPerUnit) * 1e4) / 1e4,
  };
}
