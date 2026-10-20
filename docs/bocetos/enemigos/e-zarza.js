// e-zarza.js — Zarza (bosque): arbusto de espinas que anda sobre raíces y lanza MUROS de espinas con un hueco. Pregunta: ¿lees
// a qué altura está el hueco (quedarte de pie o saltar) y no la dejas escapar? Acorralada da un latigazo que te aparta.
'use strict';
addFoe({
  id: 'bramble', name: 'Zarza', short: 'Zarza', world: 'bosque', rx: 26, ry: 34, home: 'roam',
  role: 'Tiradora de patrones (muro con hueco)',
  question: 'Mirar dónde está el hueco del muro de espinas (abajo: quédate de pie; en medio o arriba: salta en el momento justo) y perseguirla, que huye; si la acorralas, cuidado con el latigazo.',
  moves: [
    { name: 'Muro de espinas', desc: 'Se tensa y marca varias líneas horizontales hacia ti (de abajo arriba); dispara una espina por cada línea, todas a la vez. Las líneas que faltan son el hueco por donde pasar.' },
    { name: 'Huir', desc: 'Entre ataques se recoloca a ráfagas lejos de ti; si te acercas, huye a saltitos.' },
    { name: 'Latigazo', desc: 'Si te pegas a ella (o la acorralas contra la pared), gira sus ramas en círculo: daño y te aparta lejos. Luego se aleja.' },
  ],
  combos: 'Con Saltasetas: saltar el muro sin caer en un gran salto. Con Atrapamoscas: la liana te deja quieto delante del muro.',
  art: 'Arbusto de zarzas oscuro con forma de bola que camina sobre tres raíces; ramas con espinas a modo de brazos, dos bayas rojas como ojos y hojas verde bosque. Al disparar, las ramas se tensan hacia atrás. Partes: cuerpo (hojas), 2-3 ramas, raíces, ojos. ~1,3×1,7 u.',
  tuning: Object.assign(baseTuning(100, 30, 0.7, 3), {
    keepMin: [260, 50, 700, 10, 'Le gusta estar a (mín.)'], keepMax: [380, 100, 800, 10, 'Le gusta estar a (máx.)'], walk: [80, 0, 250, 5, 'Velocidad'],
    flee: [170, 0, 400, 10, 'Huye si te acercas a menos de'],
    wallWindup: [0.75, 0.2, 2, 0.05, 'Muro: preparación (s)'], lanes: [5, 3, 8, 1, 'Muro: líneas'], laneGap: [36, 20, 60, 1, 'Muro: separación entre líneas'],
    gapLanes: [3, 1, 5, 1, 'Muro: líneas del hueco'], thornSpeed: [420, 150, 900, 10, 'Muro: velocidad'], thornDmg: [9, 0, 40, 1, 'Muro: daño'],
    cooldown: [2.0, 0.3, 6, 0.1, 'Pausa entre muros (s)'],
    whipRange: [90, 30, 200, 5, 'Latigazo: si estás a menos de'], whipWindup: [0.3, 0.1, 1, 0.05, 'Latigazo: preparación (s)'],
    whipR: [95, 30, 200, 5, 'Latigazo: radio'], whipDmg: [8, 0, 40, 1, 'Latigazo: daño'], whipPush: [620, 0, 1200, 10, 'Latigazo: empujón'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    const whip = () => { if (!canStart(e)) return false; begin(e, 'Latigazo'); go(e, 'whipW'); return true; };
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        if (ad < t.whipRange && Math.abs(p.y - (e.y + e.foot)) < 80 && whip()) break;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.walk, burst: 1.9, flee: t.flee, hop: 0.25, onCornered: whip });
        if (e.st !== 'roam' || e.cdT > 0 || !canStart(e)) break;
        begin(e, 'Muro de espinas'); go(e, 'wallW');
        e.wdir = Math.sign(dx) || e.face; e.gap = Math.floor(Math.random() * (t.lanes - t.gapLanes + 1));
        for (let i = 0; i < t.lanes; i++) if (i < e.gap || i >= e.gap + t.gapLanes) {
          const y = GROUND - 16 - i * t.laneGap;
          warn({ e, kind: 'line', t: t.wallWindup, ax: e.x, ay: y, bx: e.x + e.wdir * 1500, by: y, follow: w => { w.ax = e.x + e.wdir * e.rx; w.bx = w.ax + e.wdir * 1500; w.locked = G.t - w.t0 > t.wallWindup * 0.75; } });
        }
        break;
      case 'wallW':
        e.mx = lerp(e.mx, -e.wdir * 20, Math.min(1, dt * 6)); e.face = e.wdir;
        if (!windup(e, t.wallWindup)) break;
        go(e, 'wall'); release(e);
        for (let i = 0; i < t.lanes; i++) if (i < e.gap || i >= e.gap + t.gapLanes)
          foeShot({ e, atk: 'Muro de espinas', x: e.x + e.wdir * e.rx, y: GROUND - 16 - i * t.laneGap, vx: e.wdir * t.thornSpeed, vy: 0, r: 5, style: 'thorn', dmg: t.thornDmg, life: 4 });
        sfx(k => SFXLIB.whoosh(k, 3000, 1200, 0.18, 0.16));
        break;
      case 'wall':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.5) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      case 'whipW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12));
        if (!windup(e, t.whipWindup)) break;
        go(e, 'whip'); release(e);
        if (circleHitsPlayer(e.x, e.y, t.whipR)) hitP(e, 'Latigazo', t.whipDmg, Math.sign(p.x - e.x || 1) * t.whipPush, -320);
        { const x = e.x, y = e.y; addFx(0.45, (ctx, layer, t0) => { if (layer !== 'front') return; const u = (G.t - t0) / 0.45; ctx.save(); ctx.globalAlpha = 0.8 * (1 - u); ctx.strokeStyle = '#6b4a2a'; ctx.lineWidth = 4; ctx.beginPath(); ctx.arc(x, y, t.whipR * (0.6 + 0.4 * u), u * 6, u * 6 + 4.5); ctx.stroke(); ctx.restore(); }); }
        sfx(k => SFXLIB.whoosh(k, 2200, 800, 0.2, 0.2));
        break;
      case 'whip':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.35) { go(e, 'roam'); e.roamX = undefined; groundHop(e, -Math.sign(dxP(e) || 1) * 220, 240); e.cdT = Math.max(e.cdT, 0.6); }
        break;
    }
  },
  draw(ctx, e, P) {
    const tense = e.st === 'wallW' ? e.tell : 0, spin = e.st === 'whip' ? e.stT * 20 : e.st === 'whipW' ? e.tell * 2 : 0;
    for (let i = 0; i < 3; i++) { const lx = -12 + i * 12, sw = Math.sin(e.wp * 1.3 + i * 2) * (Math.abs(e.mx) > 10 ? 6 : 0); E_line(ctx, P, [[lx, 10], [lx + sw - 4, 22], [lx + sw, e.ry]], 4, WOOD.barkD); }
    // ramas con espinas (se tensan hacia atrás al preparar el muro; giran en el latigazo)
    for (let i = 0; i < 3; i++) {
      const a = (spin ? spin + i * TAU / 3 : -0.6 + i * 0.6 + Math.PI * (tense * 0.9)), len = spin ? 46 : 30;
      const pts = [[0, -6], [Math.cos(a) * len * 0.5, -6 + Math.sin(a) * len * 0.5], [Math.cos(a) * len, -6 + Math.sin(a) * len]];
      E_line(ctx, P, pts, 3, WOOD.bark);
      for (let k = 1; k < 3; k++) E_poly(ctx, P, [[pts[k][0] - 2, pts[k][1]], [pts[k][0] + Math.cos(a + 1.6) * 7, pts[k][1] + Math.sin(a + 1.6) * 7], [pts[k][0] + 2, pts[k][1]]], '#c9e08a');
    }
    for (const [x, y, r, c] of [[-8, -8, 18, '#1f4a2a'], [8, -12, 17, WOOD.leaf], [0, 4, 20, '#264f30'], [-4, -18, 13, '#2f6b45']]) E_ell(ctx, P, x, y, r, r * 0.85, c);
    E_eye(ctx, P, e, 6, -10, 2.8); E_eye(ctx, P, e, 14, -8, 2.4);
  },
});
