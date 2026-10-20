// tools/gen-fichas.cjs — escribe / actualiza las fichas docs/enemigos-diseno/<id>.md a partir de los e-*.js (diseño + ajustes).
// Sólo se reescribe lo que hay entre <!-- gen:... --> y <!-- /gen:... -->; «Decisiones y feedback» es a mano y se respeta.
// Uso: node docs/bocetos/enemigos/tools/gen-fichas.cjs
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = path.join(__dirname, '..'), out = path.join(__dirname, '..', '..', '..', 'enemigos-diseno');
const html = fs.readFileSync(path.join(dir, 'jugable.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => !/(^|\/)(ui|jugable-ui)\.js$/.test(f));
const ctx = new Proxy({}, { get: () => () => ({ addColorStop() {} }), set: () => true });
const sb = { console, Math, performance: { now: () => 0 }, window: {}, document: { getElementById: () => ({ getContext: () => ctx }) }, setTimeout: () => 0, setInterval: () => 0, UI: {} };
vm.createContext(sb);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });
const { FOES, ENCOUNTERS } = vm.runInContext('({ FOES, ENCOUNTERS })', sb);
fs.mkdirSync(out, { recursive: true });
const fileOf = { charger: 'embestidor', stalker: 'acechador', mortar: 'artillero', weaver: 'tejedora', shell: 'caparazon', hive: 'colmena', drone: 'zangano', wisp: 'fatuo', burrower: 'excavador', chargerAlpha: 'embestidor-alfa', stalkerAlpha: 'acechador-alfa',
  icicle: 'colgante', urchin: 'erizo-escarcha', gale: 'ventisca', hermit: 'ermitano', walrus: 'morsa', walrusAlpha: 'morsa-matriarca',
  shroom: 'saltaseta', bramble: 'zarza', bear: 'oso-musgo', owl: 'buho-cristal', flytrap: 'atrapamoscas', shroomAlpha: 'seta-madre' };
const block = (name, body, txt) => {
  const a = `<!-- gen:${name} -->`, b = `<!-- /gen:${name} -->`, i = txt.indexOf(a), j = txt.indexOf(b);
  return i < 0 || j < 0 ? txt : txt.slice(0, i + a.length) + '\n' + body + '\n' + txt.slice(j);
};
for (const id in FOES) {
  const d = FOES[id], f = path.join(out, (fileOf[id] || id) + '.md');
  const diseno = [
    `**Nivel:** ${d.tier || 'común'}${d.world ? ' · **Mundo:** ' + d.world : ''} · **Rol:** ${d.role} · **Cuerpo:** ${d.fly ? 'volador' : 'de suelo'}, hitbox ${d.rx * 2}×${d.ry * 2} px (≈ ${(d.rx * 2 * 0.025).toFixed(1)}×${(d.ry * 2 * 0.025).toFixed(1)} u) · código \`e-*.js\` id \`${id}\``,
    '', `**Pregunta al jugador:** ${d.question}`, '', '**Movimientos**', '', ...d.moves.map(m => `- **${m.name}** — ${m.desc}`),
    '', `**Combina con:** ${d.combos || '—'}`, '', `**Arte (idea, sin decidir):** ${d.art || '—'}`].join('\n');
  const aj = ['| Campo | Valor | Rango | Qué es |', '|---|---:|---|---|',
    ...Object.entries(d.tuning).map(([k, v]) => `| \`${k}\` | ${v[0]} | ${v[1]} – ${v[2]} (paso ${v[3]}) | ${v[4]} |`)].join('\n');
  let txt = fs.existsSync(f) ? fs.readFileSync(f, 'utf8') :
    `# ${d.name}\n\nEstado: **prototipo web, sin probar por el usuario** · jugable: \`docs/bocetos/enemigos/jugable.html\` (Duelo ▸ ${d.name})\n\n` +
    `## Diseño\n<!-- gen:diseno -->\n<!-- /gen:diseno -->\n\n## Ajustes (\`G.tuning.foe_${id}\`, px y s; px × 0,025 = unidades)\n<!-- gen:ajustes -->\n<!-- /gen:ajustes -->\n\n` +
    `## Decisiones y feedback\n\n- ${new Date().toISOString().slice(0, 10)} · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.\n`;
  txt = block('ajustes', aj, block('diseno', diseno, txt));
  fs.writeFileSync(f, txt);
}
// tabla de la plantilla y de los encuentros en el README
const readme = path.join(out, 'README.md');
if (fs.existsSync(readme)) {
  let txt = fs.readFileSync(readme, 'utf8');
  txt = block('plantilla', ['| Enemigo | Nivel | `id` | Rol | Pregunta al jugador | Ficha |', '|---|---|---|---|---|---|',
    ...Object.entries(FOES).map(([id, d]) => `| ${d.name} | ${d.tier || 'común'} | \`${id}\` | ${d.role} | ${d.question} | [${fileOf[id] || id}.md](${fileOf[id] || id}.md) |`)].join('\n'), txt);
  txt = block('encuentros', ['| Encuentro | Quién | Qué prueba |', '|---|---|---|',
    ...ENCOUNTERS.map(e => `| ${e.name} | ${Object.entries(e.list.reduce((a, s) => (a[FOES[s.type].name] = (a[FOES[s.type].name] || 0) + 1, a), {})).map(([k, n]) => n > 1 ? `${k} ×${n}` : k).join(', ')} | ${e.desc} |`)].join('\n'), txt);
  fs.writeFileSync(readme, txt);
}
console.log('fichas:', Object.keys(FOES).length, '→', out);
