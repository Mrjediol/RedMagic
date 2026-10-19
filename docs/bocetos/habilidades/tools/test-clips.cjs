// tools/test-clips.cjs — prueba sin navegador de los clips (index.html): ejecuta los scripts con un canvas falso y dibuja cada clip
// en 400 instantes; avisa de errores y de NaN. Uso: node docs/bocetos/habilidades/tools/test-clips.cjs ["" | carpeta] [id de habilidad]
// Cuenta llamadas a primitivas con NaN para detectar matemáticas rotas.
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = process.argv[2] || path.join(__dirname, '..');
const html = fs.readFileSync(path.join(dir, 'index.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'ui.js');
let nan = 0, calls = 0; const nanWhere = {};
const ctx = new Proxy({}, {
  get(_, k) {
    if (k === 'createRadialGradient' || k === 'createLinearGradient') return () => ({ addColorStop() {} });
    if (k === 'canvas') return { width: 640, height: 400 };
    return (...a) => { calls++; if (a.some(x => typeof x === 'number' && !isFinite(x))) { nan++; nanWhere[k] = (nanWhere[k] || 0) + 1; } };
  },
  set() { return true; }
});
const sandbox = { console, Math, performance: { now: () => 1000 }, window: {}, document: { getElementById: () => ({}) } };
vm.createContext(sandbox);
for (const f of files) {
  const src = fs.readFileSync(path.join(dir, f), 'utf8');
  try { vm.runInContext(src, sandbox, { filename: f }); } catch (e) { console.log('LOAD ERROR', f, e.stack.split('\n').slice(0, 3).join(' | ')); process.exit(1); }
}
vm.runInContext('this.__ABIL = ABIL; this.__scene = scene;', sandbox);
const only = process.argv[3];
for (const A of sandbox.__ABIL) {
  if (only && A.id !== only) continue;
  for (const c of A.clips) {
    nan = 0; calls = 0; for (const k in nanWhere) delete nanWhere[k];
    let R;
    try { R = A.build(c.P || {}); } catch (e) { console.log('BUILD ERROR', A.id, c.title, e.stack.split('\n').slice(0, 4).join(' | ')); continue; }
    let err = null;
    for (let i = 0; i <= 400 && !err; i++) { const t = R.total * i / 400; try { sandbox.__scene(ctx, t, R); } catch (e) { err = `t=${t.toFixed(3)} ${e.stack.split('\n').slice(0, 4).join(' | ')}`; } }
    const sfx = (R.sfx || []).length, bad = (R.sfx || []).filter(e => typeof e.f !== 'function' || !isFinite(e.t)).length;
    console.log(`${err ? 'ERR ' : nan ? 'NaN ' : 'ok  '} ${A.id} · ${c.title} · total ${R.total.toFixed(2)}s · ${calls} calls · sfx ${sfx}${bad ? ' BAD ' + bad : ''}${nan ? ' · NaN ' + JSON.stringify(nanWhere) : ''}${err ? '\n     ' + err : ''}`);
  }
}
