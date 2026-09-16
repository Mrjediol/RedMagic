// modules/animation-lanes.js
// -----------------------------------------------------------------------------
// Lane/frame assignment logic, ported from the old lanes array + its inline
// mutation code (addLaneBtn/addProjBtn handlers, buildLaneCard's frame
// left/right/remove buttons, the projectile-link toggle, deleteLane, and
// removeBoxes' index-shifting). Pure state mutation over the `lanes`/`boxes`
// arrays app.js owns — no DOM here. Rendering the lane cards is app.js's job
// (renderLanes()); this module only ever changes data.
//
// lane shape: { id, type:'animation'|'projectile', name, fps, loop,
//               frameBoxIndices:[boxIndex,...], projectileLink }
// projectileLink (animation lanes only): null | { projId, spawnFrame }

let _laneIdCounter = 0;

export function resetLaneIdCounter() {
  _laneIdCounter = 0;
}

export function createLane(lanes, type, name, opts = {}) {
  const lane = {
    id: `lane${_laneIdCounter++}`,
    type,
    name,
    fps: opts.fps ?? (type === 'projectile' ? 10 : 8),
    loop: opts.loop ?? true,
    frameBoxIndices: [],
    projectileLink: type === 'animation' ? null : undefined,
  };
  lanes.push(lane);
  return lane;
}

/** Unassigns every frame the lane owned, unlinks it from any animation that referenced it, and removes it. */
export function deleteLane(lanes, boxes, laneId) {
  const lane = lanes.find((l) => l.id === laneId);
  if (!lane) return;

  lane.frameBoxIndices.forEach((idx) => { if (boxes[idx]) boxes[idx].assignedLane = null; });

  if (lane.type === 'projectile') {
    lanes.forEach((l) => {
      if (l.type === 'animation' && l.projectileLink && l.projectileLink.projId === lane.id) {
        l.projectileLink = null;
      }
    });
  }

  const idx = lanes.indexOf(lane);
  if (idx >= 0) lanes.splice(idx, 1);
}

/**
 * Assigns `indices` (box indices, in the order the caller wants them appended)
 * to `laneId`, pulling each box out of whatever lane it was in before.
 */
export function assignBoxesToLane(lanes, boxes, laneId, indices) {
  const lane = lanes.find((l) => l.id === laneId);
  if (!lane) return null;

  indices.forEach((idx) => {
    const b = boxes[idx];
    if (!b) return;
    if (b.assignedLane) {
      const oldLane = lanes.find((l) => l.id === b.assignedLane);
      if (oldLane) oldLane.frameBoxIndices = oldLane.frameBoxIndices.filter((i) => i !== idx);
    }
    b.assignedLane = laneId;
    lane.frameBoxIndices.push(idx);
  });

  return lane;
}

/** Un-assigns one frame (by its position within the lane, not the global box index) back to "unsorted". */
export function removeFrameFromLane(lanes, boxes, laneId, orderIdx) {
  const lane = lanes.find((l) => l.id === laneId);
  if (!lane) return;
  const boxIdx = lane.frameBoxIndices[orderIdx];
  if (boxIdx === undefined) return;
  if (boxes[boxIdx]) boxes[boxIdx].assignedLane = null;
  lane.frameBoxIndices.splice(orderIdx, 1);
}

/** Swaps a frame with its left (-1) or right (+1) neighbour within the lane. */
export function moveFrame(lane, orderIdx, direction) {
  const j = orderIdx + direction;
  if (j < 0 || j >= lane.frameBoxIndices.length) return;
  const arr = lane.frameBoxIndices;
  [arr[orderIdx], arr[j]] = [arr[j], arr[orderIdx]];
}

/** Turns an animation lane's "fires a projectile" link on/off, defaulting to the first available projectile lane. */
export function setProjectileLink(lane, projLanes, enabled) {
  if (!enabled) { lane.projectileLink = null; return; }
  const firstProj = projLanes[0];
  lane.projectileLink = firstProj ? { projId: firstProj.id, spawnFrame: 0 } : { projId: null, spawnFrame: 0 };
}

/**
 * Removes multiple boxes at once (multi-select + Delete, or "Borrar
 * selección"), keeping every lane's frameBoxIndices and every remaining box's
 * assignedLane consistent as the boxes array's indices shift down. Must go
 * highest-index-first so earlier splices don't invalidate later indices.
 */
export function removeBoxes(boxes, lanes, indices) {
  const sortedDesc = [...new Set(indices)].sort((a, b) => b - a);

  sortedDesc.forEach((i) => {
    boxes.splice(i, 1);
    lanes.forEach((l) => {
      l.frameBoxIndices = l.frameBoxIndices
        .filter((idx) => idx !== i)
        .map((idx) => (idx > i ? idx - 1 : idx));
    });
  });
}
