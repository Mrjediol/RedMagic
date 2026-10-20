// e-serpiente.js — Serpiente de lava (volcán): nada bajo el suelo y salta en ARCO por encima de ti de un lado a otro (como un delfín).
// Pregunta: ¿saltas su cuerpo en el momento justo (o sales del arco), y le pegas cuando cae y se retuerce fuera?
'use strict';
addFoe({
  id: 'lavaserpent', name: 'Serpiente de lava', short: 'Serp.', world: 'volcan', rx: 20, ry: 18, fly: true, home: 'under',
  role: 'Saltadora en arco desde el suelo',
  question: 'Su salto pasa por donde estás (marcadas la entrada, la salida y la curva): saltar por encima de su cuerpo justo cuando pasa, o salirte del arco. Al caer se retuerce fuera un momento: castígala antes de que se hunda.',
  moves: [
    { name: 'Nadar', desc: 'Bajo el suelo no se le puede dar; burbujas de lava delatan dónde está. Se coloca a un lado de ti.' },
    { name: 'Salto en arco', desc: 'Burbujea en la entrada y en la salida (las dos marcadas) y se marca la curva; salta desde un lado, pasa a la altura de tu cuerpo y se zambulle al otro. Encadena dos saltos (ida y vuelta) antes de descansar.' },
    { name: 'Fuera', desc: 'Tras el último salto se queda fuera retorciéndose un momento (recibe más daño) y se vuelve a hundir.' },
  ],
  combos: 'Con la Salamandra: los rastros te quitan suelo para esquivar. Con Ave brasa: dos pasadas por encima de ti.',
  art: 'Serpiente o anguila larga de roca volcánica negra con escamas que brillan por dentro (naranja), cabeza de dragón pequeña con cuernos de obsidiana; deja gotas de lava al salir. Partes: cabeza, 6-8 segmentos de cuerpo, cola. ~0,9 u de grosor, ~4 u de largo.',
  tuning: Object.assign(baseTuning(170, 60, 0.3, 0), {
    tunnelSpeed: [240, 50, 600, 10, 'Nadar: velocidad'], span: [420, 150, 900, 10, 'Salto: ancho del arco'], leapWindup: [0.8, 0.2, 2, 0.05, 'Salto: aviso (s)'],
    leapTime: [0.9, 0.3, 2, 0.05, 'Salto: duración (s)'], leapDmg: [16, 0, 50, 1, 'Salto: daño'], leaps: [2, 1, 5, 1, 'Saltos seguidos'],
    exposed: [1.3, 0, 4, 0.1, 'Fuera tras el último salto (s, castigo)'], exposedVuln: [1.4, 1, 3, 0.05, 'Fuera: × daño recibido'], cooldown: [1.2, 0, 5, 0.1, 'Descanso bajo tierra (s)'],
  }),
  init(e) { e.trail = []; e.leapN = 0; e.side = Math.random() < 0.5 ? -1 : 1; hide(e); e.hy = GROUND; },
  think(e, dt) {
    const t = FT(e), p = G.player;
    switch (e.st) {
      case 'under': {
        e.cdT -= dt;
        const x0 = clamp(p.x - e.side * t.span / 2, ARENA.x0 + 30, ARENA.x1 - 30);
        e.hx += clamp(x0 - e.hx, -t.tunnelSpeed * dt, t.tunnelSpeed * dt); e.hy = GROUND;
        if (Math.random() < dt * 5) dustPuff(e.hx, GROUND);
        if (Math.abs(e.hx - x0) < 12 && e.cdT <= 0 && canStart(e)) {
          begin(e, 'Salto en arco'); go(e, 'leapW'); e.leapX0 = e.hx;
          e.leapX2 = clamp(p.x + e.side * t.span / 2, ARENA.x0 + 30, ARENA.x1 - 30);
          warn({ e, kind: 'circle', x: e.leapX0, r: 28, t: t.leapWindup }); warn({ e, kind: 'circle', x: e.leapX2, r: 28, t: t.leapWindup + t.leapTime });
          warn({ e, kind: 'path', t: t.leapWindup, w: 3, follow: w => { const b = serpCurve(e); w.pts = []; for (let i = 0; i <= 16; i++) w.pts.push(bez(b, i / 16)); } });
        }
        break;
      }
      case 'leapW':
        if (e.stT < t.leapWindup * 0.7) e.target = { x: G.player.x, y: G.player.y - 50 };
        if (!windup(e, t.leapWindup)) break;
        e.curve = serpCurve(e); unhide(e, e.leapX0, GROUND); e.trail = []; go(e, 'leap'); release(e); e.hitP = false; e.leapN++;
        foeBlast(e, e.leapX0, GROUND - 6, 22, 0, 'Salto en arco', { pal: PAL.fire });
        break;
      case 'leap': {
        e.phase = 'active'; e.mx = 0; e.my = 0;
        const s = clamp(e.stT / t.leapTime), q = bez(e.curve, s), q2 = bez(e.curve, Math.min(1, s + 0.02));
        e.x = q[0]; e.y = q[1]; e.face = q2[0] >= q[0] ? 1 : -1; e.headA = Math.atan2(q2[1] - q[1], Math.abs(q2[0] - q[0]) + 0.01);
        e.trail.unshift({ x: e.x, y: e.y }); if (e.trail.length > 14) e.trail.pop();
        if (!e.hitP && e.trail.some(q => circleHitsPlayer(q.x, q.y, 14)) && hitP(e, 'Salto en arco', t.leapDmg, e.face * 360, -320)) e.hitP = true;
        if (s >= 1) {
          foeBlast(e, e.x, GROUND - 6, 22, 0, 'Salto en arco', { pal: PAL.fire });
          if (e.leapN < t.leaps && canStart(e)) { hide(e); e.hy = GROUND; e.side = -e.side; go(e, 'leapW'); begin(e, 'Salto en arco'); e.leapX0 = e.hx; e.leapX2 = clamp(G.player.x + e.side * t.span / 2, ARENA.x0 + 30, ARENA.x1 - 30);
            warn({ e, kind: 'circle', x: e.leapX2, r: 28, t: t.leapWindup * 0.6 + t.leapTime }); e.stT = t.leapWindup * 0.4;
            warn({ e, kind: 'path', t: t.leapWindup * 0.6, w: 3, follow: w => { const b = serpCurve(e); w.pts = []; for (let i = 0; i <= 16; i++) w.pts.push(bez(b, i / 16)); } }); }
          else { e.y = GROUND - e.foot; go(e, 'surface'); recover(e); makeVuln(e, t.exposedVuln, t.exposed); e.leapN = 0; }
        }
        break;
      }
      case 'surface':
        e.phase = 'recover'; e.mx = 0; e.my = 0; e.y = GROUND - e.foot; faceTo(e, G.player.x);
        e.trail = [{ x: e.x, y: e.y }]; for (let i = 1; i < 10; i++) e.trail.push({ x: e.x - e.face * i * 12, y: e.y + Math.sin(G.t * 10 + i) * 4 });
        if (e.stT > t.exposed) { hide(e); e.hy = GROUND; go(e, 'under'); e.cdT = t.cooldown; e.side = Math.sign(e.hx - G.player.x) || 1; }
        break;
    }
  },
  onInterrupt(e) { if (e.hidden) return; e.y = GROUND - e.foot; go(e, 'surface'); },
  drawHidden(ctx, e) {
    const big = e.st === 'leapW' ? e.tell : 0;
    for (const x of e.st === 'leapW' ? [e.leapX0, e.leapX2] : [e.hx]) {
      for (let i = 0; i < 3 + big * 3; i++) { const R = rand(i + Math.floor(x)), ph = (G.t * (1.5 + R()) + R()) % 1; disc(ctx, x + (R() - 0.5) * (20 + big * 30), GROUND - ph * (10 + big * 20), (3 + R() * 3) * (1 - ph), R() < 0.5 ? MAGMA.hot : MAGMA.glow, 1 - ph, 'front'); }
      ctx.save(); ctx.globalAlpha = 0.5 + big * 0.4; ctx.fillStyle = MAGMA.lava; ctx.beginPath(); ctx.ellipse(x, GROUND + 2, 14 + big * 14, 4, 0, 0, TAU); ctx.fill(); ctx.restore();
    }
  },
  draw(ctx, e, P) {
    ctx.save(); ctx.beginPath(); ctx.rect(-400, -400, 800, GROUND - e.y + 400); ctx.clip();   // lo que está bajo el suelo no se ve
    const pts = e.trail.map(q => [(q.x - e.x) * e.face, q.y - e.y]);
    for (let i = pts.length - 1; i >= 1; i--) { const k = 1 - i / pts.length; E_ell(ctx, P, pts[i][0], pts[i][1], 14 * k + 5, 13 * k + 5, i % 2 ? MAGMA.obs : '#3a2420'); if (i % 2 === 0) E_ell(ctx, P, pts[i][0], pts[i][1] - 3, 5 * k + 2, 3, MAGMA.glow); }
    ctx.rotate(e.st === 'leap' ? e.headA || 0 : 0);
    E_ell(ctx, P, 0, 0, 20, 15, MAGMA.obs); E_ell(ctx, P, 4, -3, 14, 9, '#3a2420');
    E_poly(ctx, P, [[-6, -12], [-16, -26], [0, -12]], MAGMA.obsL); E_poly(ctx, P, [[4, -12], [0, -28], [10, -11]], MAGMA.obsL);
    E_ell(ctx, P, 16, 4, 6, 3, MAGMA.hot);
    E_eye(ctx, P, e, 10, -5, 2.6);
    ctx.restore();
  },
});
// arco del salto: de la entrada a la salida (en el suelo), pasando por el objetivo en la mitad
function serpCurve(e) {
  const q = e.target || { x: G.player.x, y: G.player.y - 50 }, x0 = e.leapX0, x2 = e.leapX2, y0 = GROUND + 10, y2 = GROUND + 10;
  return [[x0, y0], [2 * q.x - 0.5 * (x0 + x2), 2 * q.y - 0.5 * (y0 + y2)], [x2, y2]];
}
