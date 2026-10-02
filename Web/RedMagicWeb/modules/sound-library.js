// modules/sound-library.js
// -----------------------------------------------------------------------------
// Audio files kept in the shared library (kind `sound`, entry-kinds.js): uploaded once — from the
// 📚 Biblioteca tab or from a gesture's 🔊 lane — and then picked wherever a sound is needed.
//
// The record: `{ kind: 'sound', enemyName, sprite: null, draft: { dataURL, fileName, mime } }`.
//
// A gesture's sound (rig-gestures.js ▸ createSound) names its clips by string: an asset path of the
// Unity project (`Assets/…wav`, what the bridge sends) or `lib:<record id>` for one of these. On
// export a library clip travels INSIDE the bundle as `Audio/<file>` (rig-export.js) and Unity copies it
// into the project (RigBundleImporter.ReadGestureSound), so it works with the bridge and with a .zip.

import { saveEnemy, listEntries } from './enemy-library.js';

const PREFIX = 'lib:';
const AUDIO = /\.(wav|ogg|mp3)$/i;

export const isLibraryClip = (clip) => typeof clip === 'string' && clip.startsWith(PREFIX);
export const libraryClipId = (clip) => clip.slice(PREFIX.length);
export const libraryClip = (id) => PREFIX + id;

/** True for a file Unity can import as a clip and the bridge can serve (wav / ogg / mp3). */
export const isAudioFile = (file) => AUDIO.test(file.name || '');

const readDataURL = (file) => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(reader.result);
  reader.onerror = reject;
  reader.readAsDataURL(file);
});

/** Saves an audio file as a library sound (a free name: <file>, <file>2…). Returns the record. */
export async function saveSoundFile(file) {
  if (!isAudioFile(file)) throw new Error(`"${file.name}" no es un wav, ogg o mp3.`);
  const names = new Set((await listEntries('sound')).map((e) => e.enemyName));
  const base = file.name.replace(AUDIO, '').replace(/[^A-Za-z0-9_-]/g, '_') || 'Sonido';
  let name = base;
  for (let n = 2; names.has(name); n++) name = `${base}${n}`;
  const ext = file.name.match(AUDIO)[0].toLowerCase();
  return saveEnemy({ kind: 'sound', sprite: null, enemyName: name,
    draft: { dataURL: await readDataURL(file), fileName: `${name}${ext}`, mime: file.type || '' } });
}

/** The file a library sound travels as inside a bundle (`Audio/<this>`). */
export const soundFileName = (record) => record.draft?.fileName || `${record.enemyName}.wav`;

/** The bytes of a library sound (for decoding). */
export async function soundArrayBuffer(record) {
  return (await fetch(record.draft.dataURL)).arrayBuffer();
}

/** Asks for audio files and saves each one. Returns the new records ([] if the dialog was cancelled). */
export function pickAndSaveSounds({ multiple = true } = {}) {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.wav,.ogg,.mp3,audio/wav,audio/ogg,audio/mpeg';
    input.multiple = multiple;
    input.onchange = async () => {
      const saved = [];
      for (const file of input.files) {
        try { saved.push(await saveSoundFile(file)); } catch (e) { console.warn('[sound-library]', e.message); }
      }
      resolve(saved);
    };
    input.addEventListener('cancel', () => resolve([]));
    input.click();
  });
}
