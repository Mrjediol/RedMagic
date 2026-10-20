// tools/dps.cjs — mide el daño de cada arma (base y cada evolución con sus 3 mejoras) contra el mismo grupo de dianas,
// sola y en pareja con el Abanico (cambiando cada 2 s), con un «bot» que apunta al enemigo más cercano y mantiene 0,8 s.
// Escribe docs/armas/dps.md (tabla para equilibrar; no es un juicio de diversión: las armas de apoyo salen bajas solas).
// Uso: node docs/bocetos/habilidades/tools/dps.cjs [segundos = 10]
const fs = require('fs'), path = require('path');
const sim = require('./sim.cjs')(), G = sim.G, W = sim.api.WEAPONS;
const SECS = +process.argv[2] || 10;
const ENEMIES = [{ x: 520, kind: 'ground' }, { x: 580, kind: 'ground' }, { x: 650, kind: 'walker', x0: 600, x1: 760 }, { x: 600, kind: 'flyer', y: 170 }, { x: 760, kind: 'ground' }];
function bot(partner) {
  return t => {
    const p = G.player, e = G.enemies.filter(e => !e.dead).sort((a, b) => Math.abs(a.x - p.x) - Math.abs(b.x - p.x))[0];
    if (e) sim.aim(e.x, e.y);
    const ph = t % 1.1;
    if (ph < 1 / 60 + 1e-6) sim.press();
    if (Math.abs(ph - 0.8) < 1 / 120) sim.release();
    if (partner && Math.abs(t % 2 - 1.95) < 1 / 120) { sim.release(); sim.swap((G.cur + 1) % 2); }
    p.x = 330; p.vx = 0;                                       // quieto a una distancia media
  };
}
function measure(slot, partner) {
  sim.setup({ slots: [slot, partner ? { w: 'fan' } : { w: slot.w === 'fan' ? 'star' : 'fan' }], enemies: ENEMIES, px: 330, hpMul: 30 });
  sim.run(SECS, bot(partner));
  return Math.round(sim.dmg() / SECS);
}
const rows = [];
for (const id in W) {
  const d = W[id], cfgs = [{ name: 'base', arch: null, ups: {} }];
  for (const a of d.archetypes || []) cfgs.push({ name: a.name, arch: a.id, ups: Object.fromEntries(a.ups.map(u => [u.id, true])) });
  for (const c of cfgs) {
    const slot = { w: id, arch: c.arch, ups: c.ups };
    rows.push({ arma: d.name + (d.fresh ? ' ✦' : ''), conf: c.name, solo: measure(slot, false), pareja: id === 'fan' ? '—' : measure(slot, true) });
  }
  process.stdout.write('.');
}
const fan = rows.find(r => r.arma.startsWith('Abanico') && r.conf === 'base').solo;
const md = ['# DPS medido (generado por `tools/dps.cjs`)', '',
  `Daño por segundo durante ${SECS} s contra 5 dianas (4 de suelo, 1 voladora; vida ×30 para que no mueran), player quieto a media`,
  'distancia y un bot que apunta al más cercano, pulsa y mantiene 0,8 s cada 1,1 s. **Sola** = sólo esa arma (la otra no se usa);',
  '**pareja** = alternando con el Abanico base cada 2 s (las armas de apoyo y las mejoras «con el arma guardada» se ven aquí).',
  `Referencia: Abanico base sola = ${fan}. Mejoras de evolución: las 3 activas. Sin items. Orientativo: el bot no sabe usar`,
  'paradas, cargas largas, recoger la jabalina ni colocarse; sirve para ver valores muy fuera de rango.', '',
  '| Arma | Configuración | DPS sola | DPS pareja (con Abanico) |', '|---|---|---:|---:|',
  ...rows.map(r => `| ${r.arma} | ${r.conf} | ${r.solo} | ${r.pareja} |`), ''];
fs.writeFileSync(path.join(__dirname, '..', '..', '..', 'armas', 'dps.md'), md.join('\n'));
console.log('\nok docs/armas/dps.md');
