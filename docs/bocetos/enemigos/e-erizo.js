// e-erizo.js — Erizo de escarcha (mundo de hielo): cuadrúpedo bajo cubierto de púas-carámbano que las dispara al cielo y caen
// sobre ti. Pregunta: ¿lees dónde va a llover (también en tu plataforma), y le pegas cuando se ha quedado pelado, no cuando se eriza?
'use strict';
addFoe({
  id: 'urchin', name: 'Erizo de escarcha', short: 'Erizo', world: 'hielo', rx: 30, ry: 22, home: 'roam',
  role: 'Artillero de suelo: lluvia desde arriba',
  question: 'Salir de las marcas de la lluvia (caen también en tu plataforma: subir no salva) y castigarle cuando se queda sin púas; no pegarte a él cuando se eriza.',
  moves: [
    { name: 'Lluvia de púas', desc: 'Se encoge y dispara sus púas hacia arriba; un momento después caen en varios sitios alrededor de ti (marcas en el suelo o en tu plataforma desde que las dispara).' },
    { name: 'Pelado', desc: 'Tras la lluvia se queda sin púas un rato: recibe más daño (anillo rosa) hasta que le vuelven a crecer.' },
    { name: 'Erizarse', desc: 'Si te pegas a él, saca todas las púas (preparación corta): tocarlo mientras está erizado duele y empuja.' },
    { name: 'Huir y Rodar', desc: 'Entre ataques se recoloca a ráfagas a media distancia. Si te acercas, huye a saltitos. Si lo acorralas contra la pared, se hace bola de púas y rueda a través de ti hasta el otro lado (salta por encima).' },
  ],
  combos: 'Con Colgantes: arriba (carámbanos) y abajo a la vez. Con Morsa: los pinchos del suelo te encierran y llueve.',
  art: 'Erizo o puercoespín de tundra, redondo y bajo, pelo blanco crema sucio; en el lomo un abanico de púas que son carámbanos (azul pálido, translúcidos en la punta). Hocico oscuro, ojos pequeños. Estados: normal, encogido (púas apuntando arriba), pelado (lomo rosado con muñones), erizado (púas largas en todas direcciones). Partes: cuerpo, cabeza, 4 patas, capa de púas (3 estados). ~1,5×1,1 u.',
  tuning: Object.assign(baseTuning(110, 35, 0.8, 4), {
    keepMin: [230, 0, 600, 10, 'Le gusta estar a (mín.)'], keepMax: [360, 50, 700, 10, 'Le gusta estar a (máx.)'], walk: [85, 0, 250, 5, 'Velocidad andando'],
    flee: [180, 0, 400, 10, 'Huye si te acercas a menos de'], rollWindup: [0.35, 0.1, 1.5, 0.05, 'Rodar: preparación (s)'],
    rollSpeed: [430, 100, 900, 10, 'Rodar: velocidad'], rollDist: [420, 100, 1000, 10, 'Rodar: recorrido'], rollDmg: [12, 0, 50, 1, 'Rodar: daño'],
    rainWindup: [0.6, 0.1, 2, 0.05, 'Lluvia: preparación (s)'], quills: [5, 1, 12, 1, 'Lluvia: púas'], spread: [70, 0, 200, 5, 'Lluvia: separación'],
    rainDelay: [1.0, 0.3, 3, 0.05, 'Lluvia: de disparar a caer (s)'], quillR: [18, 6, 60, 1, 'Lluvia: radio de cada púa'], quillDmg: [9, 0, 40, 1, 'Lluvia: daño'],
    bald: [1.8, 0, 5, 0.1, 'Pelado (s)'], baldVuln: [1.5, 1, 3, 0.05, 'Pelado: × daño recibido'],
    bristleRange: [90, 20, 250, 5, 'Erizarse: si estás a menos de'], bristleWindup: [0.25, 0.05, 1, 0.05, 'Erizarse: preparación (s)'],
    bristleTime: [1.2, 0.2, 4, 0.1, 'Erizado (s)'], bristleDmg: [10, 0, 40, 1, 'Erizado: daño al tocarlo'],
    cooldown: [2.0, 0.2, 6, 0.1, 'Pausa entre lluvias (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.walk, burst: 1.8, flee: t.flee, hop: 0.35, onCornered: e => { if (!canStart(e)) return false; begin(e, 'Rodar'); go(e, 'rollW'); return true; } });
        if (e.st !== 'roam') break;
        if (!canStart(e)) break;
        if (ad < t.bristleRange && Math.abs(p.y - (e.y + e.foot)) < 70) { begin(e, 'Erizarse'); go(e, 'bristleW'); }
        else if (e.cdT <= 0) { begin(e, 'Lluvia de púas'); go(e, 'rainW'); }
        break;
      case 'rainW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (!windup(e, t.rainWindup)) break;
        go(e, 'bald'); release(e); e.busy = false; makeVuln(e, t.baldVuln, t.bald);
        for (let i = 0; i < t.quills; i++) {
          const x = clamp(p.x + p.vx * t.rainDelay * 0.4 + (i - (t.quills - 1) / 2) * t.spread + (Math.random() - 0.5) * 20, ARENA.x0 + 10, ARENA.x1 - 10), y = surfaceUnder(x, p.y);
          warn({ kind: 'circle', x, y: y + 2, r: t.quillR, t: t.rainDelay });
          const fall = 0.3; later(e, t.rainDelay - fall, () => foeShot({ e, atk: 'Lluvia de púas', x, y: -20, vx: 0, vy: (y + 20) / fall, r: 5, style: 'icicle', dmg: t.quillDmg, landY: y, pal: ICE.pal,
            onLand: (lx, ly) => foeBlast(e, lx, ly - 4, t.quillR, t.quillDmg, 'Lluvia de púas', { pal: PAL.ice }) }));
        }
        { const x = e.x, y = e.y; addFx(0.6, (ctx, layer, t0) => { if (layer !== 'front') return; const u = G.t - t0; for (let i = 0; i < t.quills; i++) lens(ctx, x - 14 + i * 7, y - 20 - u * 700, -Math.PI / 2 + (i - 2) * 0.08, 14, 2.4, ICE.light, 1 - u / 0.6, layer); }); }
        sfx(k => SFXLIB.whoosh(k, 3200, 1400, 0.2, 0.16));
        break;
      case 'bald':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 6));
        if (e.stT > t.bald) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      case 'rollW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12)); faceTo(e, G.player.x);
        if (windup(e, t.rollWindup)) { go(e, 'roll'); release(e); e.rdir = Math.sign(dxP(e)) || e.face; e.run = 0; e.hitP = false; sfx(k => SFXLIB.whoosh(k, 1200, 500, 0.4, 0.18)); }
        break;
      case 'roll':
        e.phase = 'active'; e.mx = e.rdir * t.rollSpeed; e.face = e.rdir; e.run += t.rollSpeed * dt; e.spin = (e.spin || 0) + e.rdir * dt * 14;
        if (!e.hitP && ellipseHitsPlayer(e, 8) && hitP(e, 'Rodar', t.rollDmg, e.rdir * 380, -320)) e.hitP = true;
        if (Math.random() < dt * 12) dustPuff(e.x, GROUND);
        if (e.run > t.rollDist || (e.wall === e.rdir && e.stT > 0.1)) { go(e, 'roam'); recover(e); e.mx *= 0.3; e.cdT = Math.max(e.cdT, 0.6); e.roamX = undefined; }
        break;
      case 'bristleW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12));
        if (windup(e, t.bristleWindup)) { go(e, 'bristle'); release(e); sfx(k => SFXLIB.glass(k, 0.8)); }
        break;
      case 'bristle':
        e.phase = 'active'; e.mx = 0;
        if (ellipseHitsPlayer(e, 26)) hitP(e, 'Erizarse', t.bristleDmg, Math.sign(G.player.x - e.x || 1) * 420, -300);
        if (e.stT > t.bristleTime) { go(e, 'roam'); recover(e); e.cdT = Math.max(e.cdT, 0.8); }
        break;
    }
  },
  draw(ctx, e, P) {
    if (e.st === 'roll' || e.st === 'rollW') {                                     // bola de púas
      ctx.save(); ctx.translate(0, e.ry - 20 + (e.st === 'rollW' ? 0 : 0)); ctx.rotate(e.spin || 0);
      for (let i = 0; i < 12; i++) { const a = i / 12 * TAU, l = 30 + (i % 2) * 6; E_poly(ctx, P, [[Math.cos(a - 0.18) * 16, Math.sin(a - 0.18) * 16], [Math.cos(a) * l, Math.sin(a) * l], [Math.cos(a + 0.18) * 16, Math.sin(a + 0.18) * 16]], i % 2 ? ICE.light : ICE.pale); }
      E_ell(ctx, P, 0, 0, 18, 18, ICE.fur); E_ell(ctx, P, -4, -4, 10, 10, ICE.furD);
      ctx.restore(); return;
    }
    const curl = e.st === 'rainW' ? e.tell : 0, bald = e.st === 'bald', bristle = e.st === 'bristle' ? 1 : e.st === 'bristleW' ? e.tell : 0;
    for (let i = 0; i < 4; i++) { const lx = [-18, -8, 10, 20][i], sw = Math.sin(e.wp * 1.2 + i * Math.PI) * (Math.abs(e.mx) > 10 ? 5 : 0); E_line(ctx, P, [[lx, 6], [lx + sw, e.ry]], 5, ICE.furD); }
    ctx.save(); ctx.translate(0, curl * 6);
    // púas: abanico en el lomo; encogido = hacia arriba; erizado = largas en todas direcciones; pelado = muñones
    const n = 9;
    for (let i = 0; i < n; i++) {
      const a = -Math.PI + 0.35 + (i / (n - 1)) * (Math.PI - 0.7), aa = lerp(a, -Math.PI / 2 + (a + Math.PI / 2) * 0.3, curl), len = bald ? 5 : 16 + bristle * 14 + curl * 6;
      const bx = Math.cos(aa) * 18, by = Math.sin(aa) * 12 - 4, tx = bx + Math.cos(aa) * len, ty = by + Math.sin(aa) * len;
      E_poly(ctx, P, [[bx - Math.sin(aa) * 3, by + Math.cos(aa) * 3], [tx, ty], [bx + Math.sin(aa) * 3, by - Math.cos(aa) * 3]], i % 2 ? ICE.light : ICE.pale);
    }
    if (bristle > 0.5) for (const a of [0.4, 0.9, 2.3, 2.75]) E_poly(ctx, P, [[Math.cos(a) * 16, Math.sin(a) * 10], [Math.cos(a) * 34, Math.sin(a) * 22], [Math.cos(a) * 18 + 3, Math.sin(a) * 10 + 3]], ICE.pale);
    E_ell(ctx, P, 0, 0, 28, 17, bald ? '#d9b8b0' : ICE.fur);
    E_ell(ctx, P, -2, 4, 24, 11, ICE.furD);
    E_ell(ctx, P, 24, 4, 10, 8, ICE.fur); E_ell(ctx, P, 33, 6, 3, 2.5, '#2a2a30');
    E_eye(ctx, P, e, 24, 0, 2.2);
    ctx.restore();
  },
});
