// tools/taller-server/enemigos.mjs
// -----------------------------------------------------------------------------
// 👾 Enemigos: design batches to review and the enemy library, both plain files in docs/enemigos/ so Claude
// reads and writes them like any other repo file (full design: docs/architecture/enemigos-web.md).
//
//   docs/enemigos/lotes/<id>.json      a batch of designs a task generated: { id, title, request, createdAt,
//                                      taskId?, items: [{ id, name, image, note?, status }] }
//                                      image = "comfy:<path under Comfy's output>" or a repo path;
//                                      status = pending | accepted | discarded
//   docs/enemigos/<Name>/enemigo.json  an accepted enemy: { name, estado, diseno: { image, source, lote, item,
//                                      request, note }, pendientes: [], createdAt }
//   docs/enemigos/<Name>/diseno.png    its design, copied out of Comfy's output (which is not in the repo)
//
// Read from disk on every request (a handful of small files): Claude's edits show up with no watcher.

import fs from 'node:fs';
import path from 'node:path';

const NAME_RE = /^[A-Z][A-Za-z0-9]{2,40}$/;

export class Enemigos {
  constructor({ projectRoot, comfyOutput }) {
    this.projectRoot = projectRoot;
    this.root = path.join(projectRoot, 'docs', 'enemigos');
    this.lotesDir = path.join(this.root, 'lotes');
    this.comfyOutput = comfyOutput;
  }

  // ------------------------------------------------------------ reading

  list() {
    const lotes = readDir(this.lotesDir).filter((f) => f.endsWith('.json')).map((f) => this._readLote(f.slice(0, -5))).filter(Boolean)
      .sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')));
    const enemigos = readDir(this.root).filter((d) => d !== 'lotes').map((d) => readJson(path.join(this.root, d, 'enemigo.json'))).filter(Boolean)
      .sort((a, b) => String(b.createdAt || '').localeCompare(String(a.createdAt || '')));
    return { lotes, enemigos };
  }

  _readLote(id) {
    const lote = readJson(this._lotePath(id));
    if (!lote) return null;
    lote.id = id;
    lote.items = (Array.isArray(lote.items) ? lote.items : []).filter((i) => i && i.image).map((i, n) => ({
      ...i, id: String(i.id ?? n + 1), name: String(i.name || ''), status: ['accepted', 'discarded'].includes(i.status) ? i.status : 'pending',
    }));
    return lote;
  }

  _lotePath(id) {
    if (!/^[\w.-]+$/.test(id)) throw new Error('lote inválido');
    return path.join(this.lotesDir, `${id}.json`);
  }

  /** The file behind an image reference, or null when it points outside the two allowed roots. */
  resolveImage(src) {
    const s = String(src || '');
    const [base, rel] = s.startsWith('comfy:') ? [this.comfyOutput, s.slice(6)] : [this.root, path.relative(this.root, path.resolve(this.projectRoot, s))];
    if (!base || !rel) return null;
    const file = path.resolve(base, rel);
    return file.startsWith(base + path.sep) ? file : null;
  }

  // ------------------------------------------------------------ reviewing a batch

  /**
   * decision: 'accept' | 'discard' | 'pending' (undo). names: { itemId: Name } for the accepted ones.
   * Each item is done on its own: one bad name does not block the rest; the errors come back per item.
   */
  review({ loteId, itemIds, decision, names = {} }) {
    const lote = this._readLote(loteId);
    if (!lote) throw new Error('No encuentro ese lote');
    if (!['accept', 'discard', 'pending'].includes(decision)) throw new Error('decisión inválida');
    const errors = {};
    for (const item of lote.items.filter((i) => itemIds.includes(i.id))) {
      try {
        if (decision === 'accept' && item.status !== 'accepted') this._accept(lote, item, String(names[item.id] ?? item.name).trim());
        else if (decision !== 'accept') {
          if (item.status === 'accepted') this._unaccept(item);
          item.status = decision === 'discard' ? 'discarded' : 'pending';
        }
      } catch (e) { errors[item.id] = e.message; }
    }
    lote.reviewedAt = new Date().toISOString();
    writeJson(this._lotePath(loteId), lote);
    return { errors };
  }

  _accept(lote, item, name) {
    if (!NAME_RE.test(name)) throw new Error('El nombre va en PascalCase, sin espacios (ej.: TortugaVoladora)');
    const dir = path.join(this.root, name);
    if (fs.existsSync(dir)) throw new Error(`Ya hay un enemigo ${name} en la biblioteca`);
    if (this._prefabExists(name)) throw new Error(`Ya existe un prefab ${name} en Unity`);
    const src = this.resolveImage(item.image);
    if (!src || !fs.existsSync(src)) throw new Error('No encuentro la imagen del diseño');
    fs.mkdirSync(dir, { recursive: true });
    fs.copyFileSync(src, path.join(dir, `diseno${path.extname(src).toLowerCase() || '.png'}`));
    writeJson(path.join(dir, 'enemigo.json'), {
      name,
      estado: 'por-montar',
      diseno: {
        image: `docs/enemigos/${name}/diseno${path.extname(src).toLowerCase() || '.png'}`,
        source: item.image, lote: lote.id, item: item.id, request: lote.request || lote.title || '', note: item.note || '',
      },
      pendientes: [],
      createdAt: new Date().toISOString(),
    });
    item.name = name;
    item.status = 'accepted';
    item.enemy = name;
  }

  /** Undo an accept, only while nothing was built from it yet. */
  _unaccept(item) {
    const name = item.enemy || item.name;
    const dir = path.join(this.root, name);
    const rec = readJson(path.join(dir, 'enemigo.json'));
    if (rec && rec.estado !== 'por-montar') throw new Error(`${name} ya está en marcha (${rec.estado}): no se puede deshacer aquí`);
    if (rec) fs.rmSync(dir, { recursive: true, force: true });
    delete item.enemy;
  }

  _prefabExists(name) {
    const dir = path.join(this.projectRoot, 'Assets', 'Prefabs', 'Enemies');
    const wanted = new Set([`${name}.prefab`, `Enemy_${name}.prefab`].map((s) => s.toLowerCase()));
    const walk = (d) => readDir(d).some((f) => {
      const p = path.join(d, f);
      try { return fs.statSync(p).isDirectory() ? walk(p) : wanted.has(f.toLowerCase()); } catch { return false; }
    });
    return walk(dir);
  }
}

function readDir(dir) { try { return fs.readdirSync(dir); } catch { return []; } }
function readJson(file) { try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return null; } }
function writeJson(file, obj) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, `${JSON.stringify(obj, null, 2)}\n`);
}
