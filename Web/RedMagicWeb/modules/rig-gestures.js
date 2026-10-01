// modules/rig-gestures.js
// -----------------------------------------------------------------------------
// The 🦴 Rig Composer's GESTURES, independent of any DOM: the data of a named gesture (RigGesture in
// ProceduralRig.cs), how its poses are laid over the body, and how its weights move in time.
//
// A gesture is two poses — `windup` (reached with easing over the attack's telegraph) and `strike`
// (snapped to in `strikeTime` after release) — then back to rest over `recover`. A pose is a list of
// OFFSETS on top of the composed body, one per part:
//
//   { part: <part uid> | 'Visual', move: {x, y}, rotate: degrees, scale: {x, y} }
//
// `move` is in the part's parent space (units), `rotate` adds to its rotation, `scale` is relative
// (0.2 = 20% bigger). 'Visual' is the whole body. Only rig parts can be posed: the driver recomposes
// those every frame, anything else would drift.
//
// A gesture can also carry EFFECTS per phase (`fx`, see rig-fx.js): the charge particles on the
// weapon tip, the burst on release.
//
// The formulas copy RigDriverCode.cs (ApplyPoses, GestureRoutine, SettleRoutine): what the preview
// plays is what Unity plays.

export const BODY = 'Visual';

const zero = () => ({ x: 0, y: 0 });
const r3 = (v) => Math.round(v * 1000) / 1000;

export const easeInOut = (x) => { const k = Math.min(1, Math.max(0, x)); return k * k * (3 - 2 * k); };
export const easeOutCubic = (x) => { const k = 1 - Math.min(1, Math.max(0, x)); return 1 - k * k * k; };

/** A name no other gesture of the document uses. */
export function uniqueGestureName(state, wanted, except = null) {
  const base = (wanted || 'Gesto').trim() || 'Gesto';
  let name = base, n = 2;
  while ((state.gestures || []).some((g) => g !== except && g.name === name)) name = `${base}${n++}`;
  return name;
}

/** A new empty gesture, with RigGesture's own defaults. */
export function createGesture(state, name = 'Gesto') {
  return { name: uniqueGestureName(state, name), strikeTime: 0.08, recover: 0.45, shake: 1.5, windup: [], strike: [], fx: [] };
}

export function duplicateGesture(state, gesture) {
  const copy = JSON.parse(JSON.stringify(gesture));
  copy.name = uniqueGestureName(state, `${gesture.name}2`);
  return copy;
}

/** The pose of `target` (part uid or BODY) in a phase ('windup' | 'strike'); `create` adds it. */
export function poseOf(gesture, phase, target, create = false) {
  const list = gesture[phase] || (gesture[phase] = []);
  let pose = list.find((p) => p.part === target);
  if (!pose && create) {
    pose = { part: target, move: zero(), rotate: 0, scale: zero() };
    list.push(pose);
  }
  return pose || null;
}

export function removePose(gesture, phase, target) {
  gesture[phase] = (gesture[phase] || []).filter((p) => p.part !== target);
}

const isRest = (p) => !p.move?.x && !p.move?.y && !p.rotate && !p.scale?.x && !p.scale?.y;

/** Drops the poses that do nothing (a part that was clicked but never moved). */
export function pruneGesture(gesture) {
  ['windup', 'strike'].forEach((phase) => { gesture[phase] = (gesture[phase] || []).filter((p) => !isRest(p)); });
}

/** A deleted part takes its poses with it; an effect that hung from it goes to the body. */
export function dropPartFromGestures(state, uid) {
  (state.gestures || []).forEach((g) => {
    removePose(g, 'windup', uid);
    removePose(g, 'strike', uid);
    (g.fx || []).forEach((f) => { if (f.anchor === 'part' && f.part === uid) { f.anchor = 'body'; f.part = null; } });
  });
}

export function roundPose(pose) {
  pose.move = { x: r3(pose.move.x), y: r3(pose.move.y) };
  pose.rotate = Math.round(pose.rotate * 10) / 10;
  pose.scale = { x: r3(pose.scale.x), y: r3(pose.scale.y) };
}

/**
 * Lays a pose list, at `weight`, over the composed body — RigDriverCode.ApplyPoses, facing right.
 *
 * @param {Map<string, {position, rotation, scale}>} poses - the rig parts' composed poses (mutated).
 * @param {{position, rotation, scale}} visual - the Visual root's composed pose (mutated).
 */
export function applyPoses(list, weight, poses, visual) {
  if (!list || weight <= 0.0001) return;
  list.forEach((pose) => {
    const t = pose.part === BODY ? visual : poses.get(pose.part);
    if (!t) return;   // not a rig part (or gone): the driver skips it too
    t.position = { x: t.position.x + pose.move.x * weight, y: t.position.y + pose.move.y * weight };
    t.rotation += pose.rotate * weight;
    t.scale = { x: t.scale.x * (1 + pose.scale.x * weight), y: t.scale.y * (1 + pose.scale.y * weight) };
  });
}

/** How long the whole gesture lasts with a telegraph of `telegraph` seconds. */
export function gestureDuration(gesture, telegraph) {
  const strike = gesture.strike?.length ? Math.max(0.01, gesture.strikeTime) : 0;
  return Math.max(0.01, telegraph) + strike + Math.max(0.01, gesture.recover);
}

/**
 * The gesture's weights `t` seconds after it started — GestureRoutine + SettleRoutine.
 * @returns {{windup: number, strike: number, shake: number, released: boolean, done: boolean}}
 */
export function gestureWeights(gesture, t, telegraph) {
  const T = Math.max(0.01, telegraph);
  if (t < T) {
    const k = t / T;
    return { windup: easeInOut(k), strike: 0, shake: k > 0.7 ? gesture.shake || 0 : 0, released: false, done: false };
  }

  const recover = Math.max(0.01, gesture.recover);
  let after = t - T;

  // No strike pose (a held charge): the windup settles over 'recover'.
  if (!gesture.strike?.length) {
    return { windup: 1 - easeInOut(after / recover), strike: 0, shake: 0, released: true, done: after >= recover };
  }

  const strikeTime = Math.max(0.01, gesture.strikeTime);
  if (after < strikeTime) {
    const k = easeOutCubic(after / strikeTime);
    return { windup: 1 - k, strike: k, shake: 0, released: true, done: false };
  }

  after -= strikeTime;
  return { windup: 0, strike: 1 - easeInOut(after / recover), shake: 0, released: true, done: after >= recover };
}
