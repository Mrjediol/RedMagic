// modules/map-library.js
// -----------------------------------------------------------------------------
// Cross-session, persistent (IndexedDB, same reasoning as modules/enemy-library.js — cropped/
// composited PNGs blow past localStorage's quota) storage for Map Tracer content. A SEPARATE
// database from the enemy library (different domain, no reason to couple their schemas or
// versioning), with two object stores for the two kinds of thing this tab saves:
//
//   - `maps`   — a full scene: canvas size, every piece asset it uses (image + type + that
//                piece's own traced lines), and every placed instance. Everything
//                modules/map-export.js needs to reproduce the PNG/JSON export later.
//   - `pieces` — ONE asset saved standalone (image + assigned type + its own local-space
//                `lines`), independent of any map, so a border/background/platform can be
//                authored once and reused across different maps.
//
// See ARCHITECTURE.md's "Map Tracer library (maps vs. pieces)" section for the exact record
// shapes and how "cargar" differs between the two kinds.

// Pure helper, no DB coupling — reused as-is rather than duplicated.
export { buildThumbnail } from './enemy-library.js';

const DB_NAME = 'redmagic-map-library';
const DB_VERSION = 1;
const MAPS_STORE = 'maps';
const PIECES_STORE = 'pieces';

let _dbPromise = null;

function openDb() {
  if (_dbPromise) return _dbPromise;
  _dbPromise = new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(MAPS_STORE)) db.createObjectStore(MAPS_STORE, { keyPath: 'id' });
      if (!db.objectStoreNames.contains(PIECES_STORE)) db.createObjectStore(PIECES_STORE, { keyPath: 'id' });
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  return _dbPromise;
}

async function store(name, mode) {
  const db = await openDb();
  return db.transaction(name, mode).objectStore(name);
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

/** Decodes a stored dataURL back into a loaded `Image`. Kept local (not imported from
 * enemy-library.js) so this module has no dependency on the enemy library's module at all. */
export function loadImageFromDataURL(dataURL) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('No se pudo decodificar la imagen guardada.'));
    img.src = dataURL;
  });
}

export function imageToDataURL(image) {
  const c = document.createElement('canvas');
  c.width = image.width; c.height = image.height;
  c.getContext('2d').drawImage(image, 0, 0);
  return c.toDataURL();
}

// ============================================================ maps

/**
 * `entry.assets` is the LIVE piece array (with `.image` as an `Image`) — serialized here into
 * `{name, fileName, width, height, type, imageDataURL, lines}` per piece so the record is plain
 * JSON, in array order. Live runtime asset ids don't survive a save (they're regenerated on every
 * load), so `entry.instances` (which reference pieces by `assetId`) are stored with an
 * `assetIndex` (index into `assets`) instead — `loadMapAsSession` reverses this.
 */
export async function saveMap(entry) {
  const now = Date.now();
  const assetIndexById = new Map(entry.assets.map((a, i) => [a.id, i]));
  const record = {
    id: entry.id || uuid(),
    name: entry.name,
    createdAt: entry.createdAt || now,
    updatedAt: now,
    thumbnail: entry.thumbnail || null,
    canvasWidth: entry.canvasWidth,
    canvasHeight: entry.canvasHeight,
    assets: entry.assets.map((a) => ({
      name: a.name, fileName: a.fileName, width: a.width, height: a.height,
      type: a.type, imageDataURL: a.image ? imageToDataURL(a.image) : a.imageDataURL,
      lines: structuredClone(a.lines),
    })),
    instances: entry.instances.map((i) => ({
      id: i.id, assetIndex: assetIndexById.get(i.assetId),
      x: i.x, y: i.y, scaleX: i.scaleX ?? 1, scaleY: i.scaleY ?? 1,
    })),
  };
  const s = await store(MAPS_STORE, 'readwrite');
  await wrap(s.put(record));
  return record;
}

export async function listMaps() {
  const s = await store(MAPS_STORE, 'readonly');
  const all = await wrap(s.getAll());
  return all.sort((a, b) => b.updatedAt - a.updatedAt);
}

export async function getMap(id) {
  const s = await store(MAPS_STORE, 'readonly');
  return (await wrap(s.get(id))) || null;
}

export async function deleteMap(id) {
  const s = await store(MAPS_STORE, 'readwrite');
  await wrap(s.delete(id));
}

export async function updateMap(id, patch) {
  const existing = await getMap(id);
  if (!existing) throw new Error(`No hay ningún mapa guardado con id '${id}'.`);
  return saveMap({ ...existing, ...patch, id });
}

/**
 * Rehydrates a saved map record back into a live `{assets, instances}` pair — decodes every
 * piece's `imageDataURL` into a fresh `Image` with a fresh runtime id, then re-points each
 * instance's `assetIndex` (see `saveMap`) to that new id.
 */
export async function loadMapAsSession(record) {
  const assets = await Promise.all(record.assets.map(async (a) => {
    const image = await loadImageFromDataURL(a.imageDataURL);
    return {
      id: uuid(), name: a.name, fileName: a.fileName, width: a.width, height: a.height,
      type: a.type, image, lines: structuredClone(a.lines),
    };
  }));
  const instances = record.instances
    .filter((i) => i.assetIndex != null && assets[i.assetIndex])
    .map((i) => ({
      id: uuid(), assetId: assets[i.assetIndex].id,
      x: i.x, y: i.y, scaleX: i.scaleX ?? 1, scaleY: i.scaleY ?? 1,
    }));
  return { assets, instances };
}

// ============================================================ pieces

/** `entry.image` (an `Image`) is serialized to `imageDataURL`; a standalone piece has no `id` reuse across maps. */
export async function savePiece(entry) {
  const now = Date.now();
  const record = {
    id: entry.id || uuid(),
    name: entry.name,
    createdAt: entry.createdAt || now,
    updatedAt: now,
    thumbnail: entry.thumbnail || null,
    type: entry.type,
    imageDataURL: entry.image ? imageToDataURL(entry.image) : entry.imageDataURL,
    width: entry.width,
    height: entry.height,
    lines: structuredClone(entry.lines || []),
  };
  const s = await store(PIECES_STORE, 'readwrite');
  await wrap(s.put(record));
  return record;
}

export async function listPieces() {
  const s = await store(PIECES_STORE, 'readonly');
  const all = await wrap(s.getAll());
  return all.sort((a, b) => b.updatedAt - a.updatedAt);
}

export async function getPiece(id) {
  const s = await store(PIECES_STORE, 'readonly');
  return (await wrap(s.get(id))) || null;
}

export async function deletePiece(id) {
  const s = await store(PIECES_STORE, 'readwrite');
  await wrap(s.delete(id));
}

export async function updatePiece(id, patch) {
  const existing = await getPiece(id);
  if (!existing) throw new Error(`No hay ninguna pieza guardada con id '${id}'.`);
  return savePiece({ ...existing, ...patch, id });
}

/** Rehydrates a saved piece record into a live map-assets.js-shaped asset (fresh runtime id). */
export async function loadPieceAsAsset(record) {
  const image = await loadImageFromDataURL(record.imageDataURL);
  return {
    id: uuid(),
    name: record.name, fileName: record.name, width: record.width, height: record.height,
    type: record.type, image, lines: structuredClone(record.lines || []),
  };
}
