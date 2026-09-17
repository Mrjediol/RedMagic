// modules/map-export.js
// -----------------------------------------------------------------------------
// PNG compositing + Map JSON export, ported from MapTracer.html's `exportPng`/`exportJson`/
// `sceneLines`. The JSON shape (`format: 'RedMagicMap/1'`, `canvas`, `instances[]`,
// `collisions.groundLines/platformLines`) is byte-identical to the original — it's parsed as-is
// by Assets/Editor/CollisionImporter.cs's `MapImporter`, so **coordinate with that file before
// changing field names or shapes**.

import { findAsset } from './map-assets.js';
import { getInstanceBounds } from './map-instances.js';

const DRAW_ORDER = ['background', 'platform', 'border'];

/** Draws every instance, background-first then platform then border, into a fresh canvas. */
export function composeMap(width, height, assets, instances) {
  const off = document.createElement('canvas');
  off.width = width; off.height = height;
  const o = off.getContext('2d');
  DRAW_ORDER.forEach((type) => {
    instances.forEach((i) => {
      const a = findAsset(assets, i.assetId);
      if (!a || a.type !== type) return;
      const b = getInstanceBounds(i, a);
      o.drawImage(a.image, b.x, b.y, b.w, b.h);
    });
  });
  return off;
}

export function composeMapToBlob(width, height, assets, instances) {
  return new Promise((resolve) => composeMap(width, height, assets, instances).toBlob(resolve));
}

/**
 * Every traced line, baked from a piece's own local pixel space into absolute map-canvas space
 * (`point*scale + instance.x/y`), bucketed by collider type. This is the ONLY place a traced
 * point becomes map-space — modules/map-collision-tracing.js never needs to know about instances.
 */
export function sceneLines(assets, instances) {
  const ground = [], platform = [];
  instances.forEach((i) => {
    const a = findAsset(assets, i.assetId);
    if (!a) return;
    const sx = i.scaleX ?? 1, sy = i.scaleY ?? 1;
    a.lines.forEach((l) => {
      const dst = l.type === 'border' ? ground : platform;
      dst.push({ points: l.points.map((p) => ({ x: p.x * sx + i.x, y: p.y * sy + i.y })) });
    });
  });
  return { ground, platform };
}

/** Builds the exportable `RedMagicMap/1` JSON object (does not stringify/download it). */
export function buildMapJson(width, height, assets, instances) {
  const lines = sceneLines(assets, instances);
  return {
    format: 'RedMagicMap/1',
    canvas: { width, height },
    instances: instances.map((i) => {
      const a = findAsset(assets, i.assetId);
      return {
        assetName: a?.name, fileName: a?.fileName, type: a?.type,
        x: i.x, y: i.y, width: a?.width, height: a?.height,
        scaleX: i.scaleX ?? 1, scaleY: i.scaleY ?? 1,
      };
    }),
    collisions: { groundLines: lines.ground, platformLines: lines.platform },
  };
}

/**
 * Builds the exportable `RedMagicMapPieces/1` JSON for one or more STANDALONE library pieces
 * (`modules/map-library.js` `{name, type, width, height, lines}` records) — independent of any
 * composed map, so there is no `canvas`/`instances` here, just each piece's own local-space
 * lines. `fileName` mirrors `map-library.js`'s `loadPieceAsAsset` convention (== `name`, a saved
 * piece has no original uploaded filename to remember). Parsed by the same
 * `Assets/Editor/CollisionImporter.cs` file as `buildMapJson` above, in a dedicated importer for
 * this shape — coordinate with that file before changing field names.
 */
export function buildPiecesJson(pieceRecords) {
  return {
    format: 'RedMagicMapPieces/1',
    pieces: pieceRecords.map((p) => ({
      assetName: p.name,
      fileName: p.name,
      type: p.type,
      width: p.width,
      height: p.height,
      lines: structuredClone(p.lines || []),
    })),
  };
}
