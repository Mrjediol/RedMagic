// e-ventisca.js — Ventisca (mundo de hielo): espíritu de nieve volador que sopla. Su soplo no hace daño: te EMPUJA (contra otros
// enemigos, fuera de tu plataforma, a los pinchos). Pregunta: ¿te colocas para que el viento no te tire a nada, o lo atraviesas con el dash?
'use strict';
addFoe({
  id: 'gale', name: 'Ventisca', short: 'Vent.', world: 'hielo', rx: 22, ry: 26, fly: true, home: 'hover',
  role: 'Voladora de control: te empuja',
  question: 'Posicionarte contra el viento: el soplo te empuja (físicamente: sí te saca de una plataforma) hacia lo que tengas detrás. El dash lo atraviesa. Y esquivar sus bolas de nieve, que revientan en trozos.',
  moves: [
    { name: 'Soplo', desc: 'Se hincha y sopla en un cono hacia ti (cono marcado en rojo). Dentro del cono te empuja con fuerza en esa dirección mientras dura; sin daño. Con el dash no te mueve.' },
    { name: 'Bola de nieve', desc: 'Lanza una bola en parábola que, al caer, revienta y suelta tres trozos pequeños hacia arriba y a los lados.' },
  ],
  combos: 'Con Morsa (te empuja a sus pinchos), con Colgantes (te empuja debajo), con Erizos erizados. Sola es fácil: es un enemigo de combinación.',
  art: 'Espíritu de ventisca: un remolino de nieve con forma de lágrima y capucha, dentro de la capucha oscuridad con dos ojos azul claro. Dos brazos de nieve deshilachada y una cola de copos que se deshace. Al soplar la capucha se abre en una boca redonda con anillos de viento. Partes: cuerpo (remolino), capucha/cara, 2 brazos, cola (partículas). ~1,1×1,3 u.',
  tuning: Object.assign(baseTuning(70, 20, 1.2, 0), {
    distMin: [200, 50, 500, 10, 'Distancia mínima (huye)'], distMax: [340, 100, 700, 10, 'Distancia máxima'],
    height: [110, 0, 260, 5, 'Altura sobre ti'], speed: [140, 30, 400, 5, 'Velocidad'],
    gustChance: [0.55, 0, 1, 0.05, 'Probabilidad de soplo (si no, bola)'], gustWindup: [0.6, 0.1, 2, 0.05, 'Soplo: preparación (s)'],
    gustTime: [1.4, 0.2, 4, 0.1, 'Soplo: duración (s)'], gustRange: [440, 100, 900, 10, 'Soplo: alcance'], gustHalf: [0.45, 0.1, 1.2, 0.05, 'Soplo: medio ángulo (rad)'],
    gustPush: [300, 0, 900, 10, 'Soplo: empuje (px/s)'],
    snowWindup: [0.5, 0.1, 2, 0.05, 'Bola: preparación (s)'], snowFlight: [0.9, 0.3, 2, 0.05, 'Bola: vuelo (s)'], snowDmg: [10, 0, 40, 1, 'Bola: daño'],
    snowR: [30, 10, 90, 1, 'Bola: radio al reventar'], fragN: [3, 0, 7, 1, 'Bola: trozos'], fragDmg: [5, 0, 30, 1, 'Bola: daño de cada trozo'],
    cooldown: [1.6, 0.2, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  init(e) { e.side = 1; },
  think(e, dt) {
    const t = FT(e), p = G.player, c = pCenter();
    switch (e.st) {
      case 'hover': {
        e.cdT -= dt; faceTo(e, p.x);
        if (Math.abs(e.x - p.x) < t.distMin * 0.7) e.side = Math.sign(e.x - p.x) || e.side;
        const mid = (t.distMin + t.distMax) / 2;
        let tx = p.x + e.side * mid; if (tx < ARENA.x0 + 50 || tx > ARENA.x1 - 50) { e.side = -e.side; tx = p.x + e.side * mid; }
        steer(e, tx, clamp(c.y - t.height + Math.sin(G.t * 1.4 + e.id) * 20, CEIL + 40, GROUND - 110), t.speed, dt);
        if (e.cdT > 0 || !canStart(e)) break;
        if (Math.hypot(c.x - e.x, c.y - e.y) < t.gustRange && Math.random() < t.gustChance) {
          begin(e, 'Soplo'); go(e, 'gustW'); e.gA = Math.atan2(c.y - e.y, c.x - e.x);
          warn({ e, kind: 'cone', x: e.x, y: e.y, ang: e.gA, len: t.gustRange, half: t.gustHalf, t: t.gustWindup + t.gustTime, keep: false, follow: w => { w.x = e.x; w.y = e.y; w.ang = e.gA; } });
        } else { begin(e, 'Bola de nieve'); go(e, 'snowW'); }
        break;
      }
      case 'gustW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 5)); e.my = lerp(e.my, 0, Math.min(1, dt * 5));
        if (e.stT < t.gustWindup * 0.7) e.gA = Math.atan2(c.y - e.y, c.x - e.x);
        e.face = Math.cos(e.gA) >= 0 ? 1 : -1;
        if (windup(e, t.gustWindup)) { go(e, 'gust'); release(e); sfx(k => { const t0 = nowT(); vNoise(t0, t.gustTime * k, 0.2, 'bandpass', 700, 1600, 0.7, { hold: true, att: 0.08, wet: 0.2 }); }); }
        break;
      case 'gust': {
        e.phase = 'active'; e.mx = 0; e.my = 0;
        const dx = c.x - e.x, dy = c.y - e.y, d = Math.hypot(dx, dy), da = Math.abs(((Math.atan2(dy, dx) - e.gA + Math.PI * 3) % TAU) - Math.PI);
        if (d < t.gustRange && da < t.gustHalf && !(p.dashT > 0) && !G.es.harmless) {
          const k = 1 - 0.5 * d / t.gustRange;                       // más fuerte cerca
          p.x += Math.cos(e.gA) * t.gustPush * k * dt;
          if (!p.onGround) p.vy += Math.sin(e.gA) * t.gustPush * 2 * k * dt;
          if (G.t - (e._pushT || -9) > 0.6) { e._pushT = G.t; const F = G.fight; if (F) { const key = 'Ventisca · Soplo (empujón)'; (F.hits[key] = F.hits[key] || { n: 0, dmg: 0 }).n++; } }
        }
        if (Math.random() < dt * 40) { const a = e.gA + (Math.random() - 0.5) * t.gustHalf * 1.6, x0 = e.x, y0 = e.y, s = 500 + Math.random() * 200;
          addFx(0.7, (ctx, layer, t0) => { if (layer !== 'front') return; const u = G.t - t0, r = u * s; if (r > t.gustRange) return; ctx.save(); ctx.globalAlpha = 0.6 * (1 - u / 0.7); ctx.strokeStyle = '#eef6ff'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(x0 + Math.cos(a) * r, y0 + Math.sin(a) * r); ctx.lineTo(x0 + Math.cos(a) * (r + 22), y0 + Math.sin(a) * (r + 22)); ctx.stroke(); ctx.restore(); }); }
        if (e.stT > t.gustTime) { go(e, 'rest'); recover(e); }
        break;
      }
      case 'snowW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 5)); e.my = lerp(e.my, 0, Math.min(1, dt * 5)); faceTo(e, p.x);
        if (!windup(e, t.snowWindup)) break;
        go(e, 'rest'); release(e); e.busy = false;
        { const tx = clamp(p.x + p.vx * t.snowFlight * 0.5, ARENA.x0 + 10, ARENA.x1 - 10), ty = surfaceUnder(tx, p.y);
          lobShot(e.x, e.y, tx, ty, t.snowFlight, { e, atk: 'Bola de nieve', dmg: t.snowDmg, r: 9, style: 'snow', warnR: t.snowR,
            onLand: (x, y) => { foeBlast(e, x, y - 6, t.snowR, t.snowDmg, 'Bola de nieve', { pal: PAL.ice });
              for (let i = 0; i < t.fragN; i++) { const a = -Math.PI / 2 + (i - (t.fragN - 1) / 2) * 0.7; foeShot({ e, atk: 'Bola de nieve (trozo)', x, y: y - 8, vx: Math.cos(a) * 200, vy: Math.sin(a) * 320, g: GRAV * 0.8, r: 4, style: 'snow', dmg: t.fragDmg, life: 2 }); } } }); }
        break;
      case 'rest':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.45) { go(e, 'hover'); e.cdT = t.cooldown * (0.8 + Math.random() * 0.4); }
        break;
    }
  },
  draw(ctx, e, P) {
    const blow = e.st === 'gust' ? 1 : e.st === 'gustW' ? e.tell : 0, sw = G.t * 3 + e.id;
    if (!P.mono) glow(ctx, 0, 0, 40, [200, 230, 255], 0.3);
    // cola de copos que se deshace
    for (let i = 0; i < 6; i++) { const k = i / 6; E_ell(ctx, P, -10 - i * 7 + Math.sin(sw + i) * 3, 14 + i * 4 + Math.cos(sw * 1.3 + i) * 3, 7 * (1 - k * 0.8), 6 * (1 - k * 0.8), i % 2 ? ICE.snow : ICE.pale); }
    // brazos
    for (const s of [-1, 1]) E_line(ctx, P, [[-4, 2], [s * 10 - 8, 14 + Math.sin(sw + s) * 4], [s * 14 - 10, 22]], 4, ICE.pale);
    // cuerpo-remolino y capucha
    E_ell(ctx, P, 0, 0, 20, 24, ICE.pale);
    E_ell(ctx, P, 2, -2, 15, 18, ICE.snow);
    for (let i = 0; i < 3; i++) E_line(ctx, P, [[-14 + i * 4, 10 - i * 8], [Math.cos(sw + i) * 8, -4 - i * 6], [12 - i * 2, 6 - i * 8]], 1.4, ICE.light);
    E_ell(ctx, P, 8, -8, 9 + blow * 3, 10 + blow * 3, ICE.dark);                  // la capucha (boca al soplar)
    if (blow > 0.3) E_ell(ctx, P, 12, -8, 4 * blow, 5 * blow, '#0c1824');
    else { E_eye(ctx, P, e, 6, -10, 2); E_eye(ctx, P, e, 12, -10, 2); }
  },
});
