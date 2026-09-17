// modules/map-instances.js
// -----------------------------------------------------------------------------
// Map Tracer's scene: placed instances of piece-library assets — `{id, assetId, x, y, scaleX,
// scaleY}`, position in map-canvas pixel space (top-left, unscaled — same convention as the
// Sprites tab's `boxes`). Click-to-place, hit-testing, multi-select drag-move and the new
// Unity-style corner-resize all live here as pure functions; app.js only wires mouse events.
//
// Resize (`beginResize`/`applyResize`) replaces MapTracer.html's ±10% scale buttons. It handles a
// single selected instance and a multi-instance group with the SAME math: a group is just the
// n=1 case of "every selected instance keeps its position/size relative to the fixed anchor
// corner, scaled by the same factor" — no separate single-instance code path needed.

function uid() {
  return Math.random().toString(36).slice(2, 10);
}

export function createInstance(assetId, x, y) {
  return { id: uid(), assetId, x, y, scaleX: 1, scaleY: 1 };
}

export function findInstance(instances, id) {
  return instances.find((i) => i.id === id);
}

/** Bounds of one placed instance in map-canvas pixel space. */
export function getInstanceBounds(instance, asset) {
  const sx = instance.scaleX ?? 1, sy = instance.scaleY ?? 1;
  return { x: instance.x, y: instance.y, w: asset.width * sx, h: asset.height * sy };
}

/** Bounding box of every instance whose id is in `ids`. */
export function getGroupBounds(ids, instances, assetOf) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  ids.forEach((id) => {
    const inst = findInstance(instances, id);
    const asset = inst && assetOf(inst.assetId);
    if (!inst || !asset) return;
    const b = getInstanceBounds(inst, asset);
    minX = Math.min(minX, b.x); minY = Math.min(minY, b.y);
    maxX = Math.max(maxX, b.x + b.w); maxY = Math.max(maxY, b.y + b.h);
  });
  return { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
}

/** Topmost (last-drawn) instance whose bounds contain `pos`, or null. */
export function hitTestInstance(pos, instances, assetOf) {
  for (let i = instances.length - 1; i >= 0; i--) {
    const inst = instances[i];
    const asset = assetOf(inst.assetId);
    if (!asset) continue;
    const b = getInstanceBounds(inst, asset);
    if (pos.x >= b.x && pos.x <= b.x + b.w && pos.y >= b.y && pos.y <= b.y + b.h) return inst;
  }
  return null;
}

/** The 4 corner-resize handle keys ('tl'/'tr'/'bl'/'br') a point falls within, or null. */
export function hitTestResizeHandle(pos, bounds, handleScreenSize) {
  const corners = {
    tl: [bounds.x, bounds.y], tr: [bounds.x + bounds.w, bounds.y],
    bl: [bounds.x, bounds.y + bounds.h], br: [bounds.x + bounds.w, bounds.y + bounds.h],
  };
  for (const key in corners) {
    const [cx, cy] = corners[key];
    if (Math.abs(pos.x - cx) < handleScreenSize && Math.abs(pos.y - cy) < handleScreenSize) return key;
  }
  return null;
}

export function moveInstances(ids, instances, dx, dy, startPositions) {
  ids.forEach((id) => {
    const inst = findInstance(instances, id);
    const start = startPositions.get(id);
    if (inst && start) { inst.x = Math.round(start.x + dx); inst.y = Math.round(start.y + dy); }
  });
}

export function deleteInstances(instances, ids) {
  const idSet = ids instanceof Set ? ids : new Set(ids);
  const remaining = instances.filter((i) => !idSet.has(i.id));
  instances.length = 0; instances.push(...remaining);
}

const HANDLE_ANCHOR = { br: 'tl', bl: 'tr', tl: 'br', tr: 'bl' };

function cornerPoint(bounds, corner) {
  return {
    x: corner.includes('l') ? bounds.x : bounds.x + bounds.w,
    y: corner.includes('t') ? bounds.y : bounds.y + bounds.h,
  };
}

/**
 * Captures the state a corner-drag resize needs before the drag starts: the fixed anchor corner
 * (opposite the dragged handle) and, per selected instance, its bounds as an offset from that
 * anchor. Works identically for one instance (a 1-item group) or many.
 */
export function beginResize(ids, instances, assetOf, handle) {
  const groupBounds = getGroupBounds(ids, instances, assetOf);
  const anchor = cornerPoint(groupBounds, HANDLE_ANCHOR[handle]);
  const startCorner = cornerPoint(groupBounds, handle);
  const items = ids.map((id) => {
    const inst = findInstance(instances, id);
    const asset = assetOf(inst.assetId);
    const b = getInstanceBounds(inst, asset);
    return { inst, asset, offX: b.x - anchor.x, offY: b.y - anchor.y, w: b.w, h: b.h };
  });
  return {
    anchor,
    startW: Math.abs(startCorner.x - anchor.x) || 1,
    startH: Math.abs(startCorner.y - anchor.y) || 1,
    items,
  };
}

/** Applies the current mouse `pos` to a `beginResize` state, mutating every captured instance. */
export function applyResize(state, pos, proportional) {
  let factorX = Math.max(0.02, Math.abs(pos.x - state.anchor.x) / state.startW);
  let factorY = Math.max(0.02, Math.abs(pos.y - state.anchor.y) / state.startH);
  if (proportional) {
    const f = Math.abs(factorX - 1) > Math.abs(factorY - 1) ? factorX : factorY;
    factorX = factorY = f;
  }
  state.items.forEach(({ inst, asset, offX, offY, w, h }) => {
    inst.x = Math.round(state.anchor.x + offX * factorX);
    inst.y = Math.round(state.anchor.y + offY * factorY);
    inst.scaleX = (w * factorX) / asset.width;
    inst.scaleY = (h * factorY) / asset.height;
  });
}
