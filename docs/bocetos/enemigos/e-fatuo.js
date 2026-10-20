// e-fatuo.js — Fatuo: kamikaze flotante que se hincha y explota, también contra los demás enemigos. Pregunta: ¿lo matas
// lejos, o lo usas como bomba (lo empujas hacia otros, lo dejas explotar entre ellos)?
'use strict';
const WISP_PAL = { core: '#ffffff', light: '#d8fff0', mid: '#8af5c8', deep: '#2fb894', g: [120, 255, 200] };
addFoe({
  id: 'wisp', name: 'Fatuo', short: 'Fatuo', rx: 16, ry: 16, fly: true, home: 'drift',
  role: 'Kamikaze flotante (bomba de doble filo)',
  question: 'Matarlo antes de que llegue, apartarte cuando se hincha, o aprovecharlo: su explosión también daña (mucho) a los otros enemigos.',
  moves: [
    { name: 'Deriva', desc: 'Flota hacia ti despacio haciendo eses. Los empujones le afectan mucho (Ráfaga, Égida, explosiones lo mandan lejos).' },
    { name: 'Hincharse', desc: 'A poca distancia se hincha (se pone naranja y parpadea cada vez más rápido; anillo del tamaño exacto de la explosión) y revienta. No se puede interrumpir.' },
    { name: 'Doble filo', desc: 'Si lo matas mientras se hincha, revienta al momento donde esté. Si lo matas antes, se apaga sin explotar. Congelado tampoco explota. La explosión daña a los demás enemigos.' },
  ],
  combos: 'Con Embestidor o Caparazón: déjalo explotar junto a ellos. Con Colmena: un fatuo en el enjambre limpia zánganos.',
  art: 'Llama o esfera de luz con cola, ojos sencillos; al hincharse cambia de color. Fuego fatuo, espora luminosa.',
  tuning: Object.assign(baseTuning(35, 0, 1.6, 0), {
    speed: [95, 20, 300, 5, 'Velocidad'], zigzag: [50, 0, 150, 5, 'Eses: amplitud'], triggerR: [90, 30, 250, 5, 'Se hincha a esta distancia'],
    fuse: [0.9, 0.2, 3, 0.05, 'Hincharse: tiempo hasta reventar (s)'], blastR: [95, 30, 250, 5, 'Explosión: radio'],
    blastDmg: [22, 0, 80, 1, 'Explosión: daño al player'], foeDmg: [30, 0, 200, 1, 'Explosión: daño a otros enemigos'],
    chain: [1, 0, 1, 1, 'Revienta si lo matas hinchándose (1 = sí)'],
  }),
  think(e, dt) {
    const t = FT(e), c = pCenter(), dx = c.x - e.x, dy = c.y - e.y, d = Math.hypot(dx, dy) || 1;
    switch (e.st) {
      case 'drift': {
        const s = Math.sin(G.t * 2.4 + e.id) * t.zigzag;
        steer(e, c.x - dy / d * s, c.y - dx / d * s * 0.4 - 10, t.speed, dt, 2); faceTo(e, c.x);
        if (d < t.triggerR && !G.es.frozenAI) { go(e, 'fuse'); warn({ e, kind: 'ring', x: e.x, y: e.y, r: t.blastR, t: t.fuse, follow: w => { w.x = e.x; w.y = e.y; } }); sfx(k => SFXLIB.freeze(k, t.fuse)); }
        break;
      }
      case 'fuse':
        steer(e, c.x, c.y - 10, t.speed * 0.35, dt, 2);
        if (windup(e, t.fuse)) { wispBlast(e); selfKill(e); }
        break;
    }
  },
  onDeath(e) {
    const t = FT(e);
    if (e.st === 'fuse' && t.chain && G.t - (e.iceT || -9) > 0.25) { say(e, '¡BUM!', '#ffb35a', true); wispBlast(e); }
    else { const x = e.x, y = e.y; addFx(0.5, (ctx, layer, t0) => sparks(ctx, G.t, t0, x, y, layer, { n: 8, pal: WISP_PAL, sp: 120, seed: 2, len: 6, life: 0.4 })); }
  },
  draw(ctx, e, P) {
    const f = e.st === 'fuse' ? e.tell : 0, s = 1 + f * 0.55;
    const blinkOn = f > 0 && Math.sin(G.t * (12 + 50 * f)) > 0;
    const pal = blinkOn ? HIVE_PAL : WISP_PAL;
    if (!P.mono) glow(ctx, 0, 0, 34 * s, blinkOn ? [255, 140, 50] : WISP_PAL.g, 0.55);
    const tail = -Math.sign(e.mx || 1) * 0.6;
    E_poly(ctx, P, [[-11 * s, -4], [tail * 30 * s - 6, -22 * s + Math.sin(G.t * 9) * 4], [8 * s, -10 * s]], pal.light);
    E_ell(ctx, P, 0, 0, 15 * s, 15 * s, pal.mid);
    E_ell(ctx, P, -2, -2, 9 * s, 9 * s, pal.light);
    E_ell(ctx, P, -2, -2, 4.5 * s, 4.5 * s, '#ffffff');
    if (!P.mono) { ctx.fillStyle = '#123a30'; ctx.beginPath(); ctx.arc(5, -2, 2, 0, TAU); ctx.arc(10, -2, 2, 0, TAU); ctx.fill(); }
  },
});
function wispBlast(e) { const t = FT(e); foeBlast(e, e.x, e.y, t.blastR, t.blastDmg, 'Explosión', { foes: t.foeDmg, pal: PAL.fire }); }
