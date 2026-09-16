// modules/export-manifest.js
// -----------------------------------------------------------------------------
// Ported from the old exportBtn click handler + its cropToDataURL helper.
// Produces the exact same manifest.json shape and folder layout the existing
// Unity-side importer (Assets/Editor/EnemyImporter.cs) already reads — do not
// change field names or folder structure here without updating that importer
// too.
//
// cropToDataURL also lives here (not a separate image-utils module) because
// "turn a box region into pixels" is exactly what export needs anyway; app.js
// reuses it for the lane/unsorted-grid thumbnails so there's one crop
// implementation instead of two.

/**
 * Crops `box` out of `source` (an Image or a canvas) into a small canvas and
 * returns it. Kept separate from toDataURL below so callers that just need a
 * canvas (e.g. to draw into another canvas) don't pay for a base64 round-trip.
 */
export function cropBox(source, box) {
  const c = document.createElement('canvas');
  c.width = Math.max(1, box.w);
  c.height = Math.max(1, box.h);
  const cctx = c.getContext('2d');
  cctx.drawImage(source, box.x, box.y, box.w, box.h, 0, 0, box.w, box.h);
  return c;
}

export function cropToDataURL(source, box) {
  return cropBox(source, box).toDataURL();
}

/**
 * Builds the manifest object and zips it up alongside one PNG per frame,
 * exactly as the old tool did: `manifest.json` at the root, one folder per
 * animation lane (`<lane.name>/frame_000.png, ...`), projectile lanes nested
 * under `Projectiles/<lane.name>/...`.
 *
 * @param {object} args
 * @param {string} args.enemyName
 * @param {Array} args.lanes
 * @param {Array} args.boxes
 * @param {HTMLImageElement|HTMLCanvasElement} args.source - the (possibly bg-removed) image to crop from.
 * @param {typeof import('jszip')} args.JSZip - the JSZip constructor (loaded globally via CDN in index.html).
 * @returns {Promise<{blob: Blob, manifest: object}>}
 */
export async function buildExportZip({ enemyName, lanes, boxes, source, JSZip }) {
  const animLanes = lanes.filter((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
  const projLanes = lanes.filter((l) => l.type === 'projectile' && l.frameBoxIndices.length > 0);

  const zip = new JSZip();
  const manifest = { enemyName, animations: [], projectiles: [] };

  animLanes.forEach((lane) => {
    const folder = zip.folder(lane.name);
    lane.frameBoxIndices.forEach((boxIdx, i) => {
      const base64 = cropToDataURL(source, boxes[boxIdx]).split(',')[1];
      folder.file(`frame_${String(i).padStart(3, '0')}.png`, base64, { base64: true });
    });

    let projectileField = null;
    if (lane.projectileLink && lane.projectileLink.projId) {
      const linkedProj = lanes.find((l) => l.id === lane.projectileLink.projId);
      if (linkedProj && linkedProj.frameBoxIndices.length > 0) {
        const maxFrame = lane.frameBoxIndices.length - 1;
        const spawnFrame = Math.min(Math.max(0, lane.projectileLink.spawnFrame), maxFrame);
        projectileField = { name: linkedProj.name, spawnFrame };
      }
    }

    manifest.animations.push({
      name: lane.name,
      fps: lane.fps,
      loop: lane.loop,
      frameCount: lane.frameBoxIndices.length,
      projectile: projectileField,
    });
  });

  projLanes.forEach((lane) => {
    const folder = zip.folder('Projectiles').folder(lane.name);
    lane.frameBoxIndices.forEach((boxIdx, i) => {
      const base64 = cropToDataURL(source, boxes[boxIdx]).split(',')[1];
      folder.file(`frame_${String(i).padStart(3, '0')}.png`, base64, { base64: true });
    });
    manifest.projectiles.push({
      name: lane.name,
      fps: lane.fps,
      loop: lane.loop,
      frameCount: lane.frameBoxIndices.length,
    });
  });

  zip.file('manifest.json', JSON.stringify(manifest, null, 2));

  const blob = await zip.generateAsync({ type: 'blob' });
  return { blob, manifest };
}

/** Triggers a browser download of `blob` named `filename` — the a[download] click trick. */
export function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
