// modules/map-assets.js
// -----------------------------------------------------------------------------
// Map Tracer's piece library: the pool of uploaded images a map is built from, each tagged
// unassigned/border/background/platform and carrying its own traced collision `lines` in that
// piece's own local pixel space (see modules/map-collision-tracing.js). Ported from the
// standalone MapTracer.html's `assets` array + `addFiles`/`applyAssetType` handlers — same shape,
// split out as pure data functions so app.js only wires DOM.

function uid() {
  return Math.random().toString(36).slice(2, 10);
}

/** One piece: `{id, name, fileName, width, height, type, image, lines}`. `image` is a loaded `Image`. */
function createAsset(name, fileName, image) {
  return {
    id: uid(),
    name,
    fileName,
    width: image.width,
    height: image.height,
    type: 'unassigned',
    image,
    lines: [], // {points:[{x,y}], type:'border'|'platform'}
  };
}

/**
 * Loads every file in `fileList` as an image and appends a new asset to `assets` for each,
 * calling `onAssetAdded(asset)` as each one finishes decoding (loads are async and unordered,
 * exactly like the original tool — the caller selects/renders incrementally as they arrive).
 */
export function addFiles(fileList, assets, onAssetAdded) {
  [...fileList].forEach((f) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const image = new Image();
      image.onload = () => {
        const asset = createAsset(f.name.replace(/\.[^.]+$/, ''), f.name, image);
        assets.push(asset);
        onAssetAdded(asset);
      };
      image.src = e.target.result;
    };
    reader.readAsDataURL(f);
  });
}

export function findAsset(assets, id) {
  return assets.find((a) => a.id === id);
}

/** Sets `type` on every asset in `assets` whose id is in `ids` (a Set or array). */
export function assignType(assets, ids, type) {
  const idSet = ids instanceof Set ? ids : new Set(ids);
  assets.forEach((a) => { if (idSet.has(a.id)) a.type = type; });
}

export function deleteAssets(assets, instances, ids) {
  const idSet = ids instanceof Set ? ids : new Set(ids);
  const remainingAssets = assets.filter((a) => !idSet.has(a.id));
  const remainingInstances = instances.filter((i) => !idSet.has(i.assetId));
  assets.length = 0; assets.push(...remainingAssets);
  instances.length = 0; instances.push(...remainingInstances);
}

export const TYPE_LABEL = {
  unassigned: 'Sin clasificar',
  border: 'Borde / Ground',
  background: 'Fondo',
  platform: 'Plataforma / Platform',
};
