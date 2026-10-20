// e-caparazon.js — Caparazón: lento, con escudo frontal. Pregunta: ¿le rodeas (saltas por encima), le pegas desde arriba
// o con un golpe fuerte, o te quedas dándole al escudo?
'use strict';
addFoe({
  id: 'shell', name: 'Caparazón', short: 'Capar.', rx: 40, ry: 30, home: 'walk',
  role: 'Blindado con escudo frontal',
  question: 'Rodearlo saltando por encima (gira despacio), pegarle por la espalda o desde arriba, o romperle la guardia con un golpe fuerte; y saltar sus ondas.',
  moves: [
    { name: 'Escudo', desc: 'Lo que le llega de frente hace sólo ×0,15 (BLOQUEO). Por la espalda, daño completo. Tarda en girarse cuando le pasas al otro lado. Un golpe fuerte de frente le rompe la guardia: queda vulnerable un rato.' },
    { name: 'Golpe de escudo', desc: 'Si estás delante y cerca: retrocede un poco y embiste corto con el escudo. Empuja mucho.' },
    { name: 'Sacudida', desc: 'Cada pocos segundos se levanta (¡enseña la tripa: ahí el escudo no tapa!) y golpea el suelo: dos ondas que corren por el suelo a los dos lados. Hay que saltarlas.' },
  ],
  combos: 'Delante de Artilleros o una Colmena: tapa a los que disparan. Con Embestidor: dos muros que te encierran.',
  art: 'Tortuga o escarabajo de piedra con musgo, una placa frontal grande (escudo) de cristal-piedra; cabeza pequeña asomando.',
  tuning: Object.assign(baseTuning(220, 60, 0.25, 4), {
    walk: [45, 0, 200, 5, 'Velocidad andando'], keep: [90, 0, 300, 5, 'Se acerca hasta'], turnDelay: [0.9, 0, 3, 0.05, 'Tarda en girarse (s)'],
    frontMul: [0.15, 0, 1, 0.05, 'Escudo: × daño de frente'], breakTime: [1.6, 0, 5, 0.1, 'Guardia rota: duración (s)'],
    breakVuln: [1.3, 1, 3, 0.05, 'Guardia rota: × daño recibido'],
    bashRange: [150, 40, 400, 5, 'Golpe de escudo: si estás a menos de'], bashWindup: [0.55, 0.1, 2, 0.05, 'Golpe de escudo: preparación (s)'],
    bashDist: [120, 20, 300, 5, 'Golpe de escudo: avance'], bashDmg: [14, 0, 60, 1, 'Golpe de escudo: daño'],
    quakeEvery: [6, 1, 20, 0.5, 'Sacudida: cada (s)'], quakeWindup: [0.8, 0.2, 2, 0.05, 'Sacudida: preparación (s)'],
    waveSpeed: [330, 80, 800, 10, 'Onda: velocidad'], waveDist: [520, 50, 1400, 10, 'Onda: recorrido'], waveH: [34, 10, 90, 1, 'Onda: altura'],
    waveDmg: [12, 0, 60, 1, 'Onda: daño'], cooldown: [1.2, 0, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  init(e) { e.turnT = 0; e.quakeT = 2; e.brokenT = 0; },
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx), side = Math.sign(dx) || e.face;
    const turning = () => { if (side !== e.face) { e.turnT += dt; if (e.turnT > t.turnDelay) { e.face = side; e.turnT = 0; } } else e.turnT = 0; };
    switch (e.st) {
      case 'walk':
        turning(); e.cdT -= dt; e.quakeT += dt;
        if (side === e.face && ad > t.keep) walkDir(e, side, t.walk, dt, 3); else e.mx = lerp(e.mx, 0, Math.min(1, dt * 6));
        if (!canStart(e)) break;
        if (e.quakeT > t.quakeEvery && ad < 650) {
          begin(e, 'Sacudida'); go(e, 'quakeW'); e.quakeT = 0;
          for (const d of [-1, 1]) warn({ e, kind: 'arrow', x: e.x + d * e.rx, dir: d, len: 70, h: t.waveH, t: t.quakeWindup });
        } else if (side === e.face && ad < t.bashRange && e.cdT <= 0) { begin(e, 'Golpe de escudo'); go(e, 'bashW'); }
        break;
      case 'bashW':
        e.mx = lerp(e.mx, -e.face * 25, Math.min(1, dt * 6));
        if (windup(e, t.bashWindup)) { go(e, 'bash'); release(e); e.hitP = false; sfx(k => SFXLIB.whoosh(k, 1200, 400, 0.18, 0.18)); }
        break;
      case 'bash':
        e.phase = 'active'; e.mx = e.face * t.bashDist / 0.2;
        if (!e.hitP && rectHitsPlayer(e.x + e.face * e.rx * 0.4, e.y - e.ry - 10, e.x + e.face * (e.rx + 26), e.y + e.ry) && hitP(e, 'Golpe de escudo', t.bashDmg, e.face * 560, -260)) e.hitP = true;
        if (e.stT > 0.2) { go(e, 'bashRec'); recover(e); }
        break;
      case 'bashRec':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (e.stT > 0.5) { go(e, 'walk'); e.cdT = t.cooldown; }
        break;
      case 'quakeW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (!windup(e, t.quakeWindup)) break;
        go(e, 'quake'); release(e);
        for (const d of [-1, 1]) groundWave(e, e.x + d * e.rx, d, { speed: t.waveSpeed, dist: t.waveDist, h: t.waveH, dmg: t.waveDmg, atk: 'Sacudida', pal: { a: '#5d6b5a', b: '#9fb3c0' } });
        { const x = e.x; addFx(1.2, (ctx, layer, t0) => groundImpact(ctx, G.t, layer, t0, x, 0.8, PAL.wind, { seed: 6 })); }
        G.shake += 7; sfx(k => { const t0 = nowT(); vThump(t0, 80, 30, 0.45 * k, 0.8, 3); });
        break;
      case 'quake':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.7) { go(e, 'walk'); e.cdT = t.cooldown; }
        break;
    }
  },
  onDamage(e, o) {
    const t = FT(e);
    if (e.st === 'quakeW' || e.st === 'quake' || e.brokenT > G.t || e.frozenT > 0 || o.dot || o.elem === 'burnTick') return;
    const from = o.kx ? -Math.sign(o.kx) : (Math.sign(G.player.x - e.x) || 1);   // el empuje apunta lejos del que golpea
    if (from !== e.face) return;                                                  // por la espalda: daño completo
    if (o.heavy) { e.brokenT = G.t + t.breakTime; makeVuln(e, t.breakVuln, t.breakTime); e.stunT = Math.max(e.stunT, 0.5); say(e, '¡GUARDIA ROTA!', '#ffd27a', true); sfx(k => SFXLIB.glass(k, 1)); return; }
    o.dmg = (o.dmg || 0) * t.frontMul; o.kx = 0; o.ky = 0; o.launch = 0;
    say(e, 'BLOQUEO', '#c8d0dc');
    const x = e.x + e.face * (e.rx + 4), y = e.y - 6;
    addFx(0.4, (ctx, layer, t0) => sparks(ctx, G.t, t0, x, y, layer, { n: 6, pal: PAL.wind, sp: 220, seed: Math.floor(G.t * 10), len: 8, life: 0.3 }));
  },
  draw(ctx, e, P) {
    const rear = e.st === 'quakeW' ? e.tell : e.st === 'quake' ? Math.max(0, 1 - e.stT * 7) : 0, broken = e.brokenT > G.t;
    const turn = e.turnT > 0 ? Math.sin(G.t * 30) * 2 : 0;
    ctx.save(); ctx.translate(-34, e.ry); ctx.rotate(-rear * 0.38); ctx.translate(34, -e.ry); ctx.translate(turn, 0);
    for (let i = 0; i < 4; i++) { const lx = [-28, -12, 10, 26][i], sw = Math.sin(e.wp * 0.8 + i * Math.PI) * (Math.abs(e.mx) > 10 ? 4 : 0); E_line(ctx, P, [[lx, 10], [lx + sw, e.ry]], 10, '#3f4a3d'); }
    if (rear > 0.05) E_ell(ctx, P, 6, 12, 32, 12, '#c9b48a');                      // tripa (la ventana)
    E_ell(ctx, P, 0, 0, 40, 28, '#3f4a3d');
    E_ell(ctx, P, -3, -4, 36, 24, '#5d6b5a');
    for (const [x, y] of [[-18, -12], [0, -16], [16, -10], [-8, 2], [10, 4]]) E_ell(ctx, P, x, y, 9, 6, '#6f7e6a');
    for (const [x, y, r] of [[-24, -18, 7], [6, -24, 8]]) E_ell(ctx, P, x, y, r, r * 0.5, '#4f7a3e');
    E_ell(ctx, P, 40, 14, 10, 8, '#7a8a6a');
    E_eye(ctx, P, e, 44, 12, 2.6);
    // escudo frontal
    ctx.save(); ctx.translate(42, -2); if (broken) ctx.rotate(0.5);
    E_poly(ctx, P, [[-5, -30], [9, -24], [12, 22], [-4, 28]], broken ? '#6a7682' : '#9fb3c0');
    E_line(ctx, P, [[9, -24], [12, 22]], 2.5, broken ? '#8a96a2' : '#7fe8ff');
    if (broken) E_line(ctx, P, [[0, -14], [4, -2], [-1, 6], [5, 16]], 1.6, '#2a3038');
    ctx.restore();
    ctx.restore();
  },
});
