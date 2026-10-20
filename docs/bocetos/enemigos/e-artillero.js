// e-artillero.js — Artillero: planta enraizada que dispara morteros y deja charcos. Pregunta: ¿sabes acercarte bajo el fuego
// y pegarle cuando está abierto, no cuando se cierra?
'use strict';
const MORTAR_PAL = { core: '#fff0fb', light: '#ffb0e6', mid: '#e04aa8', deep: '#8a1a6a', dark: '#3a0a2c', g: [230, 70, 170], g1: [220, 60, 160], g2: [255, 170, 230] };
addFoe({
  id: 'mortar', name: 'Artillero', short: 'Artill.', rx: 28, ry: 40, home: 'idle',
  role: 'Torreta enraizada: niega zonas a distancia',
  question: 'Acercarte esquivando los morteros (no quedarte quieto ni en los charcos) y castigarle cuando está abierto; de cerca se cierra y suelta púas.',
  moves: [
    { name: 'Mortero', desc: 'Se hincha y escupe 3 bolas en parábola a donde vas a estar (una en tu sitio y dos a los lados). Cada una marca en el suelo su explosión desde que sale. Al caer deja un charco que hace daño unos segundos.' },
    { name: 'Cerrarse', desc: 'Si te pegas a él se cierra en su corteza: recibe sólo ×0,2 (los golpes fuertes ×0,5).' },
    { name: 'Brote', desc: 'Tras cerrarse, se abre de golpe soltando un abanico de púas hacia arriba y a los lados. Después queda abierto un rato sin poder cerrarse: tu ventana.' },
  ],
  combos: 'Detrás de un Caparazón (el escudo tapa, el artillero bombardea). Con Embestidor: la carga te empuja a los charcos.',
  art: 'Planta bulbosa con raíces a la vista, boca que se abre hacia ti y corteza que se cierra como una piña. Flor carnívora, seta de cristal.',
  tuning: Object.assign(baseTuning(140, 40, 0, 0), {
    range: [820, 100, 1400, 10, 'Alcance'], shots: [3, 1, 7, 1, 'Mortero: bolas por ataque'], spread: [85, 0, 250, 5, 'Mortero: separación entre bolas'],
    flight: [1.1, 0.4, 2.5, 0.05, 'Mortero: tiempo de vuelo (s)'], lead: [0.5, 0, 1.5, 0.05, 'Mortero: adelanta tu movimiento (×vuelo)'],
    shotDmg: [12, 0, 60, 1, 'Mortero: daño'], blastR: [46, 15, 150, 1, 'Mortero: radio de la explosión'],
    puddleTime: [2.5, 0, 8, 0.1, 'Charco: duración (s)'], puddleDmg: [4, 0, 30, 1, 'Charco: daño por toque'],
    windup: [0.7, 0.1, 2, 0.05, 'Mortero: preparación (s)'], cooldown: [2.2, 0.2, 6, 0.1, 'Pausa entre morteros (s)'],
    hideRange: [150, 0, 400, 5, 'Se cierra si estás a menos de'], hideTime: [1.3, 0.2, 4, 0.1, 'Cerrado (s)'],
    hideMul: [0.2, 0, 1, 0.05, 'Cerrado: × daño recibido'], burstWindup: [0.35, 0.1, 1.5, 0.05, 'Brote: preparación (s)'],
    ringN: [8, 3, 16, 1, 'Brote: púas'], ringSpeed: [190, 60, 500, 5, 'Brote: velocidad'], ringDmg: [8, 0, 40, 1, 'Brote: daño'],
    hideGrace: [2.0, 0, 6, 0.1, 'Tras el brote: tiempo sin poder cerrarse (s)'],
  }),
  init(e) { e.grace = 0; },
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'idle':
        faceTo(e, p.x); e.cdT -= dt; e.grace -= dt;
        if (ad < t.hideRange && e.grace <= 0) { go(e, 'hide'); sfx(k => SFXLIB.whoosh(k, 700, 250, 0.2, 0.12)); break; }
        if (e.cdT <= 0 && ad < t.range && canStart(e)) { begin(e, 'Mortero'); go(e, 'aim'); }
        break;
      case 'aim':
        faceTo(e, p.x);
        if (!windup(e, t.windup)) break;
        go(e, 'fire'); release(e);
        for (let i = 0; i < t.shots; i++) {
          const off = (i - (t.shots - 1) / 2) * t.spread, T = t.flight + Math.abs(off) / Math.max(1, t.spread) * 0.12;
          const tx = clamp(p.x + p.vx * T * t.lead + off, ARENA.x0 + 10, ARENA.x1 - 10), ty = surfaceUnder(tx, p.y);
          lobShot(e.x + e.face * 8, e.y - e.ry + 10, tx, ty, T, { e, atk: 'Mortero', dmg: t.shotDmg, r: 7, style: 'orb', pal: MORTAR_PAL, warnR: t.blastR,
            onLand: (x, y) => { foeBlast(e, x, y - 6, t.blastR, t.shotDmg, 'Mortero', { pal: MORTAR_PAL }); if (t.puddleTime > 0) foeZone(e, x, y, { halfW: t.blastR * 0.8, life: t.puddleTime, dmg: t.puddleDmg, atk: 'Charco', pal: MORTAR_PAL }); } });
        }
        sfx(k => { const t0 = nowT(); vThump(t0, 160, 60, 0.18 * k, 0.4, 2); vNoise(t0, 0.15 * k, 0.15, 'bandpass', 900, 300, 1, { att: 0.002 }); });
        break;
      case 'fire':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.5) { go(e, 'idle'); e.cdT = t.cooldown; }
        break;
      case 'hide':
        if (e.stT > t.hideTime && canStart(e)) { begin(e, 'Brote'); go(e, 'burstW'); warn({ e, kind: 'ring', x: e.x, y: e.y - 6, r: e.ry * 1.4, t: t.burstWindup }); }
        break;
      case 'burstW':
        if (!windup(e, t.burstWindup)) break;
        go(e, 'burst'); release(e);
        for (let i = 0; i < t.ringN; i++) {
          const a = -Math.PI + (i + 0.5) * Math.PI / t.ringN;
          foeShot({ e, atk: 'Brote', x: e.x, y: e.y - 10, vx: Math.cos(a) * t.ringSpeed, vy: Math.sin(a) * t.ringSpeed, r: 5, dmg: t.ringDmg, pal: MORTAR_PAL, life: 3 });
        }
        G.shake += 3; sfx(k => SFXLIB.whoosh(k, 2200, 700, 0.2, 0.18));
        break;
      case 'burst':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.45) { go(e, 'idle'); e.grace = t.hideGrace; e.cdT = Math.max(e.cdT, 1); }
        break;
    }
  },
  onDamage(e, o) {
    if (e.st !== 'hide' && e.st !== 'burstW') return;
    if (o.dot || o.elem === 'burnTick') return;
    o.dmg = (o.dmg || 0) * (o.heavy ? 0.5 : FT(e).hideMul);
    say(e, 'BLOQUEO', '#c8d0dc');
  },
  draw(ctx, e, P) {
    const closed = e.st === 'hide' ? Math.min(1, e.stT * 6) : e.st === 'burstW' ? 1 - e.tell * 0.3 : e.st === 'burst' ? Math.max(0, 0.7 - e.stT * 4) : 0;
    const swell = e.st === 'aim' ? e.tell : 0, ry = e.ry;
    // raíces
    for (const [a, b] of [[-26, -8], [-14, -2], [12, 4], [26, 10]]) E_line(ctx, P, [[0, ry - 12], [a * 0.6, ry - 4], [a, ry + 2], [a + b * 0.3, ry]], 5, '#4a3626');
    E_ell(ctx, P, 0, ry - 6, 22, 9, '#5a4632');
    // tallo
    const hy = -14 + closed * 30;
    E_line(ctx, P, [[0, ry - 8], [-4, (ry + hy) / 2], [0, hy + 10]], 10, '#2c5a36');
    E_poly(ctx, P, [[-3, 10], [-26, -2], [-4, 2]], '#3c7a46'); E_poly(ctx, P, [[3, 16], [24, 4], [4, 8]], '#3c7a46');
    if (closed > 0.5) {
      // capullo cerrado: placas de corteza
      E_ell(ctx, P, 0, hy, 22, 20, '#4a3828');
      for (let i = 0; i < 4; i++) E_poly(ctx, P, [[-20 + i * 10, hy + 14], [-14 + i * 10, hy - 18], [-6 + i * 10, hy + 14]], i % 2 ? '#6b5440' : '#5a4632');
      return;
    }
    // bulbo con boca hacia el frente
    const s = 1 + swell * 0.18;
    E_ell(ctx, P, 0, hy, 24 * s, 21 * s, '#1f4a30');
    E_ell(ctx, P, -3, hy - 3, 21 * s, 17 * s, '#2f6b45');
    for (const [x, y] of [[-12, -10], [-2, -16], [8, -12]]) E_ell(ctx, P, x * s, hy + y * s, 3, 3, '#7fe8ff');
    const mo = 0.4 + swell * 0.6;
    E_ell(ctx, P, 16 * s, hy + 2, 9 * s, 10 * mo * s, '#2a0a20');
    if (!P.mono) { glow(ctx, 16 * s, hy + 2, 18 * s, MORTAR_PAL.g, 0.25 + 0.5 * swell); }
    E_ell(ctx, P, 16 * s, hy + 2, 4 * s, 5 * mo * s, MORTAR_PAL.mid);
    E_eye(ctx, P, e, 6, hy - 6, 3);
  },
});
