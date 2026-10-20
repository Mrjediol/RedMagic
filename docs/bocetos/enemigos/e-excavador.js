// e-excavador.js — Excavador: viaja bajo tierra (intocable), sale debajo de ti y escupe rocas. Pregunta: ¿sigues moviéndote
// mientras te persigue el montículo, y castigas cuando sale y cuando vuelve a hundirse?
'use strict';
addFoe({
  id: 'burrower', name: 'Excavador', short: 'Excav.', rx: 26, ry: 36, home: 'out',
  role: 'Emboscador subterráneo',
  question: 'No quedarte quieto: el montículo te persigue (más lento que tú corriendo) y sale justo debajo. Castigarle cuando está fuera y al hundirse; las plataformas te salvan de la salida.',
  moves: [
    { name: 'Túnel', desc: 'Bajo tierra no se le puede dar. Se ve un montículo de tierra que avanza hacia ti.' },
    { name: 'Emerger', desc: 'Se para debajo de ti: grietas y zona del ancho de su cuerpo en el suelo; sale disparado hacia arriba y te lanza por los aires. La columna de tierra atraviesa las plataformas: subirte no te salva (también se marca la plataforma).' },
    { name: 'Escupitajo', desc: 'Ya fuera, escupe 3 rocas en parábola alrededor de ti (cada una marca dónde cae).' },
    { name: 'Hundirse', desc: 'Al rato se vuelve a meter: tarda un poco (ventana de castigo). Un golpe fuerte mientras está fuera lo aturde; mucho daño mientras se hunde lo interrumpe y se queda fuera.' },
  ],
  combos: 'Con Tejedora o Artilleros: tienes que moverte por dos motivos a la vez. En una arena con plataformas, menos peligroso.',
  art: 'Gusano o topo segmentado con mandíbulas de cristal que asoma erguido de un agujero. Gusano de raíz, topo de piedra.',
  tuning: Object.assign(baseTuning(160, 45, 0.2, 4), {
    tunnelSpeed: [190, 50, 500, 5, 'Túnel: velocidad'], maxTunnel: [4, 0.5, 10, 0.5, 'Túnel: sale como mucho a los (s)'],
    emergeWindup: [0.65, 0.1, 2, 0.05, 'Emerger: aviso (s)'], emergeR: [34, 10, 120, 1, 'Emerger: radio'], emergeDmg: [16, 0, 60, 1, 'Emerger: daño'],
    emergeReach: [230, 40, 340, 5, 'Emerger: altura de la columna (atraviesa plataformas)'],
    outTime: [2.6, 0.5, 8, 0.1, 'Fuera: tiempo (s)'], spitWindup: [0.5, 0.1, 2, 0.05, 'Escupitajo: preparación (s)'],
    spitN: [3, 1, 7, 1, 'Escupitajo: rocas'], spitSpread: [70, 0, 250, 5, 'Escupitajo: separación'], spitFlight: [0.9, 0.3, 2.5, 0.05, 'Escupitajo: vuelo (s)'],
    spitDmg: [10, 0, 60, 1, 'Escupitajo: daño'], digWindup: [0.45, 0.1, 2, 0.05, 'Hundirse: tiempo (s, castigo)'], heavyStun: [1.2, 0, 4, 0.1, 'Golpe fuerte fuera: aturdido (s)'],
  }),
  init(e) { e.outT = 1.2; e.spat = true; e.rise = 1; e.stunUsed = false; },
  think(e, dt) {
    const t = FT(e), p = G.player;
    switch (e.st) {
      case 'under':
        e.hx = clamp(e.hx + clamp(p.x - e.hx, -1, 1) * Math.min(Math.abs(p.x - e.hx), t.tunnelSpeed * dt), ARENA.x0 + e.rx, ARENA.x1 - e.rx);
        if (Math.random() < dt * 4) dustPuff(e.hx, GROUND);
        if ((Math.abs(p.x - e.hx) < 24 || e.stT > t.maxTunnel) && canStart(e)) { begin(e, 'Emerger'); go(e, 'emergeW'); warn({ e, kind: 'circle', x: e.hx, r: t.emergeR, t: t.emergeWindup });
          const sy = surfaceUnder(e.hx, p.y); if (sy < GROUND && GROUND - sy < t.emergeReach) warn({ e, kind: 'circle', x: e.hx, y: sy + 2, r: t.emergeR, t: t.emergeWindup }); }
        break;
      case 'emergeW':
        if (!windup(e, t.emergeWindup)) break;
        unhide(e, e.hx, GROUND - e.foot); go(e, 'emerge'); release(e); e.rise = 0; e.outT = 0; e.spat = false; e.stunUsed = false;
        if (Math.abs(p.x - e.x) < t.emergeR + 12 && p.y > GROUND - t.emergeReach) hitP(e, 'Emerger', t.emergeDmg, Math.sign(p.x - e.x || 1) * 150, -640);
        { const x = e.x, h = t.emergeReach, w = t.emergeR; addFx(1.3, (ctx, layer, t0) => debris(ctx, G.t, t0, x, layer, { n: 10, c1: '#5a4632', c2: '#8a6a52', seed: 13 }));
          addFx(0.6, (ctx, layer, t0) => { if (layer !== 'front') return; const u = G.t - t0, k = u < 0.12 ? u / 0.12 : 1 - seg(u, 0.25, 0.6); ctx.save(); ctx.globalAlpha = 0.85 * k; ctx.fillStyle = '#6b5442'; ctx.beginPath(); ctx.moveTo(x - w * 0.7, GROUND); ctx.lineTo(x - w * 0.15, GROUND - h * k); ctx.lineTo(x + w * 0.15, GROUND - h * k * 0.92); ctx.lineTo(x + w * 0.7, GROUND); ctx.closePath(); ctx.fill(); ctx.fillStyle = '#9a7c5e'; ctx.fillRect(x - 3, GROUND - h * k, 6, h * k); ctx.restore(); }); }
        G.shake += 6; sfx(k => { const t0 = nowT(); vThump(t0, 70, 30, 0.35 * k, 0.7, 3); vNoise(t0, 0.4 * k, 0.25, 'lowpass', 1500, 150, 0.8, { att: 0.005 }); });
        break;
      case 'emerge':
        e.phase = 'active'; e.rise = clamp(e.stT / 0.3);
        if (e.stT > 0.3) { go(e, 'out'); recover(e); }
        break;
      case 'out':
        faceTo(e, p.x); e.outT += dt; e.rise = 1;
        if (!e.spat && e.outT > 0.5 && canStart(e)) { begin(e, 'Escupitajo'); go(e, 'spitW'); break; }
        if (e.outT > t.outTime) go(e, 'digW');
        break;
      case 'spitW':
        faceTo(e, p.x); e.outT += dt;
        if (!windup(e, t.spitWindup)) break;
        release(e); e.spat = true;
        for (let i = 0; i < t.spitN; i++) {
          const tx = clamp(p.x + (i - (t.spitN - 1) / 2) * t.spitSpread, ARENA.x0 + 10, ARENA.x1 - 10), ty = surfaceUnder(tx, p.y);
          lobShot(e.x + e.face * 10, e.y - e.ry + 8, tx, ty, t.spitFlight + i * 0.08, { e, atk: 'Escupitajo', dmg: t.spitDmg, r: 6, style: 'rock', warnR: 22,
            onLand: (x, y) => { foeBlast(e, x, y - 4, 22, t.spitDmg, 'Escupitajo', { pal: PAL.wind }); } });
        }
        go(e, 'out'); e.busy = false;
        break;
      case 'digW':
        e.rise = 1 - clamp(e.stT / t.digWindup);
        if (windup(e, t.digWindup)) { e.tell = 0; hide(e); go(e, 'under'); dustPuff(e.hx, GROUND); }
        break;
    }
  },
  onInterrupt(e) { go(e, 'out'); e.outT = Math.min(e.outT, FT(e).outTime - 1.2); e.rise = 1; },
  onHurt(e, d, o) {
    if (o.heavy && !e.stunUsed && (e.st === 'out' || e.st === 'spitW')) { e.stunUsed = true; e.stunT = Math.max(e.stunT, FT(e).heavyStun); e.outT -= FT(e).heavyStun; say(e, '¡ATURDIDO!', '#fff3b0', true); }
  },
  drawHidden(ctx, e) {
    if (e.st === 'emergeW') { const k = e.tell; ctx.save(); ctx.translate(e.hx + Math.sin(G.t * 60) * 2 * k, GROUND); ctx.fillStyle = '#5a4632'; ctx.beginPath(); ctx.ellipse(0, 0, 22 + 10 * k, 8 + 6 * k, 0, Math.PI, TAU); ctx.fill(); ctx.restore(); return; }
    ctx.save(); ctx.translate(e.hx, GROUND);
    ctx.fillStyle = '#5a4632'; ctx.beginPath(); ctx.ellipse(0, 0, 22, 8, 0, Math.PI, TAU); ctx.fill();
    ctx.fillStyle = '#7a5e44'; for (let i = 0; i < 3; i++) { ctx.beginPath(); ctx.arc(-10 + i * 10, -5 - Math.abs(Math.sin(G.t * 8 + i)) * 4, 3, 0, TAU); ctx.fill(); }
    ctx.restore();
  },
  draw(ctx, e, P) {
    const k = e.rise === undefined ? 1 : e.rise, sway = Math.sin(G.t * 2.5 + e.id) * 4;
    ctx.save(); ctx.beginPath(); ctx.rect(-80, -220, 160, 220 + e.ry); ctx.clip();
    ctx.translate(0, (1 - k) * (e.ry * 2 + 10));
    const lean = e.st === 'spitW' ? -e.tell * 0.25 : G.t - e.relT < 0.25 ? 0.2 : 0;
    ctx.translate(0, e.ry); ctx.rotate(lean); ctx.translate(0, -e.ry);
    for (let i = 0; i < 5; i++) { const y = e.ry - 6 - i * 13, x = sway * (i / 5); E_ell(ctx, P, x, y, 20 - i * 1.2, 9, i % 2 ? '#6b4f40' : '#8a6a58'); }
    const hx = sway, hy = -e.ry + 6;
    E_ell(ctx, P, hx + 2, hy, 17, 13, '#8a6a58');
    const open = e.st === 'spitW' ? e.tell : 0.2;
    E_poly(ctx, P, [[hx + 12, hy - 4], [hx + 28, hy - 8 - open * 8], [hx + 16, hy + 1]], '#7fe8ff');
    E_poly(ctx, P, [[hx + 12, hy + 4], [hx + 28, hy + 8 + open * 8], [hx + 16, hy + 1]], '#bff6ff');
    E_eye(ctx, P, e, hx + 8, hy - 6, 2.6);
    ctx.restore();
    E_ell(ctx, P, 0, e.ry, 26, 6, '#3a2c20');
  },
});
