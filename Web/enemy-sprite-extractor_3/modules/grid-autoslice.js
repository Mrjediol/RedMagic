// modules/grid-autoslice.js
// -----------------------------------------------------------------------------
// NEW module — didn't exist in the old tool. Alternative to connected-component
// detection (sprite-detection.js) for sheets that are already a clean, regular
// grid: instead of measuring alpha blobs, it just divides the sheet into
// rows×cols equal cells. Coexists with detection mode as a separate path app.js
// switches between — it doesn't touch or replace anything in sprite-detection.js.

/**
 * Slices a `width`×`height` sheet into an exact `rows`×`cols` grid, covering
 * the full sheet with no gaps or overlaps (cell edges are rounded independently
 * so the last row/column absorbs any remainder instead of leaving a sliver).
 *
 * @returns {{
 *   boxes: Array<{x:number,y:number,w:number,h:number,assignedLane:null}>,
 *   rowIndices: number[][],  // rowIndices[r] = box indices for row r, left→right
 * }}
 */
export function buildGridBoxes(width, height, rows, cols) {
  rows = Math.max(1, Math.floor(rows));
  cols = Math.max(1, Math.floor(cols));

  const cellW = width / cols;
  const cellH = height / rows;

  const boxes = [];
  const rowIndices = [];

  for (let r = 0; r < rows; r++) {
    const y = Math.round(r * cellH);
    const y2 = Math.round((r + 1) * cellH);
    const rowBoxIdx = [];

    for (let c = 0; c < cols; c++) {
      const x = Math.round(c * cellW);
      const x2 = Math.round((c + 1) * cellW);
      boxes.push({ x, y, w: x2 - x, h: y2 - y, assignedLane: null });
      rowBoxIdx.push(boxes.length - 1);
    }

    rowIndices.push(rowBoxIdx);
  }

  return { boxes, rowIndices };
}
