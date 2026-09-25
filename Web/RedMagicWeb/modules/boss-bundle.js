// modules/boss-bundle.js
// -----------------------------------------------------------------------------
// The "⬇ Exportar jefe completo (.zip)" bundle — the exact layout BossBundleImporter.cs reads:
//
//   boss-config.json                         (modules/boss-export.js)
//   Body/<Animation>/frame_000.png ...        the body entry's lanes
//   Art/<libraryId>/<State>/frame_000.png ... every library entry an art slot uses
//
// Unity copies Body/ and Art/ into Assets/Art/Bosses/<slug>/Source/ and the config's relative
// folders ("Body", "Art/<libraryId>") resolve there.

import { loadImageFromDataURL } from './enemy-library.js';
import { cropToDataURL } from './export-manifest.js';
import { framedLanes, laneFolder } from './boss-export.js';

async function sourceCanvas(sprite) {
  const img = await loadImageFromDataURL(sprite.sourceDataURL);
  const canvas = document.createElement('canvas');
  canvas.width = sprite.width;
  canvas.height = sprite.height;
  canvas.getContext('2d').drawImage(img, 0, 0);
  return canvas;
}

async function writeLanes(folder, entry) {
  const source = await sourceCanvas(entry.sprite);
  framedLanes(entry).forEach((lane) => {
    const dir = folder.folder(laneFolder(lane.name));
    lane.frameBoxIndices.forEach((boxIdx, i) => {
      const base64 = cropToDataURL(source, entry.sprite.boxes[boxIdx]).split(',')[1];
      dir.file(`frame_${String(i).padStart(3, '0')}.png`, base64, { base64: true });
    });
  });
}

/**
 * @param {object} doc - buildBossExport(...).doc
 * @param {Map<string, object>} entriesById - library records by id
 * @returns {Promise<Blob>}
 */
export async function buildBossBundle(doc, entriesById) {
  const zip = new window.JSZip();
  zip.file('boss-config.json', JSON.stringify(doc, null, 2));

  if (doc.body?.libraryId) {
    const entry = entriesById.get(doc.body.libraryId);
    if (entry?.sprite) await writeLanes(zip.folder('Body'), entry);
  }

  const written = new Set();
  for (const asset of Object.values(doc.artAssets || {})) {
    if (written.has(asset.libraryId)) continue;
    written.add(asset.libraryId);
    const entry = entriesById.get(asset.libraryId);
    if (entry?.sprite) await writeLanes(zip.folder('Art').folder(asset.libraryId), entry);
  }

  return zip.generateAsync({ type: 'blob' });
}
