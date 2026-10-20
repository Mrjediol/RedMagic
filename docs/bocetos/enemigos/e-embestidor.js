// e-embestidor.js — Embestidor: cuadrúpedo pesado que carga en línea recta. Pregunta: ¿sabes saltarlo en el último momento
// y ponerte delante de una pared para que se estampe?
'use strict';
addFoe({
  id: 'charger', name: 'Embestidor', short: 'Embest.', rx: 44, ry: 30, home: 'prowl', platforms: true,
  role: 'Tanque de suelo que carga',
  question: 'Saltar o esquivar la carga en el último momento; colocarte delante de una pared para que se estampe y quede vendido.',
  moves: [
    { name: 'Embestida', desc: 'Rasca el suelo (polvo + chevrones del alto de su cuerpo) y carga en línea recta. Fija la dirección un poco antes de salir: si te mueves al final, falla. Si choca con la pared se queda aturdido y recibe ×1,5. Si le saltas, derrapa y queda de espaldas. Atropella a los demás enemigos que encuentra.' },
    { name: 'Pisotón', desc: 'Si estás pegado a él: se encabrita y pisa. Zona circular en el suelo; saltar la esquiva.' },
    { name: 'Embestida con salto', desc: 'Si estás en una plataforma baja, la carga acaba en un salto hasta tu plataforma (flechas también a esa altura) y sigue corriendo por ella hasta caerse por el borde. Más alta de lo que alcanza, no te llega: se queda debajo esperando a que bajes.' },
  ],
  combos: 'Con Fatuos: atrae su carga hacia un fatuo o hacia otros enemigos (los atropella). Con Artilleros: la carga te obliga a moverte mientras caen morteros.',
  art: 'Silueta ancha y baja, cuatro patas cortas, cabeza gacha con cuernos/colmillos de cristal. Jabalí de raíces, carnero de piedra, escarabajo rinoceronte.',
  tuning: Object.assign(baseTuning(260, 70, 0.35, 5), {
    keepMin: [220, 0, 600, 10, 'Distancia mínima (se aparta)'], keepMax: [420, 100, 900, 10, 'Distancia máxima (se acerca)'],
    walk: [80, 0, 300, 5, 'Velocidad andando'],
    windup: [0.8, 0.2, 2, 0.05, 'Embestida: preparación (s)'], lockAt: [0.7, 0, 1, 0.05, 'Embestida: fija la dirección a esta fracción'],
    chargeSpeed: [640, 200, 1200, 10, 'Embestida: velocidad'], chargeMax: [720, 200, 1400, 10, 'Embestida: recorrido máximo'],
    passBy: [150, 0, 500, 10, 'Embestida: sigue tanto tras pasarte'], chargeDmg: [18, 0, 60, 1, 'Embestida: daño'],
    allyDmg: [12, 0, 60, 1, 'Embestida: daño a otros enemigos'], crashStun: [1.8, 0, 5, 0.1, 'Choque con pared: aturdido (s)'],
    crashVuln: [1.5, 1, 3, 0.05, 'Choque con pared: × daño recibido'], skidTime: [0.55, 0.1, 2, 0.05, 'Derrape (s, ventana de castigo)'],
    stompRange: [120, 0, 300, 5, 'Pisotón: si estás a menos de'], stompWindup: [0.5, 0.1, 1.5, 0.05, 'Pisotón: preparación (s)'],
    stompR: [130, 30, 300, 5, 'Pisotón: radio'], stompDmg: [12, 0, 60, 1, 'Pisotón: daño'],
    leapReach: [160, 0, 300, 5, 'Embestida con salto: salta a plataformas hasta esta altura'],
    cooldown: [1.3, 0, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'prowl': {
        faceTo(e, p.x); e.cdT -= dt;
        roam(e, dt, { min: t.keepMin, max: t.keepMax, speed: t.walk, burst: 2.2, hop: 0.15 });
        if (e.cdT > 0 || !canStart(e)) break;
        {
          const pl = p.onGround && p.y < GROUND - 2 ? PLATFORMS.find(q => onPlatform(q)) : null, reach = !pl || GROUND - pl.y <= t.leapReach;
          if (ad < t.stompRange && p.y > GROUND - 140) {
            begin(e, 'Pisotón'); go(e, 'stompW'); warn({ e, kind: 'circle', x: e.x, r: t.stompR, t: t.stompWindup });
          } else if (ad < t.chargeMax + 100 && reach && (pl || p.y > GROUND - 120)) {
            begin(e, pl ? 'Embestida con salto' : 'Embestida'); go(e, 'chargeW'); e.cdir = Math.sign(dx) || 1; e.leapPl = pl; e.leapt = false;
            warn({ e, kind: 'arrow', x: e.x, dir: e.cdir, len: e.rx * 2.4, h: e.ry * 2, t: t.windup, follow: w => { w.x = e.x + e.cdir * e.rx * 0.6; w.dir = e.cdir; } });
            if (pl) warn({ e, kind: 'arrow', x: e.cdir > 0 ? pl.x0 : pl.x1, y: pl.y - 2, dir: e.cdir, len: e.rx * 1.6, h: e.ry * 2, t: t.windup });
          } else if (pl && !reach) walkTo(e, clamp(p.x, pl.x0 - 60, pl.x1 + 60), t.walk, dt);   // no llega: espera debajo
        }
        break;
      }
      case 'chargeW':
        if (e.stT < t.windup / (e.rage || 1) * t.lockAt) { e.cdir = Math.sign(dx) || e.cdir; e.face = e.cdir; }
        e.mx = lerp(e.mx, -e.cdir * 25, Math.min(1, dt * 6));
        if (Math.random() < dt * 5) dustPuff(e.x - e.cdir * e.rx * 0.8, GROUND);
        if (windup(e, t.windup / (e.rage || 1))) { go(e, 'charge'); release(e); e.run = 0; e.hitP = false; e.bowled = new Set(); sfx(k => SFXLIB.whoosh(k, 900, 300, 0.4, 0.25)); }
        break;
      case 'charge': {
        e.phase = 'active';
        const sp = t.chargeSpeed * (e.rage || 1) * Math.min(1, 0.35 + e.stT * 5);
        e.mx = e.cdir * sp; e.face = e.cdir; e.run += sp * dt;
        // con salto: despega justo para caer sobre el borde de la plataforma (sube h en tr segundos mientras avanza sp·tr)
        if (e.leapPl && !e.leapt && e.onGround) {
          const pl = e.leapPl, h = GROUND - pl.y + 10, tr = Math.sqrt(2 * h / GRAV), edge = e.cdir > 0 ? pl.x0 : pl.x1;
          if ((e.x - (edge - e.cdir * t.chargeSpeed * (e.rage || 1) * tr * 0.85)) * e.cdir >= 0) { e.vy = -Math.sqrt(2 * GRAV * h); e.leapt = true; e.onGround = false; e.y -= 1; }
        }
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, e.leapPl ? 'Embestida con salto' : 'Embestida', t.chargeDmg, e.cdir * 520, -300)) e.hitP = true;
        for (const o of alive()) if (o !== e && o.def && !e.bowled.has(o) && Math.abs(o.x - e.x) < e.rx + o.rx && Math.abs(o.y - e.y) < e.ry + o.ry) {
          e.bowled.add(o); damage(o, { dmg: t.allyDmg, kx: e.cdir * 520, ky: -200, launch: 0.4, heavy: true, color: '#ffd0a0' }); say(o, '¡ATROPELLADO!', '#ffd0a0', true);
        }
        if (Math.random() < dt * 8) dustPuff(e.x - e.cdir * e.rx, GROUND);
        if (e.wall === e.cdir && e.stT > 0.05) chargerCrash(e, t);
        else if (e.run > t.chargeMax || (p.x - e.x) * e.cdir < -t.passBy) { go(e, 'skid'); recover(e); }
        break;
      }
      case 'skid':
        e.phase = 'recover';
        if (e.wall === Math.sign(e.mx) && Math.abs(e.mx) > 260) { chargerCrash(e, t); break; }
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 5));
        if (Math.random() < dt * 10) dustPuff(e.x + Math.sign(e.mx) * e.rx * 0.6, GROUND);
        if (e.stT > t.skidTime) { go(e, 'prowl'); e.cdT = t.cooldown / (e.rage || 1); }
        break;
      case 'crash':
        e.phase = 'recover'; e.mx = 0;
        if (e.stT > 0.1) { go(e, 'prowl'); e.cdT = t.cooldown / (e.rage || 1); }
        break;
      case 'stompW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (windup(e, t.stompWindup)) {
          go(e, 'stomp'); release(e);
          if (p.y > GROUND - 30 && Math.abs(p.x - e.x) < t.stompR) hitP(e, 'Pisotón', t.stompDmg, Math.sign(dx || 1) * 360, -400);
          const x = e.x; addFx(1.2, (ctx, layer, t0) => groundImpact(ctx, G.t, layer, t0, x, t.stompR / 110, { core: '#fff3e0', light: '#e8c9a8', mid: '#b08a66', deep: '#6b5442', dark: '#3a2c20', g1: [200, 150, 100], g2: [240, 210, 170] }, { seed: 4 }));
          G.shake += 6; sfx(k => { const t0 = nowT(); vThump(t0, 90, 35, 0.4 * k, 0.6, 2); });
        }
        break;
      case 'stomp':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.55) { go(e, 'prowl'); e.cdT = t.cooldown / (e.rage || 1); }
        break;
    }
  },
  draw(ctx, e, P) {
    const charging = e.st === 'charge', rear = e.st === 'stompW' ? e.tell : e.st === 'stomp' ? Math.max(0, 1 - e.stT * 6) : 0;
    const lean = e.st === 'chargeW' ? -0.06 * e.tell : charging ? 0.08 : 0;
    ctx.save(); ctx.translate(-30, e.ry); ctx.rotate(-rear * 0.32 + lean); ctx.translate(30, -e.ry);
    // patas
    for (let i = 0; i < 4; i++) {
      const lx = [-30, -15, 16, 31][i], sw = Math.sin(e.wp * (charging ? 1.6 : 1) + i * Math.PI) * (Math.abs(e.mx) > 20 ? 7 : 0) + (e.st === 'chargeW' && i === 3 ? Math.sin(G.t * 26) * 6 : 0);
      E_line(ctx, P, [[lx, 6], [lx + sw, e.ry]], 9, i % 2 ? '#3d3024' : '#4a3b2c');
    }
    E_ell(ctx, P, 0, -2, 44, 25, '#4a3b2c');
    E_ell(ctx, P, -4, -6, 40, 21, '#6b5640');
    for (const [x, y, r] of [[-20, -22, 10], [-4, -25, 12], [14, -22, 9]]) E_ell(ctx, P, x, y, r, r * 0.6, '#4f7a3e');
    // cabeza y cuernos de cristal
    const hy = charging ? 10 : 3;
    E_ell(ctx, P, 38, hy, 18, 15, '#5a4734');
    E_poly(ctx, P, [[46, hy + 4], [72, hy - 6 + (charging ? 6 : 0)], [50, hy - 4]], '#7fe8ff');
    E_poly(ctx, P, [[40, hy - 8], [58, hy - 24 + (charging ? 10 : 0)], [46, hy - 10]], '#bff6ff');
    E_eye(ctx, P, e, 42, hy - 4, 3.4);
    ctx.restore();
  },
});
function chargerCrash(e, t) {
  go(e, 'crash'); recover(e); e.mx = 0; e.vx = -e.cdir * 160; e.stunT = t.crashStun; makeVuln(e, t.crashVuln, t.crashStun);
  const x = e.x + e.cdir * e.rx;
  text(x, e.y - e.ry - 40, '¡CRASH!', '#ffd27a', true); G.shake += 9;
  addFx(1.3, (ctx, layer, t0) => debris(ctx, G.t, t0, x, layer, { n: 9, c1: '#5a4632', c2: '#8a6a52', seed: 7 }));
  addFx(0.8, (ctx, layer, t0) => impact(ctx, G.t, layer, t0, x, e.y, 0.7, PAL.wind, { dir: e.cdir > 0 ? Math.PI : 0, spread: 2.2, seed: 3 }));
  sfx(k => { const t0 = nowT(); vThump(t0, 110, 40, 0.35 * k, 0.8, 3); vNoise(t0, 0.25 * k, 0.3, 'lowpass', 2500, 200, 0.8, { att: 0.002 }); });
}
