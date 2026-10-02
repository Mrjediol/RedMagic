// modules/rig-gestures.js
// -----------------------------------------------------------------------------
// The rig composer's GESTURES, independent of any DOM: the data of a named gesture (RigGesture in
// ProceduralRig.cs), how its poses are laid over the body, and how it moves in time.
//
// A gesture is a TIMELINE: any number of steps the body goes through in order, then back to rest.
//
//   { name, recover,
//     steps:    [{ name, duration, ease: 'inout'|'out'|'in'|'linear', stretch, shake, aim, poses: [pose] }],
//     releases: [{ step, at: 0..1, point: <part uid> | null (= the muzzle), offset: {x, y} }],
//     fx:       [hook]   (rig-fx.js) }
//
// A step's pose is measured from REST (a part not listed returns to rest) — a list of offsets:
//
//   { part: <part uid> | 'Visual', move: {x, y}, rotate: degrees, scale: {x, y} }
//
// `move` is in the part's parent space (units), `rotate` adds to its rotation, `scale` is relative
// (0.2 = 20% bigger). 'Visual' is the whole body. Only rig parts can be posed: the driver recomposes
// those every frame, anything else would drift.
//
// `aim` on a step = it ends with the aim part's barrel (pivot → muzzle) on the attack's target, wherever
// the poses left the part: it overrides the part's own rotation in the pose.
//
// `stretch` on a step = whoever launches the gesture gives its duration (the attack's telegraph): the
// stretch steps share what is left for the FIRST release to land exactly at that time. A release is
// an instant the attack LEAVES, and from where; none written = once, at the end of the last step.
//
// The old shape (two poses `windup` / `strike` + `strikeTime` / `shake`, effects by phase) is read
// and converted by `normalizeGesture` — the same conversion as RigGesture.OnAfterDeserialize.
//
// The formulas copy ProceduralRig.cs (RigGesture.Durations / EffectiveReleases) and RigDriverCode.cs
// (GestureRoutine, SettleRoutine, ApplyPose): what the preview plays is what Unity plays.

export const BODY = 'Visual';

export const EASES = ['inout', 'out', 'in', 'linear'];
export const EASE_LABELS = { inout: 'Suave (arranca y frena)', out: 'Latigazo (sale de golpe)', in: 'Impulso (acaba rápido)', linear: 'Constante' };

const zero = () => ({ x: 0, y: 0 });
const r3 = (v) => Math.round(v * 1000) / 1000;
const clamp01 = (x) => Math.min(1, Math.max(0, x));
const MIN = 0.01;

export const easeInOut = (x) => { const k = clamp01(x); return k * k * (3 - 2 * k); };
export const easeOutCubic = (x) => { const k = 1 - clamp01(x); return 1 - k * k * k; };

/** RigDriverCode.Ease. */
export function ease(kind, x) {
  const k = clamp01(x);
  if (kind === 'out') return easeOutCubic(k);
  if (kind === 'in') return k * k * k;
  if (kind === 'linear') return k;
  return easeInOut(k);
}

// ============================================================ data

/** A name no other gesture of the document uses. */
export function uniqueGestureName(state, wanted, except = null) {
  const base = (wanted || 'Gesto').trim() || 'Gesto';
  let name = base, n = 2;
  while ((state.gestures || []).some((g) => g !== except && g.name === name)) name = `${base}${n++}`;
  return name;
}

/** A new step with RigGestureStep's defaults. */
export function createStep(name = '', over = {}) {
  return { name, duration: 0.3, ease: 'inout', stretch: false, shake: 0, aim: false, poses: [], ...over };
}

/** A new release with RigRelease's defaults: the end of `step`, from the muzzle. */
export function createRelease(step = 0, at = 1) {
  return { step, at, point: null, offset: zero() };
}

/** A new gesture: one step that lasts the attack's telegraph, and the attack leaves at its end. */
export function createGesture(state, name = 'Gesto') {
  return { name: uniqueGestureName(state, name), recover: 0.45,
    steps: [createStep('Carga', { stretch: true })], releases: [createRelease(0, 1)], fx: [] };
}

export function duplicateGesture(state, gesture) {
  const copy = JSON.parse(JSON.stringify(gesture));
  copy.name = uniqueGestureName(state, `${gesture.name}2`);
  return copy;
}

/**
 * Any saved gesture → the timeline shape, in place. The classic one (windup / strike / strikeTime /
 * shake, effects by phase) becomes its two steps — RigGesture.SetClassic: the windup lasts the
 * telegraph, the attack leaves at its end, the strike arrives in `strikeTime`; its effects keep
 * turning with their anchor, as they were placed. Anything missing gets its default.
 * @returns {boolean} true when a classic gesture WITHOUT a strike was converted (one step): its
 *          sprite windows were written on the windup / strike / recover scale (see `convertWindow`).
 */
export function normalizeGesture(g) {
  let single = false;
  if (!Array.isArray(g.steps)) {
    const hasStrike = (g.strike || []).length > 0;
    g.steps = [createStep('Carga', { stretch: true, shake: g.shake || 0, poses: g.windup || [] })];
    if (hasStrike) g.steps.push(createStep('Golpe', { duration: Math.max(MIN, g.strikeTime ?? 0.08), ease: 'out', poses: g.strike }));
    g.releases = [createRelease(0, 1)];
    (g.fx || []).forEach((f) => {
      if (f.phase === undefined) return;
      if (f.phase === 'windup') f.step = 0;
      else if (f.phase === 'strike') { f.step = hasStrike ? 1 : 0; if (!hasStrike) f.at = 1; }
      else f.step = g.steps.length;
      f.facing = f.facing || 'anchor';
      delete f.phase;
    });
    ['windup', 'strike', 'strikeTime', 'shake'].forEach((k) => delete g[k]);
    single = !hasStrike;
  }
  g.recover = g.recover > 0 ? g.recover : 0.45;
  g.steps = g.steps.map((s) => ({ ...createStep(), ...s, poses: s.poses || [] }));
  g.releases = (g.releases || []).map((r) => ({ ...createRelease(), ...r, point: r.point || null, offset: { ...zero(), ...(r.offset || {}) } }));
  g.fx = g.fx || [];
  return single;
}

/**
 * A sprite window bound written for a classic gesture with no strike (0–1 windup, 1–2 strike,
 * 2–3 recover) on the scale of its single step (0–1 the step, 1–2 the recover) —
 * ProceduralRig.FixConvertedWindows.
 */
export const convertWindow = (v) => (v >= 2 ? v - 1 : v > 1 ? 1 : v);

/** The pose of `target` (part uid or BODY) in step `stepIndex`; `create` adds it. */
export function poseOf(gesture, stepIndex, target, create = false) {
  const step = gesture.steps[stepIndex];
  if (!step) return null;
  let pose = step.poses.find((p) => p.part === target);
  if (!pose && create) {
    pose = { part: target, move: zero(), rotate: 0, scale: zero() };
    step.poses.push(pose);
  }
  return pose || null;
}

/** Takes `target` out of one step's pose. Returns how many poses went. */
export function removePose(gesture, stepIndex, target) {
  const step = gesture.steps[stepIndex];
  if (!step) return 0;
  const before = step.poses.length;
  step.poses = step.poses.filter((p) => p.part !== target);
  return before - step.poses.length;
}

const isRest = (p) => !p.move?.x && !p.move?.y && !p.rotate && !p.scale?.x && !p.scale?.y;

/** Drops the poses that do nothing (a part that was clicked but never moved). */
export function pruneGesture(gesture) {
  gesture.steps.forEach((s) => { s.poses = s.poses.filter((p) => !isRest(p)); });
}

/** A deleted part takes its poses with it; what hung from it goes to the body / the muzzle. */
export function dropPartFromGestures(state, uid) {
  (state.gestures || []).forEach((g) => {
    g.steps.forEach((s, i) => removePose(g, i, uid));
    g.releases.forEach((r) => { if (r.point === uid) { r.point = null; r.offset = zero(); } });
    (g.fx || []).forEach((f) => { if (f.anchor === 'part' && f.part === uid) { f.anchor = 'body'; f.part = null; } });
  });
}

export function roundPose(pose) {
  pose.move = { x: r3(pose.move.x), y: r3(pose.move.y) };
  pose.rotate = Math.round(pose.rotate * 10) / 10;
  pose.scale = { x: r3(pose.scale.x), y: r3(pose.scale.y) };
}

// ============================================================ editing the list of steps
//
// Releases and effects name their step by index, and an effect on step number "how many steps" sits
// on the way back to rest: every change to the list keeps them on the step they were on.

/** Adds a step after `after` (-1 = first). Returns its index. */
export function insertStep(gesture, after, step = createStep()) {
  const at = Math.min(gesture.steps.length, Math.max(0, after + 1));
  const count = gesture.steps.length;
  gesture.steps.splice(at, 0, step);
  const shift = (i) => (i >= at ? i + 1 : i);
  gesture.releases.forEach((r) => { r.step = shift(r.step); });
  (gesture.fx || []).forEach((f) => {
    const recover = f.step >= count;
    f.step = recover ? count + 1 : shift(f.step);
    if (f.untilStep >= 0) f.untilStep = f.untilStep >= count ? count + 1 : shift(f.untilStep);
  });
  return at;
}

/** Removes a step (never the last one left). Its releases and effects move to the step before. */
export function removeStep(gesture, index) {
  const count = gesture.steps.length;
  if (count <= 1 || !gesture.steps[index]) return false;
  gesture.steps.splice(index, 1);
  // What was on the removed step lands at the end of the one before (the start of the next, if it was the first).
  const shift = (i) => (i >= count ? count - 1 : i > index ? i - 1 : i === index ? Math.max(0, index - 1) : i);
  const land = (item, key) => {
    if (item[key] === index) item.at = index > 0 ? 1 : 0;
    item[key] = shift(item[key]);
  };
  gesture.releases.forEach((r) => { r.step = Math.min(r.step, count - 1); land(r, 'step'); });
  (gesture.fx || []).forEach((f) => {
    land(f, 'step');
    if (f.untilStep >= 0) f.untilStep = Math.max(f.step, shift(f.untilStep));
  });
  return true;
}

/** Moves a step to another place in the order; what was on each step travels with it. */
export function moveStep(gesture, from, to) {
  const count = gesture.steps.length;
  if (from === to || !gesture.steps[from] || to < 0 || to >= count) return false;
  const order = gesture.steps.map((_, i) => i);
  order.splice(to, 0, order.splice(from, 1)[0]);
  const next = (i) => (i >= count ? i : order.indexOf(i));
  gesture.steps = order.map((i) => gesture.steps[i]);
  gesture.releases.forEach((r) => { r.step = next(Math.min(r.step, count - 1)); });
  (gesture.fx || []).forEach((f) => {
    f.step = next(f.step);
    if (f.untilStep >= 0) f.untilStep = Math.max(f.step, next(f.untilStep));
  });
  return true;
}

// ============================================================ time (RigGesture.Durations, GestureRoutine)

/** RigGesture.EffectiveReleases: the written ones, else one at the end of the last step. */
export function effectiveReleases(gesture) {
  if (gesture.releases?.length) return gesture.releases;
  return gesture.steps?.length ? [createRelease(gesture.steps.length - 1, 1)] : [];
}

function firstRelease(gesture) {
  let first = null;
  effectiveReleases(gesture).forEach((r) => {
    if (!first || r.step < first.step || (r.step === first.step && r.at < first.at)) first = r;
  });
  return first;
}

/** Seconds of each step for a gesture launched with `lead` seconds of warning — RigGesture.Durations. */
export function stepDurations(gesture, lead) {
  const steps = gesture.steps || [];
  const first = firstRelease(gesture);
  const lastStretch = first ? Math.min(first.step, steps.length - 1) : steps.length - 1;

  let fixedTime = 0, stretched = 0;
  for (let i = 0; i <= lastStretch; i++) {
    const s = steps[i];
    const releaseStep = first && i === first.step;
    if (s.stretch) stretched++;
    else fixedTime += Math.max(MIN, s.duration) * (releaseStep ? first.at : 1);
  }

  const share = stretched > 0 ? Math.max(MIN, (lead - fixedTime) / stretched) : 0;
  return steps.map((s, i) => {
    if (!s.stretch || i > lastStretch) return Math.max(MIN, s.duration);
    // The step the release falls on: what stretches is the stretch UP TO it.
    const releaseStep = first && i === first.step;
    return releaseStep && first.at > 0.01 ? share / first.at : share;
  });
}

/**
 * The whole gesture laid on a clock, launched with `lead` seconds of warning:
 * `{ count, durations[], starts[], recoverStart, recover, total, releases: [{ release, index, step, time }] }`.
 * The stretch after the last step (segment number `count`) is the way back to rest.
 */
export function timeline(gesture, lead) {
  const count = (gesture.steps || []).length;
  const durations = stepDurations(gesture, Math.max(MIN, lead));
  const starts = [];
  let t = 0;
  durations.forEach((d) => { starts.push(t); t += d; });
  // No steps: nothing to go through, it waits the warning and releases.
  const recoverStart = count ? t : Math.max(MIN, lead);
  const recover = Math.max(MIN, gesture.recover);
  const written = gesture.releases?.length ? gesture.releases : null;
  const releases = effectiveReleases(gesture).map((release, index) => {
    const step = Math.min(release.step, count - 1);
    return { release, index: written ? index : -1, step, time: starts[step] + clamp01(release.at) * durations[step] };
  });
  if (!count) releases.push({ release: createRelease(0, 1), index: -1, step: 0, time: recoverStart });
  return { count, durations, starts, recoverStart, recover, total: recoverStart + recover, releases };
}

export const gestureDuration = (gesture, lead) => timeline(gesture, lead).total;

/** Start and length of segment `step` (a step, or `count` = the way back to rest). */
export function segment(tl, step) {
  const s = Math.min(Math.max(0, step), tl.count);
  return s >= tl.count ? { start: tl.recoverStart, length: tl.recover } : { start: tl.starts[s], length: tl.durations[s] };
}

/** The clock time of an instant written as (step, at). */
export function timeOf(tl, step, at) {
  const seg = segment(tl, step);
  return seg.start + clamp01(at) * seg.length;
}

/** The (step, at) a clock time falls on — the inverse of `timeOf`. */
export function instantAt(tl, t) {
  const time = Math.min(tl.total, Math.max(0, t));
  for (let s = 0; s < tl.count; s++) {
    if (time < tl.starts[s] + tl.durations[s] || (s === tl.count - 1 && time <= tl.recoverStart))
      return { step: s, at: clamp01((time - tl.starts[s]) / tl.durations[s]) };
  }
  return { step: tl.count, at: clamp01((time - tl.recoverStart) / tl.recover) };
}

/**
 * Where the body is `t` seconds into the gesture — GestureRoutine + SettleRoutine. One weight per
 * step: each step blends from wherever the body was to "only this pose"; the way back blends
 * everything to rest.
 * @returns {{weights: number[], shake: number, moment: number, settling: boolean, done: boolean}}
 *          `moment` = step index + progress (what a gesture sprite's window is compared with).
 */
export function gestureAt(gesture, t, lead, tl = timeline(gesture, lead)) {
  const weights = new Array(tl.count).fill(0);
  if (t >= tl.recoverStart || !tl.count) {
    const linear = clamp01((t - tl.recoverStart) / tl.recover);
    if (tl.count && t >= tl.recoverStart) weights[tl.count - 1] = 1 - easeInOut(linear);
    const waiting = !tl.count && t < tl.recoverStart;
    return { weights, shake: 0, moment: waiting ? 0 : tl.count + linear, settling: !waiting, done: t >= tl.total };
  }

  let s = 0;
  while (s < tl.count - 1 && t >= tl.starts[s] + tl.durations[s]) s++;
  const step = gesture.steps[s];
  const progress = clamp01((t - tl.starts[s]) / tl.durations[s]);
  const k = ease(step.ease, progress);
  if (s > 0) weights[s - 1] = 1 - k;
  weights[s] = k;
  return { weights, shake: progress > 0.7 ? step.shake || 0 : 0, moment: s + progress, settling: false, done: false };
}

// ============================================================ poses over the body (RigDriverCode.ApplyPose)

const shortAngle = (a) => ((((a % 360) + 540) % 360) - 180);

/**
 * Lays a pose list, at `weight`, over the composed body — facing right.
 *
 * @param {Map<string, {position, rotation, scale}>} poses - the rig parts' composed poses (mutated).
 * @param {{position, rotation, scale}} visual - the Visual root's composed pose (mutated).
 * @param {boolean} shortTurns - turns go the short way (on the way back to rest a step that left a
 *        part at 360° is already at rest to the eye and must not unwind).
 */
export function applyPoses(list, weight, poses, visual, shortTurns = false) {
  if (!list || Math.abs(weight) <= 0.0001) return;
  list.forEach((pose) => {
    const t = pose.part === BODY ? visual : poses.get(pose.part);
    if (!t) return;   // not a rig part (or gone): the driver skips it too
    t.position = { x: t.position.x + pose.move.x * weight, y: t.position.y + pose.move.y * weight };
    t.rotation += (shortTurns ? shortAngle(pose.rotate) : pose.rotate) * weight;
    t.scale = { x: t.scale.x * (1 + pose.scale.x * weight), y: t.scale.y * (1 + pose.scale.y * weight) };
  });
}

/** How much the gesture aims right now: the weights of its `aim` steps (RigDriverCode.GestureAimWeight). */
export function aimWeight(gesture, weights) {
  return clamp01(gesture.steps.reduce((sum, step, i) => sum + (step.aim ? weights[i] || 0 : 0), 0));
}

/** Every step's pose at its weight: the blend the body is in (RigDriverCode.ApplySteps). */
export function applySteps(gesture, weights, poses, visual, shortTurns = false) {
  gesture.steps.forEach((step, i) => applyPoses(step.poses, weights[i] || 0, poses, visual, shortTurns));
}
