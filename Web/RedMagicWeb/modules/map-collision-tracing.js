// modules/map-collision-tracing.js
// -----------------------------------------------------------------------------
// Per-piece collision line tracing, ported straight from MapTracer.html's `line`/`commitLine`/
// keydown handlers. Operates on ONE asset's `lines` array (modules/map-assets.js) in that piece's
// own local pixel space — never map/scene space. A traced line only becomes scene-space when
// modules/map-export.js's `sceneLines()` bakes each point as `point*scale + instance.x/y` at
// export time, so this module never needs to know about instances at all.

/** Starts a new in-progress line of the asset's own type ('border'/'platform'). Click adds points. */
export function startLine(type) {
  return { points: [], type };
}

export function addPoint(line, pos) {
  line.points.push(pos);
}

/** Removes the last point; returns false if the line is now empty (caller should discard it). */
export function undoPoint(line) {
  line.points.pop();
  return line.points.length > 0;
}

/**
 * Commits `line` onto `asset.lines` if it has >=2 points. Returns `{ok, message}` — mirrors the
 * original tool's status messages so app.js can just display `message`.
 */
export function commitLine(asset, line) {
  if (!line) return { ok: false, message: '' };
  if (line.points.length >= 2) {
    asset.lines.push(line);
    return { ok: true, message: 'Línea guardada.' };
  }
  return { ok: false, message: 'Una línea necesita al menos dos puntos.' };
}

export function deleteLine(asset, index) {
  asset.lines.splice(index, 1);
}
