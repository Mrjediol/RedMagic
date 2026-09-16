// modules/enemy-bundle.js
// -----------------------------------------------------------------------------
// Builds the combined "sprites + config" bundle: the exact same manifest.json + per-animation PNG
// folders `populateSpriteZip` already produces for a plain sprite export, PLUS `enemy-config.json`
// at the zip root. One function so both the Enemy Creator tab (live draft) and the Biblioteca tab
// (a previously-authored, persisted config) build byte-identical structure from the same source —
// see docs/schemas/enemy-config.schema.json and Assets/Scripts/Pipeline/Editor/ConfigImport/
// CombinedBundleImporter.cs on the Unity side, which is the reader for this exact shape.

import { loadImageFromDataURL } from './enemy-library.js';
import { populateSpriteZip } from './export-manifest.js';

/**
 * @param {object} args
 * @param {string} args.enemyName
 * @param {object} args.sprite - a library entry's `sprite` block: { lanes, boxes, sourceDataURL, width, height }.
 * @param {object} args.configObj - the EnemyConfig export object (modules/enemy-export.js's buildExportObject).
 * @returns {Promise<Blob>}
 */
export async function buildCombinedBundle({ enemyName, sprite, configObj }) {
  const img = await loadImageFromDataURL(sprite.sourceDataURL);
  const canvas = document.createElement('canvas');
  canvas.width = sprite.width;
  canvas.height = sprite.height;
  canvas.getContext('2d').drawImage(img, 0, 0);

  const zip = new window.JSZip();
  const manifest = populateSpriteZip(zip, { enemyName, lanes: sprite.lanes, boxes: sprite.boxes, source: canvas });
  zip.file('manifest.json', JSON.stringify(manifest, null, 2));
  zip.file('enemy-config.json', JSON.stringify(configObj, null, 2));

  return zip.generateAsync({ type: 'blob' });
}
