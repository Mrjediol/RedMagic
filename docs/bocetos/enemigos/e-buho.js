// e-buho.js — Búho de cristal (bosque): volador que se POSA (ramas altas, plataformas) en vez de flotar; baja en picado por una
// curva marcada que pasa por ti y vuelve a subir. Pregunta: ¿lees la curva y le pegas mientras está posado (si te acercas, se va)?
'use strict';
addFoe({
  id: 'owl', name: 'Búho de cristal', short: 'Búho', world: 'bosque', rx: 20, ry: 22, fly: true, home: 'fly',
  role: 'Volador que se posa y cae en picado',
  question: 'Leer la curva del picado (sale marcada: pasa por donde estás y sube al otro lado) y apartarte de ella; pegarle cuando está posado. Si te acercas a su rama, se va volando a otra.',
  moves: [
    { name: 'Posarse', desc: 'Vuela a una rama alta o a una plataforma a media distancia de ti y se queda ahí mirándote (gira la cabeza). Si te acercas, se va a otra.' },
    { name: 'Picado', desc: 'Se agacha en la rama; se marca una curva desde él, pasando por ti, hasta el otro lado. Baja por ella muy rápido y sube a posarse en otro sitio.' },
    { name: 'Plumas', desc: 'Desde la rama, sacude las alas y lanza un abanico de plumas de cristal hacia ti.' },
  ],
  combos: 'Con el Oso (arriba el búho, abajo el oso). Con Zarzas: la curva del picado y el hueco del muro a la vez.',
  art: 'Búho o lechuza grande de plumaje gris pardo con las puntas de las plumas de cristal turquesa; disco facial claro y dos ojos enormes turquesa que brillan; penachos en las orejas. Posado: alas plegadas, gira la cabeza. Picado: alas pegadas, cuerpo en flecha. Volando: alas abiertas. Partes: cuerpo, cabeza (gira), 2 alas (plegada / abierta / pegada), patas. ~1×1,1 u.',
  tuning: Object.assign(baseTuning(85, 25, 1.2, 0), {
    perchMin: [180, 0, 500, 10, 'Se posa a (mín.) de ti'], perchMax: [420, 100, 900, 10, 'Se posa a (máx.) de ti'], speed: [280, 60, 600, 10, 'Velocidad volando'],
    perchTime: [1.4, 0.2, 5, 0.1, 'Posado antes de atacar (s)'], flee: [140, 0, 400, 10, 'Se va si te acercas a menos de'],
    swoopChance: [0.6, 0, 1, 0.05, 'Probabilidad de picado (si no, plumas)'], swoopWindup: [0.6, 0.1, 2, 0.05, 'Picado: preparación (s)'],
    swoopTime: [0.85, 0.3, 2, 0.05, 'Picado: duración (s)'], swoopDmg: [14, 0, 50, 1, 'Picado: daño'],
    featherWindup: [0.45, 0.1, 1.5, 0.05, 'Plumas: preparación (s)'], featherN: [3, 1, 7, 1, 'Plumas: cuántas'], featherSpeed: [430, 100, 900, 10, 'Plumas: velocidad'],
    featherDmg: [8, 0, 40, 1, 'Plumas: daño'], cooldown: [1.0, 0, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  init(e) { e.perch = null; },
  think(e, dt) {
    const t = FT(e), p = G.player, c = pCenter();
    const pick = (avoid) => {
      const all = perches().filter(q => q !== avoid && Math.abs(q.x - p.x) > t.perchMin && Math.abs(q.x - p.x) < t.perchMax && Math.abs(q.x - e.x) > 40);
      const list = all.length ? all : perches().filter(q => Math.abs(q.x - p.x) > 80);
      return list[Math.floor(Math.random() * list.length)] || { x: e.x, y: 100 };
    };
    switch (e.st) {
      case 'fly': {
        if (!e.perch) e.perch = pick();
        const tx = e.perch.x, ty = e.perch.y - e.ry;
        steer(e, tx, ty, t.speed, dt, 5); faceTo(e, e.mx > 0 ? e.x + 1 : e.mx < 0 ? e.x - 1 : p.x);
        if (Math.hypot(tx - e.x, ty - e.y) < 10) { e.x = tx; e.y = ty; e.mx = 0; e.my = 0; go(e, 'perch'); }
        if (e.stT > 3) e.perch = pick();
        break;
      }
      case 'perch': {
        e.mx = 0; e.my = 0; e.cdT -= dt; faceTo(e, p.x);
        const gone = e.perch && e.perch.y < GROUND - 1 && !perches().some(q => Math.abs(q.x - e.perch.x) < 2 && Math.abs(q.y - e.perch.y) < 2);   // su plataforma se rompió
        if (gone || Math.hypot(c.x - e.x, c.y - e.y) < t.flee || e.stT > t.perchTime * 3) { e.perch = pick(e.perch); go(e, 'fly'); e.vy -= 80; break; }
        if (e.stT > t.perchTime && e.cdT <= 0 && canStart(e)) {
          if (Math.random() < t.swoopChance) { begin(e, 'Picado'); go(e, 'swoopW'); e.lock = null;
            warn({ e, kind: 'path', t: t.swoopWindup, w: 3, follow: w => { const b = swoopCurve(e, e.lock || pCenter()); w.pts = []; for (let i = 0; i <= 16; i++) w.pts.push(bez(b, i / 16)); } }); }
          else { begin(e, 'Plumas'); go(e, 'featherW'); }
        }
        break;
      }
      case 'swoopW':
        e.mx = 0; e.my = 0; faceTo(e, p.x);
        if (e.stT > t.swoopWindup * 0.7 && !e.lock) e.lock = pCenter();
        if (windup(e, t.swoopWindup)) { e.curve = swoopCurve(e, e.lock || pCenter()); go(e, 'swoop'); release(e); e.hitP = false; sfx(k => SFXLIB.whoosh(k, 2600, 700, 0.5, 0.2)); }
        break;
      case 'swoop': {
        e.phase = 'active'; e.mx = 0; e.my = 0;
        const s = clamp(e.stT / t.swoopTime), q = bez(e.curve, s), q2 = bez(e.curve, Math.min(1, s + 0.02));
        e.x = clamp(q[0], ARENA.x0 + e.rx, ARENA.x1 - e.rx); e.y = q[1]; e.face = q2[0] >= q[0] ? 1 : -1; e.diveA = Math.atan2(q2[1] - q[1], Math.abs(q2[0] - q[0]) + 0.01);
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Picado', t.swoopDmg, e.face * 360, -220)) e.hitP = true;
        if (s >= 1) { go(e, 'fly'); recover(e); e.perch = pick(); e.cdT = t.cooldown; }
        break;
      }
      case 'featherW':
        faceTo(e, p.x);
        if (!windup(e, t.featherWindup)) break;
        { const a = aimPoint(e), a0 = Math.atan2(a.y - e.y, a.x - e.x);
          for (let i = 0; i < t.featherN; i++) { const an = a0 + (i - (t.featherN - 1) / 2) * 0.22; foeShot({ e, atk: 'Plumas', x: e.x, y: e.y, vx: Math.cos(an) * t.featherSpeed, vy: Math.sin(an) * t.featherSpeed, r: 4, style: 'feather', dmg: t.featherDmg, life: 3 }); } }
        go(e, 'perch'); release(e); recover(e); e.cdT = t.cooldown; e.stT = t.perchTime * 0.4;
        sfx(k => SFXLIB.whoosh(k, 3600, 1800, 0.15, 0.12));
        break;
    }
  },
  draw(ctx, e, P) {
    const st = e.st, flying = st === 'fly', dive = st === 'swoop', crouch = st === 'swoopW' ? e.tell : 0, flap = Math.sin(G.t * 16 + e.id);
    ctx.save();
    if (dive) ctx.rotate(e.diveA || 0);
    ctx.translate(0, crouch * 5);
    // alas: abiertas al volar, pegadas en el picado, plegadas posado (al lanzar plumas se abren)
    const open = flying ? 1 : st === 'featherW' ? e.tell : 0;
    for (const s of [-1, 1]) {
      ctx.save(); ctx.translate(-2, -2);
      if (open > 0.2) { ctx.rotate(s * (0.9 + flap * 0.6 * open)); E_ell(ctx, P, 0, -18 * s, 9, 20, '#6b5a48'); E_poly(ctx, P, [[-4, -34 * s], [0, -44 * s], [5, -34 * s]], WOOD.crystal); }
      else if (!dive) { E_ell(ctx, P, -6, 4 + s * 2, 10, 16, '#5a4a3a'); E_poly(ctx, P, [[-14, 16], [-10, 26], [-6, 16]], WOOD.crystal); }
      ctx.restore();
    }
    E_ell(ctx, P, 0, 2, 17, 21, '#6b5a48');
    E_ell(ctx, P, 3, 8, 11, 12, '#8a7864');
    E_ell(ctx, P, 4, -12, 14, 12, '#7a6854');
    E_ell(ctx, P, 6, -11, 11, 9, '#cfc4b0');
    E_poly(ctx, P, [[-4, -22], [-2, -32], [2, -22]], '#5a4a3a'); E_poly(ctx, P, [[8, -22], [12, -31], [13, -21]], '#5a4a3a');
    if (!P.mono) glow(ctx, 6, -12, 16, [120, 235, 255], 0.35 + 0.3 * e.tell);
    E_eye(ctx, P, e, 2, -12, 3.6); E_eye(ctx, P, e, 10, -12, 3.6);
    E_poly(ctx, P, [[6, -8], [9, -5], [6, -3]], '#d8a040');
    if (!flying && !dive) { E_line(ctx, P, [[0, 22], [0, e.ry]], 2, '#d8a040'); E_line(ctx, P, [[6, 22], [6, e.ry]], 2, '#d8a040'); }
    ctx.restore();
  },
});
// curva del picado: cuadrática que empieza en el búho, pasa por el objetivo en el punto medio y sube al otro lado a la misma altura
function swoopCurve(e, q) {
  const x0 = e.x, y0 = e.y, x2 = clamp(q.x + (q.x - x0), ARENA.x0 + 40, ARENA.x1 - 40), y2 = Math.min(y0, q.y - 60);
  return [[x0, y0], [2 * q.x - 0.5 * (x0 + x2), 2 * (q.y + 6) - 0.5 * (y0 + y2)], [x2, y2]];
}
const bez = (b, s) => { const u = 1 - s; return [u * u * b[0][0] + 2 * u * s * b[1][0] + s * s * b[2][0], u * u * b[0][1] + 2 * u * s * b[1][1] + s * s * b[2][1]]; };
