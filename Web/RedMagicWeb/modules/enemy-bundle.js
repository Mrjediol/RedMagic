// modules/enemy-bundle.js
// -----------------------------------------------------------------------------
// Builds the combined "sprites + config" bundle: the exact same manifest.json + per-animation PNG
// folders `populateSpriteZip` already produces for a plain sprite export, PLUS `enemy-config.json`
// at the zip root. One function so both the Enemy Creator tab (live draft) and the Biblioteca tab
// (a previously-authored, persisted config) build byte-identical structure from the same source —
// see docs/schemas/enemy-config.schema.json and Assets/Scripts/Pipeline/Editor/ConfigImport/
// CombinedBundleImporter.cs on the Unity side, which is the reader for this exact shape.

import { loadImageFromDataURL, getEnemy } from './enemy-library.js';
import { populateSpriteZip } from './export-manifest.js';
import { kindOf } from './entry-kinds.js';

/** Decodes a stored sheet snapshot back into a canvas `populateSpriteZip` can crop from. */
async function sourceCanvas(sprite) {
  const img = await loadImageFromDataURL(sprite.sourceDataURL);
  const canvas = document.createElement('canvas');
  canvas.width = sprite.width;
  canvas.height = sprite.height;
  canvas.getContext('2d').drawImage(img, 0, 0);
  return canvas;
}

/**
 * Writes every library entry the config references by id into `Library/<libraryId>/` inside the
 * same zip — its own `manifest.json` plus its frames, the identical layout a standalone export of
 * that entry produces.
 *
 * This is what makes the enemy import self-sufficient: `ProjectileConfigImporter` resolves
 * `tuning.projectile.libraryId` by asking `FxPrefabBuilder` for that id, and the builder needs the
 * frames to be somewhere on disk. Shipping them inside the enemy's own bundle means importing the
 * enemy alone materializes its projectile, with no "import the projectile first" step — which is
 * exactly the requirement. `CombinedBundleImporter.LiftEmbeddedLibraryEntries` moves them out to
 * the shared `Assets/Art/WebLibrary/` on the Unity side, since a projectile can be shared by two
 * enemies and must not die with either one.
 */
async function embedReferencedLibraryEntries(zip, configObj) {
  for (const id of referencedLibraryIds(configObj)) {
    const entry = await getEnemy(id);
    if (!entry || !entry.sprite) continue;

    const folder = zip.folder('Library').folder(id);
    const manifest = populateSpriteZip(folder, {
      enemyName: entry.enemyName,
      kind: entry.kind,
      libraryId: entry.id,
      lanes: entry.sprite.lanes,
      boxes: entry.sprite.boxes,
      source: await sourceCanvas(entry.sprite),
    });
    folder.file('manifest.json', JSON.stringify(manifest, null, 2));

    // The entry's own config travels with its frames, under the name that entry's kind uses, so
    // whatever reads the lifted folder later sees the same pair a standalone export produces.
    const entryConfigFile = kindOf(entry.kind).configFile;
    if (entry.config && entryConfigFile) {
      folder.file(entryConfigFile, JSON.stringify(entry.config, null, 2));
    }
  }
}

/**
 * Every library id the config points at, whatever kind of config it is — deduplicated, since two
 * boss attacks can legitimately throw the same projectile and embedding it twice would just make
 * the zip bigger.
 *
 * The three places an id can appear are the three "art by reference" links in the schemas:
 *  - EnemyConfig  -> tuning.projectile.libraryId
 *  - BossConfig   -> attacks.<id>.params.projectile.libraryId (see modules/boss-export.js for why
 *                    it lands inside `params`) and base.enemyLibraryId
 *  - FxConfig     -> its own top-level libraryId is the entry being exported, NOT a reference to a
 *                    different one, so it is deliberately absent here: embedding it would put the
 *                    entry's own frames inside its own bundle a second time.
 */
function referencedLibraryIds(configObj) {
  const ids = new Set();

  const enemyProjectile = configObj?.tuning?.projectile?.libraryId;
  if (enemyProjectile) ids.add(enemyProjectile);

  if (configObj?.kind === 'boss') {
    if (configObj.base?.enemyLibraryId) ids.add(configObj.base.enemyLibraryId);
    Object.values(configObj.attacks || {}).forEach((attack) => {
      const id = attack?.params?.projectile?.libraryId;
      if (id) ids.add(id);
    });
  }

  return [...ids];
}

/**
 * @param {object} args
 * @param {string} args.enemyName
 * @param {string} [args.kind] - the entry's kind, forwarded into manifest.json AND used to pick the
 *   config's file name at the zip root (KINDS[kind].configFile — `enemy-config.json` for an enemy,
 *   `fx-config.json` for a projectile/VFX). Named per kind rather than one generic `config.json`
 *   because the Unity readers are different files: CombinedBundleImporter looks for the first,
 *   FxPrefabBuilder for the second, and a single name would force whoever finds a folder to sniff
 *   the document to know what it is holding.
 * @param {object} args.sprite - a library entry's `sprite` block: { lanes, boxes, sourceDataURL, width, height }.
 * @param {object} args.configObj - the config export object for that kind (modules/enemy-export.js's
 *   buildExportObject, or modules/fx-export.js's buildFxExportObject).
 * @returns {Promise<Blob>}
 */
export async function buildCombinedBundle({ enemyName, kind = 'enemy', libraryId = null, sprite, configObj }) {
  const zip = new window.JSZip();
  const manifest = populateSpriteZip(zip, {
    enemyName, kind, libraryId, lanes: sprite.lanes, boxes: sprite.boxes, source: await sourceCanvas(sprite),
  });
  zip.file('manifest.json', JSON.stringify(manifest, null, 2));
  zip.file(kindOf(kind).configFile || 'enemy-config.json', JSON.stringify(configObj, null, 2));

  await embedReferencedLibraryEntries(zip, configObj);

  // Still resolves to the Blob and nothing else, so every existing `await buildCombinedBundle(...)`
  // call site keeps working unchanged.
  return zip.generateAsync({ type: 'blob' });
}
