// tools/gen-fichas.cjs — rellena en docs/armas/*.md las tablas que salen del código de jugable.html: el árbol de cada arma
// (evoluciones y mejoras, o su lista provisional), sus ajustes (TUNING), y en el README los items generales y las reacciones.
// Sólo reescribe lo que hay entre <!-- gen:X --> y <!-- /gen:X --> (X = tree, tuning, items, react): el resto es texto a mano.
// Uso: node docs/bocetos/habilidades/tools/gen-fichas.cjs
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = path.join(__dirname, '..'), out = path.join(__dirname, '..', '..', '..', 'armas');
const FILES = { fan: 'abanico', sing: 'singularidad', ray: 'rayo', moon: 'media-luna', seed: 'semilla', star: 'golpe-estelar', time: 'reloj', gust: 'rafaga', whip: 'latigo' };
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'jugable-ui.js');
const sb = { console, Math, performance: { now: () => 0 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0 };
vm.createContext(sb);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });
vm.runInContext('this.__x = { WEAPONS, REACT_TUNING, ITEMS, ITEM_TUNING, TAGS };', sb);
const { WEAPONS, REACT_TUNING, ITEMS, ITEM_TUNING, TAGS } = sb.__x;

const code = s => '`' + s + '`';
const tuningTable = spec => '| Campo | Valor | Rango | Qué es |\n|---|---:|---|---|\n' +
  Object.entries(spec).map(([k, [v, mn, mx, st, lab]]) => `| ${code(k)} | ${v} | ${mn} – ${mx} (paso ${st}) | ${lab} |`).join('\n');
const upsTable = ups => '| Mejora (id) | Qué hace |\n|---|---|\n' + ups.map(m => `| ${m.name} (${code(m.id)}) | ${m.desc} |`).join('\n');
const treeText = d => d.archetypes
  ? '| Evolución (id) | Idea | Mejoras |\n|---|---|---|\n' + d.archetypes.map(a => `| **${a.name}** (${code(a.id)}) | ${a.desc} | ${a.ups.map(u => `**${u.name}** (${code(u.id)}): ${u.desc}`).join('<br>')} |`).join('\n')
  : 'Mejoras provisionales (el árbol con evoluciones está pendiente de diseñar):\n\n' + upsTable(d.ups || []);
const itemsTable = () => '| Item (id) | Rareza | Etiquetas | Efecto propio |\n|---|---|---|---|\n' + ITEMS.map(i => `| ${i.name} (${code(i.id)}) | ${i.rarity} | ${i.tags.map(t => TAGS[t].name).join(' + ')} | ${i.desc} |`).join('\n');
const synTable = () => '| Etiqueta | 2 | 4 | 6 |\n|---|---|---|---|\n' + Object.values(TAGS).map(t => `| **${t.name}** (${t.kind === 'elem' ? 'elemental' : 'universal'}) | ${t.tiers.join(' | ')} |`).join('\n');

function put(file, tag, body) {
  const f = path.join(out, file);
  if (!fs.existsSync(f)) { console.log('falta', f); return; }
  let s = fs.readFileSync(f, 'utf8');
  const re = new RegExp(`<!-- gen:${tag} -->[\\s\\S]*?<!-- /gen:${tag} -->`);
  if (!re.test(s)) { console.log(`sin marcas gen:${tag} en`, file); return; }
  s = s.replace(re, () => `<!-- gen:${tag} -->\n${body}\n<!-- /gen:${tag} -->`);
  fs.writeFileSync(f, s);
}
for (const id in WEAPONS) {
  const file = (FILES[id] || id) + '.md';
  put(file, 'tree', treeText(WEAPONS[id]));
  put(file, 'tuning', tuningTable(WEAPONS[id].tuning));
  console.log('ok', file);
}
put('README.md', 'react', tuningTable(REACT_TUNING));
put('README.md', 'items', '**Sinergias**\n\n' + synTable() + '\n\n**Items**\n\n' + itemsTable() +'\n\nAjustes (' + code('G.tuning.items') + '):\n\n' + tuningTable(ITEM_TUNING));
console.log('ok README.md (reacciones, items)');
