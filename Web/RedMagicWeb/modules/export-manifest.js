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
 * Fills `zip` (an already-constructed JSZip instance) with the manifest's PNG frames — one folder
 * per animation lane (`<lane.name>/frame_000.png, ...`), projectile lanes nested under
 * `Projectiles/<lane.name>/...` — and returns the manifest object. Does NOT write `manifest.json`
 * into the zip and does NOT generate the blob: split out of `buildExportZip` below so a caller
 * that needs to add MORE files to the same zip (the combined sprites+config bundle, see
 * modules/enemy-bundle.js) can do so before finalizing, instead of unzipping and rezipping.
 *
 * @param {object} args
 * @param {string} args.enemyName
 * @param {string} [args.kind] - 'enemy' | 'projectile' | 'fx' (modules/entry-kinds.js). Emitted
 *   into the manifest so the Unity side can discriminate instead of assuming every bundle is an
 *   enemy. ADDITIVE: `enemyName` keeps its name and position, so an importer that predates this
 *   field (EnemyImporter.cs parses into a typed class and ignores unknown members) reads a
 *   kind-carrying manifest exactly as it read the old one.
 * @param {string} [args.libraryId] - the library record's id, when this sheet was exported from a
 *   saved entry. Emitted so Unity can find this entry's source folder BY ID instead of by folder
 *   name — what lets `FxPrefabBuilder.BuildOrGetProjectilePrefab(libraryId)` resolve a projectile
 *   an EnemyConfig only references by id. Absent for a never-saved sheet, which is fine: only the
 *   lazy projectile/VFX path needs it.
 * @param {Array} args.lanes
 * @param {Array} args.boxes
 * @param {HTMLImageElement|HTMLCanvasElement} args.source - the (possibly bg-removed) image to crop from.
 * @returns {object} manifest
 */
export function populateSpriteZip(zip, { enemyName, kind = 'enemy', libraryId = null, lanes, boxes, source }) {
  const animLanes = lanes.filter((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
  const projLanes = lanes.filter((l) => l.type === 'projectile' && l.frameBoxIndices.length > 0);

  const manifest = { enemyName, kind, animations: [], projectiles: [] };
  if (libraryId) manifest.libraryId = libraryId;

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

  return manifest;
}

/**
 * Convenience wrapper for the plain Sprites-tab export: builds a fresh zip, populates it, writes
 * `manifest.json`, and generates the final blob.
 *
 * @param {object} args - see populateSpriteZip, plus:
 * @param {typeof import('jszip')} args.JSZip - the JSZip constructor (loaded globally via CDN in index.html).
 * @returns {Promise<{blob: Blob, manifest: object}>}
 */
export async function buildExportZip({ enemyName, kind = 'enemy', libraryId = null, lanes, boxes, source, JSZip }) {
  const zip = new JSZip();
  const manifest = populateSpriteZip(zip, { enemyName, kind, libraryId, lanes, boxes, source });
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
