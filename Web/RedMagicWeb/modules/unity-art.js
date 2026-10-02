// modules/unity-art.js
// -----------------------------------------------------------------------------
// Art that lives in the Unity PROJECT, not in this browser's library: what an attack read through the
// bridge (unity-bridge.js) points at when its projectile / effect is a prefab of the project.
//
// In an attack card an art slot holds a string: a library id, or — for these — `unity:<asset path>`.
// Such a value is exported back exactly as it came (`{ "project": "<asset path>" }`, which Unity's
// BossArtBuilder.ResolveArtRefs turns into the same reference), so what was not changed in the web is
// never rebuilt. When Unity also said which library entry the prefab was built from, that entry is
// used as its PREVIEW (the picker's thumbnail, the test stage's projectile) — `artLibraryId`.

const PREFIX = 'unity:';
const libraryOf = new Map();   // asset path -> library id it was built from (when known)

export const isUnityArt = (value) => typeof value === 'string' && value.startsWith(PREFIX);

export const unityArtPath = (value) => value.slice(PREFIX.length);

/** The card value for a project prefab. `libraryId` (optional) = its preview. */
export function unityArtValue(path, libraryId = null) {
  if (libraryId) libraryOf.set(path, libraryId);
  return PREFIX + path;
}

/** The library entry to SHOW for an art value: itself, or a project prefab's preview ('' = none). */
export function artLibraryId(value) {
  if (!isUnityArt(value)) return value || '';
  return libraryOf.get(unityArtPath(value)) || '';
}

/** What is exported for a project prefab. */
export function unityArtRef(value) {
  const path = unityArtPath(value);
  const libraryId = libraryOf.get(path);
  return libraryId ? { project: path, libraryId } : { project: path };
}

/** `{ project, libraryId? }` (Unity's BossAttackExporter, or a config saved from one). */
export const isProjectRef = (v) => !!v && typeof v === 'object' && !Array.isArray(v) && typeof v.project === 'string';

/** File name of a project prefab, for labels. */
export const unityArtLabel = (value) => unityArtPath(value).split('/').pop().replace(/\.prefab$/i, '');
