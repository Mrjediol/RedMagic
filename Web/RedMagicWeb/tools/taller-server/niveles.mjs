// Pestaña 🧱 Niveles: los bocetos *.levelsketch.json viven en Assets/Levels/Bocetos (de ahí los coge el importador de
// Unity, menú EditorNiveles ▸ Importar nivel…) y el arte de los biomas (textura de roca, capas del fondo) se lee de
// Assets/Levels. Nada fuera de esas dos carpetas se lee ni se escribe.
import fs from 'node:fs';
import path from 'node:path';

const EXT = '.levelsketch.json';

export class Niveles {
  constructor({ projectRoot }) {
    this.root = projectRoot;
    this.art = path.join(projectRoot, 'Assets', 'Levels');
    this.dir = path.join(this.art, 'Bocetos');
  }

  /** Un archivo de arte por su ruta del proyecto (Assets/Levels/…), o null si sale de Assets/Levels. */
  resolveArt(rel) {
    if (!rel) return null;
    const file = path.resolve(this.root, rel);
    return file.startsWith(this.art + path.sep) ? file : null;
  }

  /** Un boceto por su nombre (sin extensión), o null. */
  resolveSketch(name) {
    const file = path.join(this.dir, safeName(name) + EXT);
    return file.startsWith(this.dir + path.sep) ? file : null;
  }

  /** Los bocetos guardados, el más reciente primero. */
  list() {
    if (!fs.existsSync(this.dir)) return [];
    return fs.readdirSync(this.dir).filter((f) => f.endsWith(EXT))
      .map((f) => ({ name: f.slice(0, -EXT.length), mtime: fs.statSync(path.join(this.dir, f)).mtimeMs }))
      .sort((a, b) => b.mtime - a.mtime);
  }

  /** { name, text } → escribe Assets/Levels/Bocetos/<name>.levelsketch.json. */
  save({ name, text }) {
    let doc;
    try { doc = JSON.parse(text); } catch { return { error: 'JSON no válido' }; }
    if (!Array.isArray(doc.rows) || doc.rows.length !== doc.height) return { error: 'el boceto no tiene filas válidas' };
    const file = this.resolveSketch(name);
    if (!file) return { error: 'nombre' };
    fs.mkdirSync(this.dir, { recursive: true });
    fs.writeFileSync(file, text);
    return { ok: true, path: path.relative(this.root, file).split(path.sep).join('/') };
  }
}

const safeName = (n) => String(n || 'nivel').trim().replace(/[\\/:*?"<>|.]+/g, '_').slice(0, 80) || 'nivel';
