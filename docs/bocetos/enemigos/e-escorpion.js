// e-escorpion.js — Escorpión de obsidiana (volcán): pica con la cola desde lejos y se pone en GUARDIA: si le pegas mientras se
// protege, contraataca al instante. Pregunta: ¿tienes paciencia para no golpear la guardia y castigar cuando la cola se clava?
'use strict';
addFoe({
  id: 'scorpion', name: 'Escorpión de obsidiana', short: 'Escorp.', world: 'volcan', rx: 34, ry: 20, home: 'roam',
  role: 'Duelista: guardia y contraataque',
  question: 'Paciencia: cuando cruza las pinzas en guardia (brillo claro, «GUARDIA»), no le pegues o te contraataca con la cola. Esquiva el picotazo largo (línea marcada) y castígale mientras la cola está clavada.',
  moves: [
    { name: 'Picotazo', desc: 'Levanta la cola y marca una línea hacia ti que se fija (blanca) justo antes; la cola sale disparada por esa línea, muy lejos. Después se queda clavada un momento (castigo).' },
    { name: 'Guardia', desc: 'Cruza las pinzas delante un rato: tus golpes no le hacen nada y, en cuanto le das, contraataca al instante con un picotazo rápido.' },
    { name: 'Pinza', desc: 'Si estás pegado, pinzazo rápido delante.' },
  ],
  combos: 'Con Ave brasa: te obliga a esperar mientras llueven brasas. Con Salamandra: esperar con el suelo lleno de lava.',
  art: 'Escorpión grande de caparazón de obsidiana negra y brillante con filos de cristal violáceo y vetas de magma; aguijón de cristal ardiente; pinzas grandes que cruza como un escudo. Partes: cuerpo, cola (3-4 segmentos + aguijón), 2 pinzas, 6 patas. ~1,7×1 u (cola aparte).',
  tuning: Object.assign(baseTuning(150, 50, 0.4, 4), {
    speed: [90, 20, 300, 5, 'Velocidad'], keepMin: [130, 0, 400, 10, 'Le gusta estar a (mín.)'], keepMax: [230, 50, 500, 10, 'Le gusta estar a (máx.)'],
    stingRange: [280, 80, 500, 10, 'Picotazo: alcance'], stingWindup: [0.6, 0.15, 2, 0.05, 'Picotazo: preparación (s)'], stingDmg: [14, 0, 50, 1, 'Picotazo: daño'],
    stingRec: [0.7, 0, 2, 0.05, 'Picotazo: cola clavada (s, castigo)'], guardChance: [0.45, 0, 1, 0.05, 'Probabilidad de guardia'],
    guardTime: [1.6, 0.3, 5, 0.1, 'Guardia: dura (s)'], counterDmg: [16, 0, 60, 1, 'Contraataque: daño'],
    pinchRange: [70, 20, 200, 5, 'Pinza: si estás a menos de'], pinchWindup: [0.3, 0.1, 1, 0.05, 'Pinza: preparación (s)'], pinchDmg: [10, 0, 40, 1, 'Pinza: daño'],
    cooldown: [1.3, 0.2, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, c = pCenter(), ad = Math.abs(dxP(e)), tail = scorpTail(e);
    const aimSting = () => { const a = Math.atan2(c.y - tail.y, c.x - tail.x); e.sA = a; };
    switch (e.st) {
      case 'roam':
        faceTo(e, p.x); e.cdT -= dt;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.speed, burst: 2.4 });
        if (e.cdT > 0 || !canStart(e)) break;
        if (ad < t.pinchRange && Math.abs(p.y - (e.y + e.foot)) < 60) { begin(e, 'Pinza'); go(e, 'pinchW'); break; }
        if (Math.random() < t.guardChance) { go(e, 'guard'); say(e, 'GUARDIA', '#e6e0ff', true); sfx(k => SFXLIB.glass(k, 0.6)); break; }
        if (Math.hypot(c.x - tail.x, c.y - tail.y) < t.stingRange) {
          begin(e, 'Picotazo'); go(e, 'stingW'); aimSting();
          warn({ e, kind: 'line', t: t.stingWindup, follow: w => { const tl = scorpTail(e); if (G.t - w.t0 < t.stingWindup * 0.7) aimSting(); else w.locked = true; w.ax = tl.x; w.ay = tl.y; w.bx = tl.x + Math.cos(e.sA) * t.stingRange; w.by = tl.y + Math.sin(e.sA) * t.stingRange; } });
        }
        break;
      case 'guard':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); faceTo(e, p.x);
        if (e.stT > t.guardTime) { go(e, 'roam'); e.cdT = t.cooldown * 0.6; }
        break;
      case 'counter':                                                          // picotazo rápido tras recibir en guardia
        e.mx = 0;
        if (windup(e, 0.12)) { aimSting(); scorpSting(e, t, t.counterDmg, 'Contraataque'); go(e, 'stingRec'); e.phase = 'recover'; }
        break;
      case 'stingW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); e.face = Math.cos(e.sA) >= 0 ? 1 : -1;
        if (windup(e, t.stingWindup)) { scorpSting(e, t, t.stingDmg, 'Picotazo'); go(e, 'stingRec'); e.phase = 'recover'; }
        break;
      case 'stingRec':
        e.phase = 'recover'; e.busy = false; e.mx = 0;
        if (e.stT > t.stingRec) { go(e, 'roam'); e.cdT = t.cooldown; }
        break;
      case 'pinchW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12)); faceTo(e, p.x);
        if (windup(e, t.pinchWindup)) { go(e, 'pinch'); release(e); if (rectHitsPlayer(e.x, e.y - e.ry - 10, e.x + e.face * (e.rx + 34), e.y + e.ry)) hitP(e, 'Pinza', t.pinchDmg, e.face * 360, -220); }
        break;
      case 'pinch':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.4) { go(e, 'roam'); e.cdT = t.cooldown * 0.7; }
        break;
    }
  },
  onDamage(e, o) {
    if (e.st !== 'guard' || o.dot || o.elem === 'burnTick') return;
    o.dmg = 0; o.kx = 0; o.ky = 0; o.launch = 0;
    say(e, '¡CONTRAATAQUE!', '#e6e0ff', true); begin(e, 'Contraataque'); go(e, 'counter'); release(e);
    const x = e.x + e.face * e.rx, y = e.y - 6; addFx(0.4, (ctx, layer, t0) => sparks(ctx, G.t, t0, x, y, layer, { n: 8, pal: PAL.void, sp: 240, seed: 4, len: 9, life: 0.3 }));
    sfx(k => SFXLIB.glass(k, 1));
  },
  draw(ctx, e, P) {
    const guard = e.st === 'guard' || e.st === 'counter' ? 1 : 0, raise = e.st === 'stingW' ? e.tell : e.st === 'counter' ? 1 : 0;
    for (let i = 0; i < 3; i++) for (const s of [-1, 1]) { const lx = -14 + i * 10, sw = Math.sin(e.wp * 1.5 + i + s) * (Math.abs(e.mx) > 10 ? 4 : 0); E_line(ctx, P, [[lx, 4], [lx + s * 6 + sw, 12], [lx + s * 8 + sw, e.ry]], 2.5, MAGMA.obs); }
    // cola: arco por encima; al picar se estira hacia el objetivo (dibujada en coordenadas del cuerpo)
    const st = e.st === 'stingRec' && e.stT < 0.25 ? 1 : 0;
    const pts = [[-28, -4], [-40, -20 - raise * 6], [-34, -40 - raise * 10], [-14, -50 - raise * 8]];
    if (st && e.stingTip) pts.push([(e.stingTip.x - e.x) * e.face, e.stingTip.y - e.y]);
    E_line(ctx, P, pts, 7, MAGMA.obsL);
    const tip = pts[pts.length - 1]; E_poly(ctx, P, [[tip[0] - 4, tip[1] - 3], [tip[0] + 12, tip[1] + 2], [tip[0] - 4, tip[1] + 5]], MAGMA.hot);
    if (!P.mono) glow(ctx, tip[0], tip[1], 12, [255, 140, 50], 0.5 + 0.4 * raise);
    E_ell(ctx, P, 0, 0, 32, 12, MAGMA.obs); E_ell(ctx, P, -2, -3, 28, 8, MAGMA.obsL);
    E_line(ctx, P, [[-18, -2], [-4, -5], [10, -2]], 1.6, MAGMA.glow);
    // pinzas: delante; en guardia, cruzadas y levantadas (brillo claro)
    for (const s of [-1, 1]) {
      ctx.save(); ctx.translate(28, 2 + s * 3); ctx.rotate(guard ? -0.9 + s * 0.35 : s * 0.15);
      E_line(ctx, P, [[0, 0], [12, -2]], 5, MAGMA.obsL);
      E_poly(ctx, P, [[10, -8], [26, -6], [24, 0], [12, 2]], guard ? '#cfc6ff' : '#4a4058');
      ctx.restore();
    }
    if (!P.mono && guard) glow(ctx, 40, -10, 34, [200, 190, 255], 0.35);
    E_eye(ctx, P, e, 26, -6, 2);
  },
});
function scorpTail(e) { return { x: e.x - e.face * 14, y: e.y - 50 }; }
function scorpSting(e, t, dmg, atk) {
  release(e); const tl = scorpTail(e), a = e.sA;
  e.stingTip = { x: tl.x + Math.cos(a) * t.stingRange, y: Math.min(GROUND - 4, tl.y + Math.sin(a) * t.stingRange) };
  for (let k = 0; k <= 12; k++) { const s = k / 12, x = tl.x + Math.cos(a) * t.stingRange * s, y = tl.y + Math.sin(a) * t.stingRange * s; if (circleHitsPlayer(x, y, 8)) { hitP(e, atk, dmg, Math.sign(Math.cos(a)) * 380, -240); break; } }
  sfx(k => SFXLIB.whoosh(k, 3600, 1200, 0.12, 0.2));
}
