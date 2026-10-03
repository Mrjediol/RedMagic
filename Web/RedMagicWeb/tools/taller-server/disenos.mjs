// tools/taller-server/disenos.mjs
// -----------------------------------------------------------------------------
// 🎨 Designs: batches of proposals a task generated (enemies, projectiles, effects, sounds) waiting for the
// user's yes / no, and what an accepted one becomes. Plain repo files, so Claude reads and writes them like
// any other (full design: docs/architecture/designs.md).
//
//   docs/disenos/<id>.json             a batch: { title, request, createdAt, kind?, taskId?, items: [Item] }
//   Item = { id, kind, name, status: pending|accepted|discarded, note?, + the proposal itself:
//            enemy → image ("comfy:<path under Comfy's output>" or docs/…) · projectile / fx → vfx: { preset,
//            params } · sound → sound: { family, label, seconds, loop?, layers } }
//
// What ACCEPT does, by kind:
//   enemy       → docs/enemigos/<Name>/ (enemigo.json + diseno.png): the enemy library, estado "por-montar"
//   sound       → modules/game-sounds-added.js (the game's palette) + bake-sounds.mjs → Assets/Audio/Sfx/Game/
//   projectile / fx → baked into the web's 📚 Biblioteca by the BROWSER (it lives in IndexedDB); here only the
//                 decision and its libraryId are recorded
// "pending" undoes either decision (an accepted sound / enemy is taken back out).
//
// Read from disk on every request (a handful of small files): Claude's edits show up with no watcher.

import fs from 'node:fs';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const run = promisify(execFile);
const KINDS = ['enemy', 'projectile', 'fx', 'sound'];
const PASCAL = /^[A-Z][A-Za-z0-9]{2,40}$/;
const SNAKE = /^[a-z][a-z0-9_]{2,40}$/;
const LIBRARY_NAME = /^[A-Za-z][A-Za-z0-9_]{1,40}$/;

export class Disenos {
  constructor({ projectRoot, comfyOutput }) {
    this.projectRoot = projectRoot;
    this.dir = path.join(projectRoot, 'docs', 'disenos');
    this.enemiesDir = path.join(projectRoot, 'docs', 'enemigos');
    this.webRoot = path.join(projectRoot, 'Web', 'RedMagicWeb');
    this.addedSounds = path.join(this.webRoot, 'modules', 'game-sounds-added.js');
    this.comfyOutput = comfyOutput;
  }

  // ------------------------------------------------------------ reading

  list() {
    const lotes = readDir(this.dir).filter((f) => f.endsWith('.json')).map((f) => this._readLote(f.slice(0, -5))).filter(Boolean)
      .sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')));
    const enemigos = readDir(this.enemiesDir).map((d) => readJson(path.join(this.enemiesDir, d, 'enemigo.json'))).filter(Boolean)
      .sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')));
    return { lotes, enemigos };
  }

  _readLote(id) {
    const lote = readJson(this._lotePath(id));
    if (!lote) return null;
    lote.id = id;
    lote.items = (Array.isArray(lote.items) ? lote.items : []).filter((i) => i && typeof i === 'object').map((i, n) => ({
      ...i,
      id: String(i.id ?? n + 1),
      kind: KINDS.includes(i.kind) ? i.kind : KINDS.includes(lote.kind) ? lote.kind : 'enemy',
      name: String(i.name || ''),
      status: ['accepted', 'discarded'].includes(i.status) ? i.status : 'pending',
    }));
    return lote;
  }

  _lotePath(id) {
    if (!/^[\w.-]+$/.test(id)) throw new Error('lote inválido');
    return path.join(this.dir, `${id}.json`);
  }

  /** The file behind an image reference, or null when it points outside Comfy's output and docs/. */
  resolveImage(src) {
    const s = String(src || '');
    const docs = path.join(this.projectRoot, 'docs');
    const [base, file] = s.startsWith('comfy:') ? [this.comfyOutput, path.resolve(this.comfyOutput, s.slice(6))] : [docs, path.resolve(this.projectRoot, s)];
    return base && file.startsWith(base + path.sep) ? file : null;
  }

  // ------------------------------------------------------------ reviewing

  /**
   * decision: accept | discard | pending (undo). names: { itemId: name } typed by the user; libraryIds:
   * { itemId: id } for projectiles / effects the browser already saved. Each item on its own: one error
   * does not block the rest; errors come back per item.
   */
  async review({ loteId, itemIds, decision, names = {}, libraryIds = {} }) {
    const lote = this._readLote(loteId);
    if (!lote) throw new Error('No encuentro ese lote');
    if (!['accept', 'discard', 'pending'].includes(decision)) throw new Error('decisión inválida');
    const errors = {};
    for (const item of lote.items.filter((i) => itemIds.includes(i.id))) {
      try {
        if (decision === 'accept') {
          if (item.status === 'accepted') continue;
          const name = String(names[item.id] ?? item.name).trim();
          if (item.kind === 'enemy') this._acceptEnemy(lote, item, name);
          else if (item.kind === 'sound') await this._acceptSound(item, name);
          else {
            if (!libraryIds[item.id]) throw new Error('Falta guardarlo en la Biblioteca (lo hace la web)');
            item.name = name;
            item.libraryId = libraryIds[item.id];
          }
          item.status = 'accepted';
        } else {
          if (item.status === 'accepted') await this._undo(item);
          item.status = decision === 'discard' ? 'discarded' : 'pending';
        }
      } catch (e) { errors[item.id] = e.message; }
    }
    lote.reviewedAt = new Date().toISOString();
    const { id: _id, ...stored } = lote;
    writeJson(this._lotePath(loteId), stored);
    return { errors };
  }

  async _undo(item) {
    if (item.kind === 'enemy') {
      const dir = path.join(this.enemiesDir, item.enemy || item.name);
      const rec = readJson(path.join(dir, 'enemigo.json'));
      if (rec && rec.estado !== 'por-montar') throw new Error(`${rec.name} ya está en marcha (${rec.estado}): no se puede deshacer aquí`);
      if (rec) fs.rmSync(dir, { recursive: true, force: true });
      delete item.enemy;
    } else if (item.kind === 'sound') {
      const added = this._readAdded();
      const def = added[item.name];
      if (def) {
        delete added[item.name];
        this._writeAdded(added);
        const wav = path.join(this.projectRoot, 'Assets', 'Audio', 'Sfx', 'Game', def.family, `${item.name}.wav`);
        for (const f of [wav, `${wav}.meta`]) fs.rmSync(f, { force: true });
        await this._bakeSounds('__ninguno__');   // rewrites palette.json without the sound
      }
    } else {
      delete item.libraryId;                     // the browser already deleted the library entry
    }
  }

  // ------------------------------------------------------------ enemy → docs/enemigos/<Name>/

  _acceptEnemy(lote, item, name) {
    if (!PASCAL.test(name)) throw new Error('El nombre va en PascalCase, sin espacios (ej.: TortugaVoladora)');
    const dir = path.join(this.enemiesDir, name);
    if (fs.existsSync(dir)) throw new Error(`Ya hay un enemigo ${name} en la biblioteca`);
    if (this._prefabExists(name)) throw new Error(`Ya existe un prefab ${name} en Unity`);
    const src = this.resolveImage(item.image);
    if (!src || !fs.existsSync(src)) throw new Error('No encuentro la imagen del diseño');
    const ext = path.extname(src).toLowerCase() || '.png';
    fs.mkdirSync(dir, { recursive: true });
    fs.copyFileSync(src, path.join(dir, `diseno${ext}`));
    writeJson(path.join(dir, 'enemigo.json'), {
      name,
      estado: 'por-montar',
      diseno: { image: `docs/enemigos/${name}/diseno${ext}`, source: item.image, lote: lote.id, item: item.id, request: lote.request || lote.title || '', note: item.note || '' },
      pendientes: [],
      createdAt: new Date().toISOString(),
    });
    item.name = name;
    item.enemy = name;
  }

  _prefabExists(name) {
    const wanted = new Set([`${name}.prefab`, `Enemy_${name}.prefab`].map((s) => s.toLowerCase()));
    const walk = (d) => readDir(d).some((f) => {
      const p = path.join(d, f);
      try { return fs.statSync(p).isDirectory() ? walk(p) : wanted.has(f.toLowerCase()); } catch { return false; }
    });
    return walk(path.join(this.projectRoot, 'Assets', 'Prefabs', 'Enemies'));
  }

  // ------------------------------------------------------------ sound → the game's palette

  async _acceptSound(item, id) {
    if (!SNAKE.test(id)) throw new Error('El nombre de un sonido va en minúsculas con _ (ej.: enemy_turtle_shell)');
    const def = item.sound;
    if (!def || !Array.isArray(def.layers) || !def.family) throw new Error('A la propuesta le falta el sonido (family, layers)');
    const used = await this._soundIds();
    if (used.includes(id)) throw new Error(`Ya hay un sonido ${id}`);
    const added = this._readAdded();
    added[id] = { family: def.family, label: def.label || id, seconds: def.seconds, ...(def.loop ? { loop: true } : {}), layers: def.layers };
    this._writeAdded(added);
    try {
      await this._bakeSounds(id);
    } catch (e) {
      delete added[id];
      this._writeAdded(added);
      throw new Error(`No pasa la comprobación de sonidos: ${e.message}`);
    }
    item.name = id;
  }

  async _soundIds() {
    const mod = (f) => JSON.stringify(path.join(this.webRoot, 'modules', f).replace(/\\/g, '/'));
    const code = `const g = await import('file:///' + ${mod('game-sounds.js')}); const v = await import('file:///' + ${mod('vfx-sounds.js')});`
      + 'console.log(JSON.stringify([...Object.keys(g.GAME_SOUNDS), ...Object.keys(v.SOUNDS)]));';
    const { stdout } = await run(process.execPath, ['--input-type=module', '-e', code], { cwd: this.webRoot });
    return JSON.parse(stdout);
  }

  async _bakeSounds(id) {
    try {
      await run(process.execPath, ['bake-sounds.mjs', id], { cwd: path.join(this.webRoot, 'tools'), timeout: 60000 });
    } catch (e) {
      throw new Error(String(e.stderr || e.message).trim().split('\n').slice(0, 4).join(' · '));
    }
  }

  _readAdded() {
    const text = fs.existsSync(this.addedSounds) ? fs.readFileSync(this.addedSounds, 'utf8') : '';
    const m = text.match(/\/\* BEGIN \*\/([\s\S]*)\/\* END \*\//);
    return m ? JSON.parse(m[1]) : {};
  }

  _writeAdded(sounds) {
    fs.writeFileSync(this.addedSounds, `// modules/game-sounds-added.js
// -----------------------------------------------------------------------------
// Sounds accepted in 🎨 Designs, written by tools/taller-server (disenos.mjs): game-sounds.js adds them to
// GAME_SOUNDS (same shape). Generated — to change one, move it into game-sounds.js and delete it here.

export const ADDED_SOUNDS = /* BEGIN */${JSON.stringify(sounds, null, 2)}/* END */;
`);
  }
}

function readDir(dir) { try { return fs.readdirSync(dir); } catch { return []; } }
function readJson(file) { try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return null; } }
function writeJson(file, obj) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(obj, null, 2)}\n`);
}
