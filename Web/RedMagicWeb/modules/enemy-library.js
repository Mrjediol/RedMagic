// modules/enemy-library.js
// -----------------------------------------------------------------------------
// Cross-tab, persistent (survives reload/browser restart) storage for enemies authored in this
// tool — IndexedDB, not localStorage: a sheet's cropped frames re-encoded as base64 PNG blow past
// localStorage's ~5MB quota almost immediately, IndexedDB does not have that ceiling.
//
// One record per entry: the Sprites tab's authoring state (lanes/boxes + a snapshot of the sheet
// pixels, everything buildExportZip needs to reproduce a zip later without the original file
// re-uploaded) plus, once authored, the Enemy Creator's linked EnemyConfig export object. A record
// is the unit both tabs and the Biblioteca tab share — see ARCHITECTURE.md's "Shared entry
// library" section for the exact shape.
//
// KIND: every record carries `kind` ('enemy' | 'projectile' | 'fx' — see modules/entry-kinds.js).
// Records written before kinds existed have no such field; rather than a stored migration (which
// would have to rewrite every record on first load, and fail halfway on a quota error), they are
// normalized to 'enemy' ON READ by `normalizeRecord` below — idempotent, and the normalized value
// is persisted the next time that record is saved for any other reason. The DB version therefore
// stays at 1: nothing about the object store itself changed.
//
// The field holding the name is still called `enemyName` for every kind. It is deliberately NOT
// renamed: it is the same key that reaches `manifest.json`, which Assets/Editor/EnemyImporter.cs
// reads by that exact name. A neutral alias is exposed as `entryName()` for UI that shouldn't say
// "enemy" about a VFX.

import { normalizeKind } from './entry-kinds.js';

const DB_NAME = 'redmagic-enemy-library';
const DB_VERSION = 1;
const STORE = 'enemies';

let _dbPromise = null;

function openDb() {
  if (_dbPromise) return _dbPromise;
  _dbPromise = new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id' });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  return _dbPromise;
}

async function store(mode) {
  const db = await openDb();
  return db.transaction(STORE, mode).objectStore(STORE);
}

function uuid() {
  if (crypto.randomUUID) return crypto.randomUUID();
  return `id-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function wrap(req) {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

/**
 * Fills in what a record saved by an older version of this tool doesn't have. Read-side only —
 * never writes. Keep it total (every field it touches must end up valid for ANY input record), so
 * every consumer can assume a normalized shape without re-checking.
 */
function normalizeRecord(record) {
  if (!record) return null;
  return { ...record, kind: normalizeKind(record.kind) };
}

/** The entry's display name, whatever its kind. Storage still calls this field `enemyName`. */
export function entryName(entry) {
  return entry ? entry.enemyName : '';
}

/**
 * Creates a new record (no `id`) or overwrites an existing one (`id` present). `entry.sprite` is
 * `{ lanes, boxes, sourceDataURL, width, height }` — everything buildExportZip/populateSpriteZip
 * needs, reconstructable into a real canvas via `loadImageFromDataURL(sourceDataURL)`.
 * `entry.config` is null until the Enemy Creator has exported at least once against this entry
 * (only ever non-null for `kind: 'enemy'` — the other kinds have no config form).
 *
 * @returns {Promise<object>} the saved record, with its `id` (new or existing) and fresh `updatedAt`.
 */
export async function saveEnemy(entry) {
  const now = Date.now();
  const record = {
    id: entry.id || uuid(),
    kind: normalizeKind(entry.kind),
    enemyName: entry.enemyName,
    createdAt: entry.createdAt || now,
    updatedAt: now,
    sprite: entry.sprite,
    thumbnail: entry.thumbnail || null,
    config: entry.config || null,
  };
  // The Boss Creator's full form state (boss entries only), so reopening a boss restores exactly
  // what was on screen — the exported config alone loses web-only choices.
  if (entry.draft) record.draft = entry.draft;
  const s = await store('readwrite');
  await wrap(s.put(record));
  return record;
}

/** All saved entries of EVERY kind, newest-updated first. */
export async function listEnemies() {
  const s = await store('readonly');
  const all = await wrap(s.getAll());
  return all.map(normalizeRecord).sort((a, b) => b.updatedAt - a.updatedAt);
}

/**
 * Saved entries of one kind (or all of them when `kind` is null/absent), newest-updated first.
 * Filtered in JS rather than through an IndexedDB index on purpose: this library holds tens of
 * entries, not thousands, and an index would have needed a store migration to add.
 */
export async function listEntries(kind = null) {
  const all = await listEnemies();
  return kind ? all.filter((e) => e.kind === kind) : all;
}

export async function getEnemy(id) {
  const s = await store('readonly');
  const result = await wrap(s.get(id));
  return normalizeRecord(result) || null;
}

export async function deleteEnemy(id) {
  const s = await store('readwrite');
  await wrap(s.delete(id));
}

/** Merges `patch` onto the existing record (e.g. `{ config: exportObj }`) and saves it. */
export async function updateEnemy(id, patch) {
  const existing = await getEnemy(id);
  if (!existing) throw new Error(`No hay ningún enemigo guardado con id '${id}'.`);
  return saveEnemy({ ...existing, ...patch, id });
}

/** Decodes a stored `sourceDataURL` (or any data URL) back into a loaded `Image`. */
export function loadImageFromDataURL(dataURL) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('No se pudo decodificar la imagen guardada.'));
    img.src = dataURL;
  });
}

/** Small (max ~160px wide) thumbnail of `source` (an Image or canvas), for the library cards. */
export function buildThumbnail(source, maxSize = 160) {
  const w = source.width || source.naturalWidth;
  const h = source.height || source.naturalHeight;
  const scale = Math.min(1, maxSize / Math.max(w, h));
  const c = document.createElement('canvas');
  c.width = Math.max(1, Math.round(w * scale));
  c.height = Math.max(1, Math.round(h * scale));
  c.getContext('2d').drawImage(source, 0, 0, c.width, c.height);
  return c.toDataURL();
}
