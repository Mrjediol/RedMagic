// tools/gen-fichas.cjs — rellena en docs/armas/*.md las tablas de mods y ajustes leyendo el código real de jugable.html
// (addWeapon → mods + TUNING, y REACT_TUNING). Sólo reescribe lo que hay entre <!-- gen:mods --> … <!-- /gen:mods --> y
// <!-- gen:tuning --> … <!-- /gen:tuning -->: el resto de cada ficha es texto a mano. Uso: node docs/bocetos/habilidades/tools/gen-fichas.cjs
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = path.join(__dirname, '..'), out = path.join(__dirname, '..', '..', '..', 'armas');
const FILES = { fan: 'abanico', sing: 'singularidad', ray: 'rayo', moon: 'media-luna', seed: 'semilla', star: 'golpe-estelar' };
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'jugable-ui.js');
const sb = { console, Math, performance: { now: () => 0 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0 };
vm.createContext(sb);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });
vm.runInContext('this.__x = { WEAPONS, REACT_TUNING };', sb);
const { WEAPONS, REACT_TUNING } = sb.__x;

const tuningTable = spec => '| Campo | Valor | Rango | Qué es |\n|---|---:|---|---|\n' +
  Object.entries(spec).map(([k, [v, mn, mx, st, lab]]) => `| \`${k}\` | ${v} | ${mn} – ${mx} (paso ${st}) | ${lab} |`).join('\n');
const modsTable = mods => '| Mod (`id`) | Qué hace |\n|---|---|\n' + mods.map(m => `| ${m.name} (\`${m.id}\`) | ${m.desc} |`).join('\n');
function put(file, tag, body) {
  const f = path.join(out, file);
  if (!fs.existsSync(f)) { console.log('falta', f); return; }
  let s = fs.readFileSync(f, 'utf8');
  const re = new RegExp(`<!-- gen:${tag} -->[\\s\\S]*?<!-- /gen:${tag} -->`);
  if (!re.test(s)) { console.log(`sin marcas gen:${tag} en`, file); return; }
  s = s.replace(re, `<!-- gen:${tag} -->\n${body}\n<!-- /gen:${tag} -->`);
  fs.writeFileSync(f, s);
}
for (const id in WEAPONS) {
  const file = (FILES[id] || id) + '.md';
  put(file, 'mods', modsTable(WEAPONS[id].mods));
  put(file, 'tuning', tuningTable(WEAPONS[id].tuning));
  console.log('ok', file);
}
put('README.md', 'react', tuningTable(REACT_TUNING));
console.log('ok README.md (reacciones)');
