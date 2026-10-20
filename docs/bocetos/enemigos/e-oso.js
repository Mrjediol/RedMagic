// e-oso.js — Oso de musgo (bosque): pesado que te persigue con un combo de zarpazos (el tercero a veces llega tarde) y TREPA a tu
// plataforma; desde arriba se tira en panzazo. Pregunta: ¿esperas al tercer zarpazo antes de castigar, y subir no te salva?
'use strict';
addFoe({
  id: 'bear', name: 'Oso de musgo', short: 'Oso', world: 'bosque', rx: 46, ry: 40, home: 'roam', platforms: true,
  role: 'Pesado cuerpo a cuerpo que trepa',
  question: 'Esquivar el combo de zarpazos y no castigar antes de tiempo: a veces hay un tercero, más lento y fuerte. Subirte no te salva: trepa (despacio) a tu plataforma y desde arriba se tira en panzazo.',
  moves: [
    { name: 'Zarpazos', desc: 'De cerca: dos zarpazos rápidos que avanzan un poco (cada uno levanta la zarpa y se enciende). A veces, tras una pausa que parece el final, se levanta sobre las patas y da un tercero más fuerte.' },
    { name: 'Trepar', desc: 'Si te quedas en una plataforma, va hasta el borde, se agacha y trepa de un salto torpe. Una vez arriba, sigue luchando.' },
    { name: 'Panzazo', desc: 'Si está en una plataforma y tú abajo cerca, se deja caer encima de ti: zona en el suelo y, al caer, dos ondas a los lados.' },
  ],
  combos: 'Con Búho: arriba te espera el búho, abajo el oso. Con Saltasetas: no puedes quedarte quieto esquivando zarpazos.',
  art: 'Oso pardo enorme cubierto de musgo, con un racimo de cristales turquesa creciéndole en el lomo y un arbolito o setas en el hombro; ojos turquesa que brillan. Anda a cuatro patas, se levanta a dos para el zarpazo fuerte. Partes: cuerpo, cabeza, 4 patas (zarpa delantera articulada), cristales. ~2,3×2 u.',
  tuning: Object.assign(baseTuning(320, 90, 0.25, 6), {
    walk: [75, 0, 250, 5, 'Velocidad'], keepMin: [70, 0, 300, 5, 'Se acerca hasta (mín.)'], keepMax: [140, 30, 400, 5, 'Se acerca hasta (máx.)'],
    comboRange: [140, 40, 300, 5, 'Zarpazos: si estás a menos de'], swipeWindup: [0.45, 0.1, 1.5, 0.05, 'Zarpazo: preparación (s)'],
    swipeGap: [0.3, 0.05, 1, 0.05, 'Zarpazos: pausa entre los dos (s)'], swipeDmg: [12, 0, 50, 1, 'Zarpazo: daño'], lungeX: [50, 0, 150, 5, 'Zarpazo: avance'],
    thirdChance: [0.45, 0, 1, 0.05, 'Probabilidad del tercer zarpazo'], thirdDelay: [0.6, 0.2, 2, 0.05, 'Tercer zarpazo: preparación (s)'],
    thirdDmg: [18, 0, 60, 1, 'Tercer zarpazo: daño'], comboRec: [0.7, 0, 2, 0.05, 'Tras el combo: recuperación (s, castigo)'],
    climbDelay: [0.8, 0, 4, 0.1, 'Trepar: si estás en una plataforma más de (s)'], climbReach: [170, 40, 300, 5, 'Trepar: altura máxima'],
    flopRange: [160, 30, 400, 5, 'Panzazo: si estás debajo a menos de'], flopWindup: [0.5, 0.1, 2, 0.05, 'Panzazo: preparación (s)'],
    flopR: [70, 20, 200, 5, 'Panzazo: radio'], flopDmg: [16, 0, 60, 1, 'Panzazo: daño'], flopWaveDmg: [10, 0, 40, 1, 'Panzazo: daño de las ondas'],
    cooldown: [1.2, 0, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    const myPl = e.onGround && e.y + e.foot < GROUND - 2 ? PLATFORMS.find(pl => e.x > pl.x0 && e.x < pl.x1 && Math.abs(pl.y - (e.y + e.foot)) < 3) : null;
    const pPl = p.onGround && p.y < GROUND - 2 ? PLATFORMS.find(q => onPlatform(q)) : null;
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        if (myPl && !pPl && p.y > GROUND - 5 && ad < t.flopRange + (myPl.x1 - myPl.x0) / 2 && e.cdT <= 0 && canStart(e)) { begin(e, 'Panzazo'); go(e, 'flopW'); break; }
        if (pPl && pPl !== myPl && GROUND - pPl.y <= t.climbReach && playerUpTime() > t.climbDelay && e.onGround) { e.climbPl = pPl; go(e, 'climbGo'); break; }
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.walk, burst: 2, hop: 0.05 });
        if (e.cdT <= 0 && ad < t.comboRange && Math.abs(p.y - (e.y + e.foot)) < 60 && canStart(e)) { begin(e, 'Zarpazos'); go(e, 'swipeW'); e.combo = 0; e.third = Math.random() < t.thirdChance; }
        break;
      // ---- trepar: ir al borde más cercano de tu plataforma y subir de un salto
      case 'climbGo': {
        const pl = e.climbPl; if (!PLATFORMS.includes(pl)) { go(e, 'roam'); break; }
        const side = e.x < (pl.x0 + pl.x1) / 2 ? -1 : 1, edge = side < 0 ? pl.x0 : pl.x1, foot = edge - side * 10;
        if (e.onGround && Math.abs(e.x - (edge + side * 40)) < 24) { go(e, 'climbW'); e.climbX = clamp(foot - side * 30, pl.x0 + 20, pl.x1 - 20); break; }
        walkTo(e, edge + side * 40, t.walk * 2, dt, 8); faceTo(e, edge);
        if (e.stT > 4) go(e, 'roam');
        break;
      }
      case 'climbW':
        e.mx = 0;
        if (e.stT > 0.3) { leapTo(e, e.climbX, e.climbPl.y, 40); go(e, 'climb'); sfx(k => SFXLIB.whoosh(k, 700, 300, 0.3, 0.12)); }
        break;
      case 'climb':
        if (e.onGround && e.stT > 0.1) { e.mx = 0; go(e, 'roam'); e.cdT = Math.max(e.cdT, 0.4); }
        break;
      // ---- zarpazos
      case 'swipeW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); if (e.stT < 0.1) faceTo(e, p.x);
        if (windup(e, e.combo === 2 ? t.thirdDelay : e.combo === 1 ? t.swipeWindup * 0.6 : t.swipeWindup)) {
          go(e, 'swipe'); release(e); e.mx = e.face * t.lungeX / 0.12; e.hitP = false;
          const x = e.x + e.face * (e.rx + 10), y = e.y - 4, big = e.combo === 2, f = e.face;
          addFx(0.3, (ctx, layer, t0) => { if (layer !== 'front') return; const u = (G.t - t0) / 0.3; ctx.save(); ctx.globalAlpha = 0.85 * (1 - u); ctx.strokeStyle = big ? '#ffd27a' : '#f0e8d8'; ctx.lineWidth = big ? 5 : 3; ctx.beginPath(); ctx.arc(x - f * 10, y, big ? 50 : 38, f > 0 ? -1.3 : Math.PI - 0.6, f > 0 ? 0.6 : Math.PI + 1.3); ctx.stroke(); ctx.restore(); });
          sfx(k => SFXLIB.whoosh(k, big ? 1400 : 2200, 600, 0.15, big ? 0.24 : 0.16));
        }
        break;
      case 'swipe':
        e.phase = 'active';
        if (!e.hitP && rectHitsPlayer(e.x, e.y - e.ry - (e.combo === 2 ? 30 : 10), e.x + e.face * (e.rx + 42), e.y + e.ry) && hitP(e, e.combo === 2 ? 'Tercer zarpazo' : 'Zarpazo', e.combo === 2 ? t.thirdDmg : t.swipeDmg, e.face * (e.combo === 2 ? 520 : 300), -240)) e.hitP = true;
        if (e.stT > 0.12) { e.mx = 0; go(e, 'swipeGap'); e.phase = 'recover'; }
        break;
      case 'swipeGap':
        e.phase = 'recover'; e.mx = 0;
        if (e.combo === 0 && e.stT > t.swipeGap) { e.combo = 1; go(e, 'swipeW'); }
        else if (e.combo === 1 && e.third && e.stT > t.swipeGap * 1.6) { e.combo = 2; go(e, 'swipeW'); }
        else if (e.stT > (e.combo === 1 && !e.third ? t.comboRec : e.combo === 2 ? t.comboRec : 99)) { go(e, 'roam'); recover(e); e.cdT = t.cooldown; }
        break;
      // ---- panzazo desde la plataforma
      case 'flopW':
        e.mx = 0; faceTo(e, p.x);
        if (!windup(e, t.flopWindup)) break;
        { const tx = clamp(p.x, ARENA.x0 + e.rx, ARENA.x1 - e.rx), T = leapTo(e, tx, GROUND, 30); e.dropT = 1; go(e, 'flop'); release(e); warn({ e, kind: 'circle', x: tx, r: t.flopR, t: T }); }
        break;
      case 'flop':
        e.phase = 'active';
        if (e.onGround && e.stT > 0.1 && e.y + e.foot > GROUND - 2) {
          e.mx = 0; e.dropT = 0; go(e, 'flopRec'); recover(e);
          foeBlast(e, e.x, GROUND - 10, t.flopR, t.flopDmg, 'Panzazo', { pal: PAL.wind });
          for (const d of [-1, 1]) groundWave(e, e.x + d * e.rx, d, { speed: 330, dist: 380, h: 26, dmg: t.flopWaveDmg, atk: 'Panzazo (ondas)', pal: { a: WOOD.barkD, b: WOOD.moss } });
          G.shake += 8;
        }
        if (e.stT > 3) { e.dropT = 0; go(e, 'roam'); recover(e); }
        break;
      case 'flopRec':
        e.phase = 'recover'; e.mx = 0;
        if (e.stT > 0.8) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
    }
  },
  onInterrupt(e) { e.dropT = 0; go(e, 'roam'); },
  draw(ctx, e, P) {
    const raise = e.st === 'swipeW' ? e.tell : e.st === 'swipe' ? 1 - e.stT * 6 : 0, stand = e.combo === 2 && (e.st === 'swipeW' || e.st === 'swipe') ? 1 : 0;
    const crouch = e.st === 'climbW' || e.st === 'flopW' ? 0.6 : 0;
    ctx.save(); ctx.translate(-30, e.ry); ctx.rotate(-stand * 0.5 * Math.max(0.3, raise)); ctx.translate(30, -e.ry); ctx.translate(0, crouch * 8);
    for (let i = 0; i < 4; i++) { const lx = [-30, -14, 14, 30][i], sw = Math.sin(e.wp * 0.9 + i * Math.PI) * (Math.abs(e.mx) > 10 ? 7 : 0); if (i === 3 && raise > 0) continue; E_line(ctx, P, [[lx, 10], [lx + sw, e.ry]], 12, i % 2 ? '#3d2e20' : '#4a3828'); }
    E_ell(ctx, P, -2, 0, 46, 30, '#4a3828');
    E_ell(ctx, P, -6, -4, 40, 25, '#6b5038');
    for (const [x, y, r] of [[-24, -20, 12], [-4, -26, 14], [14, -20, 10]]) E_ell(ctx, P, x, y, r, r * 0.6, WOOD.moss);
    for (let i = 0; i < 3; i++) E_poly(ctx, P, [[-14 + i * 8, -26], [-11 + i * 8, -42 - (i % 2) * 8], [-7 + i * 8, -26]], i % 2 ? WOOD.crystalL : WOOD.crystal);
    // zarpa delantera (se levanta y baja)
    ctx.save(); ctx.translate(28, 6); ctx.rotate(-raise * 1.6); E_line(ctx, P, [[0, 0], [14, 22]], 12, '#4a3828'); for (let k = 0; k < 3; k++) E_line(ctx, P, [[12 + k * 2, 26], [18 + k * 3, 32]], 2, '#e8dcc0'); ctx.restore();
    E_ell(ctx, P, 40, -10, 18, 15, '#6b5038'); E_ell(ctx, P, 52, -4, 9, 7, '#9c8064');
    E_ell(ctx, P, 32, -24, 5, 5, '#4a3828'); E_ell(ctx, P, 46, -24, 5, 5, '#4a3828');
    E_eye(ctx, P, e, 44, -12, 2.8);
    ctx.restore();
  },
});
