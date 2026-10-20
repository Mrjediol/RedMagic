// e-acechador.js — Acechador: cazador ágil que va en manada. Pregunta: ¿lees la finta antes de gastar el dash, y vigilas
// a los dos lados cuando te rodean?
'use strict';
addFoe({
  id: 'stalker', name: 'Acechador', short: 'Acech.', rx: 30, ry: 20, home: 'prowl', platforms: true,
  role: 'Cazador ágil de manada',
  question: 'Leer la finta (el agachado no siempre acaba en salto) y esquivar por debajo del arco; no dejarse rodear.',
  moves: [
    { name: 'Abalanzarse', desc: 'Se agacha (cuerpo naranja, ojos blancos) y salta en arco a donde vas a estar (adelanta tu movimiento). Marca en el suelo de dónde va a caer. Al caer se queda un momento recuperándose: ahí se le castiga.' },
    { name: 'Finta', desc: 'A veces el mismo agachado acaba en un salto atrás. Castiga gastar el dash por reflejo.' },
    { name: 'Rodear', desc: 'Se mueve a tu alrededor a media distancia y salta atrás si te pegas. En manada se reparten a los dos lados (uno a cada lado) y se turnan para atacar.' },
  ],
  combos: 'Manada de 3: siempre hay uno a tu espalda. Con Tejedora: el ralentizado de la telaraña hace que el salto acierte.',
  art: 'Cuadrúpedo esbelto y bajo, orejas puntiagudas, cola larga, púas de cristal en el lomo. Lobo de escarcha, zorro de musgo.',
  tuning: Object.assign(baseTuning(90, 25, 1.1, 3), {
    ringMin: [170, 40, 500, 10, 'Rodea a: distancia mínima'], ringMax: [280, 60, 700, 10, 'Rodea a: distancia máxima'],
    run: [230, 50, 500, 5, 'Velocidad corriendo'], crouch: [0.45, 0.1, 1.5, 0.05, 'Abalanzarse: preparación (s)'],
    leapH: [110, 20, 300, 5, 'Abalanzarse: altura del arco'], lead: [0.25, 0, 1, 0.05, 'Abalanzarse: adelanta tu movimiento (s)'],
    leapDmg: [12, 0, 60, 1, 'Abalanzarse: daño'], landRecover: [0.6, 0, 2, 0.05, 'Al caer: recuperación (s, castigo)'],
    feint: [0.3, 0, 1, 0.05, 'Probabilidad de finta'], hopV: [330, 100, 700, 10, 'Salto atrás: impulso'],
    flank: [1, 0, 1, 1, 'Manada: repartirse a los dos lados (1 = sí)'], cooldown: [1.0, 0, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'prowl': {
        faceTo(e, p.x); e.cdT -= dt;
        const pack = foes().filter(o => o.type === e.type && !o.dead), idx = pack.indexOf(e);
        const side = t.flank && pack.length > 1 ? (idx % 2 ? 1 : -1) : (Math.sign(e.x - p.x) || 1);
        const mid = (t.ringMin + t.ringMax) / 2 + Math.sin(G.t * 1.3 + e.id * 2) * (t.ringMax - t.ringMin) * 0.45;
        let tx = p.x + side * mid; if (tx < ARENA.x0 + 40 || tx > ARENA.x1 - 40) tx = p.x - side * mid;
        if (e.onGround) walkTo(e, tx, t.run, dt, 5);
        if (e.onGround && ad < t.ringMin * 0.55 && e.stT > 0.4) { stalkerHop(e, -Math.sign(dx || 1)); break; }
        if (e.cdT <= 0 && e.onGround && ad > t.ringMin * 0.5 && ad < t.ringMax + 80 && canStart(e)) { begin(e, 'Abalanzarse'); go(e, 'crouch'); e.isFeint = Math.random() < t.feint; }
        break;
      }
      case 'crouch':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); faceTo(e, p.x);
        if (!windup(e, t.crouch)) break;
        if (e.isFeint) { e.busy = false; stalkerHop(e, -Math.sign(dx || 1)); break; }
        {
          const tx = clamp(p.x + p.vx * t.lead, ARENA.x0 + e.rx, ARENA.x1 - e.rx);
          const feetE = e.y + e.foot, feetT = surfaceUnder(tx, p.y), top = Math.min(feetE, feetT) - t.leapH;
          const vy0 = -Math.sqrt(2 * GRAV * (feetE - top)), T = -vy0 / GRAV + Math.sqrt(2 * Math.max(0, feetT - top) / GRAV);
          e.vy = vy0; e.mx = (tx - e.x) / T; e.onGround = false; e.y -= 1;
          go(e, 'leap'); release(e); e.hitP = false;
          warn({ e, kind: 'circle', x: tx, y: feetT + 2, r: e.rx * 1.1, t: T });
          sfx(k => SFXLIB.whoosh(k, 1600, 500, 0.25, 0.14));
        }
        break;
      case 'leap':
        e.phase = 'active'; e.face = Math.sign(e.mx) || e.face;
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Abalanzarse', t.leapDmg, Math.sign(e.mx || 1) * 380, -260)) e.hitP = true;
        if (e.onGround && e.stT > 0.08) { go(e, 'land'); recover(e); dustPuff(e.x, e.y + e.foot); }
        break;
      case 'land':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 12));
        if (e.stT > t.landRecover) { go(e, 'prowl'); e.cdT = t.cooldown * (0.8 + Math.random() * 0.4); }
        break;
      case 'hop':
        if (e.onGround && e.stT > 0.1) { go(e, 'prowl'); e.cdT = Math.max(e.cdT, 0.35); }
        break;
    }
  },
  draw(ctx, e, P) {
    const air = !e.onGround, cr = e.st === 'crouch' ? e.tell : e.st === 'land' ? Math.max(0, 0.6 - e.stT) : 0;
    ctx.save(); ctx.translate(0, cr * 7); ctx.scale(1 + cr * 0.08, 1 - cr * 0.2);
    if (air) ctx.rotate(clamp(e.vy / 1400, -0.35, 0.35));
    // patas (en el aire: estiradas)
    for (let i = 0; i < 4; i++) {
      const lx = [-20, -10, 12, 22][i], sw = air ? (i < 2 ? -10 : 10) : Math.sin(e.wp * 1.4 + i * Math.PI) * (Math.abs(e.mx) > 20 ? 8 : 0);
      E_line(ctx, P, [[lx, 4], [lx + sw * 0.6, 12], [lx + sw, e.ry]], 4.5, i % 2 ? '#24352c' : '#2e4438');
    }
    E_line(ctx, P, [[-26, -2], [-40, -10], [-52, -6 + Math.sin(G.t * 6 + e.id) * 4]], 6, '#2e4438');
    E_ell(ctx, P, 0, 0, 30, 13, '#2e4438');
    E_ell(ctx, P, 2, -3, 26, 10, '#3f5a4c');
    for (let i = 0; i < 3; i++) E_poly(ctx, P, [[-14 + i * 10, -10], [-10 + i * 10, -22 - (i === 1 ? 4 : 0)], [-6 + i * 10, -10]], i === 1 ? '#bff6ff' : '#7fe8ff');
    // cabeza
    E_poly(ctx, P, [[22, -12], [46, -4], [44, 2], [20, 4]], '#3f5a4c');
    E_poly(ctx, P, [[24, -12], [28, -26], [32, -11]], '#2e4438');
    E_eye(ctx, P, e, 33, -6, 2.6);
    ctx.restore();
  },
});
function stalkerHop(e, dir) { const t = FT(e); go(e, 'hop'); e.vy = -t.hopV; e.mx = dir * 260; e.onGround = false; e.y -= 1; }
