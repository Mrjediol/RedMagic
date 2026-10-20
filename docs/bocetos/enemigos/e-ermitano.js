// e-ermitano.js — Ermitaño de hielo (mundo de hielo): cangrejo dentro de un bloque de hielo. Tiene el ÚNICO ataque que congela
// al player (su pinza: es el «aturdido» de este enemigo, con aspecto de hielo). Pregunta: ¿le rompes la coraza a golpes fuertes
// sin dejarte agarrar? Roto, sale el cangrejo: rápido y frágil.
'use strict';
addFoe({
  id: 'hermit', name: 'Ermitaño de hielo', short: 'Ermit.', world: 'hielo', rx: 34, ry: 30, home: 'shell',
  role: 'Blindado en dos fases (coraza → cangrejo)',
  question: 'Romperle la coraza (los golpes fuertes la agrietan mucho más) sin ponerte a tiro de su pinza, que te congela un segundo y te remata. Roto, cambia de ritmo: pequeño, rápido y frágil.',
  moves: [
    { name: 'Coraza', desc: 'Dentro del bloque de hielo recibe ×0,3. El daño que le haces lo agrieta (3 grietas = rota); un golpe fuerte cuenta como una grieta entera.' },
    { name: 'Pinza helada', desc: 'De cerca levanta la pinza (brilla) y da un tirón hacia delante. Si te pilla te CONGELA 1 s (no te mueves ni disparas) y al momento te da un pellizco fuerte. Si falla, se queda vendido un instante.' },
    { name: 'Al descubierto', desc: 'Con la coraza rota sale el cangrejo: corre de lado alrededor de ti y pellizca rápido (sin congelar). Poca vida.' },
  ],
  combos: 'Con Ventisca: el viento te empuja a su pinza. Con Erizo: la lluvia te obliga a acercarte. En pareja: uno te congela y el otro remata (cuidado con el ajuste de turnos).',
  art: 'Cangrejo ermitaño que lleva por concha un bloque de hielo facetado (transparente, con burbujas y vetas blancas, se ven sombras del cangrejo dentro); asoman 4 patas articuladas gris-rosado, dos ojos en pedúnculo y una pinza grande con punta de cristal azul. Al romperse: cangrejo pequeño y blando, rosado. Partes: concha (3 estados de grietas), cuerpo, pinza (abierta / cerrada), patas. ~1,7×1,5 u (con concha), 0,9×0,7 u (sin ella).',
  tuning: Object.assign(baseTuning(160, 50, 0.3, 4), {
    shellMul: [0.3, 0, 1, 0.05, 'Coraza: × daño recibido'], crackDmg: [45, 5, 300, 5, 'Coraza: daño (sin reducir) por grieta'], cracks: [3, 1, 6, 1, 'Coraza: grietas para romperse'],
    walk: [40, 0, 200, 5, 'Velocidad (con coraza)'], grabRange: [120, 30, 300, 5, 'Pinza: si estás a menos de'], grabWindup: [0.55, 0.1, 2, 0.05, 'Pinza: preparación (s)'],
    grabLunge: [110, 0, 300, 5, 'Pinza: tirón (px)'], freezeTime: [1.0, 0, 3, 0.1, 'Pinza: te congela (s)'], grabDmg: [4, 0, 30, 1, 'Pinza: daño al pillarte'],
    pinchDelay: [0.75, 0.2, 2, 0.05, 'Pinza: pellizco tras congelarte (s)'], pinchDmg: [12, 0, 60, 1, 'Pinza: daño del pellizco'],
    missRec: [0.7, 0, 2, 0.05, 'Pinza fallida: vendido (s)'], cooldown: [1.6, 0.2, 6, 0.1, 'Pausa entre ataques (s)'],
    coreHp: [45, 5, 300, 5, 'Al descubierto: vida máxima'], scuttle: [210, 50, 500, 5, 'Al descubierto: velocidad'],
    nipWindup: [0.25, 0.05, 1, 0.05, 'Al descubierto: preparación del pellizco (s)'], nipDmg: [6, 0, 40, 1, 'Al descubierto: daño del pellizco'],
  }),
  init(e) { e.crackAcc = 0; e.broken = false; e.side = 1; },
  think(e, dt) {
    const t = FT(e), p = G.player, dx = dxP(e), ad = Math.abs(dx);
    switch (e.st) {
      case 'shell':
        faceTo(e, p.x); e.cdT -= dt;
        walkDir(e, ad > 70 ? Math.sign(dx) : 0, t.walk, dt, 3);
        if (e.cdT <= 0 && ad < t.grabRange && Math.abs(p.y - (e.y + e.foot)) < 70 && canStart(e)) { begin(e, 'Pinza helada'); go(e, 'grabW'); }
        break;
      case 'grabW':
        e.mx = lerp(e.mx, -e.face * 15, Math.min(1, dt * 6)); faceTo(e, p.x);
        if (windup(e, t.grabWindup)) { go(e, 'grab'); release(e); e.caught = false; sfx(k => SFXLIB.whoosh(k, 1800, 600, 0.15, 0.16)); }
        break;
      case 'grab':
        e.phase = 'active'; e.mx = e.face * t.grabLunge / 0.18;
        if (!e.caught && rectHitsPlayer(e.x + e.face * e.rx * 0.5, e.y - e.ry, e.x + e.face * (e.rx + 30), e.y + e.ry) && hitP(e, 'Pinza helada', t.grabDmg, 0, 0)) { e.caught = true; freezePlayer(t.freezeTime); }
        if (e.stT > 0.18) { go(e, 'grabRec'); e.mx = 0; e.phase = 'recover'; }
        break;
      case 'grabRec':
        e.mx = 0; e.phase = 'recover'; e.busy = e.caught; e.noContact = e.caught;   // agarrado: el pellizco es el golpe, no el contacto
        if (e.caught && !e.pinched && e.stT > t.pinchDelay) { e.pinched = true; release(e); if (Math.abs(G.player.x - e.x) < e.rx + 60) hitP(e, 'Pellizco', t.pinchDmg, e.face * 380, -260); }
        if (e.stT > (e.caught ? t.pinchDelay + 0.4 : t.missRec)) { go(e, 'shell'); recover(e); e.pinched = false; e.noContact = false; e.cdT = t.cooldown; }
        break;
      // ---- al descubierto
      case 'scuttle': {
        e.cdT -= dt; faceTo(e, p.x);
        if (Math.random() < dt * 0.6) e.side = -e.side;
        walkTo(e, clamp(p.x + e.side * 70, ARENA.x0 + 30, ARENA.x1 - 30), t.scuttle, dt, 8);
        if (e.cdT <= 0 && ad < 80 && canStart(e)) { begin(e, 'Pellizco'); go(e, 'nipW'); }
        break;
      }
      case 'nipW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 14)); faceTo(e, p.x);
        if (windup(e, t.nipWindup)) {
          go(e, 'nip'); release(e);
          if (rectHitsPlayer(e.x, e.y - e.ry - 10, e.x + e.face * (e.rx + 30), e.y + e.ry)) hitP(e, 'Pellizco', t.nipDmg, e.face * 300, -200);
        }
        break;
      case 'nip':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.35) { go(e, 'scuttle'); e.cdT = 0.9; }
        break;
    }
  },
  onInterrupt(e) { go(e, e.broken ? 'scuttle' : 'shell'); },
  onDamage(e, o) {
    if (e.broken || o.dot || o.elem === 'burnTick') return;
    const t = FT(e), before = Math.floor(e.crackAcc / t.crackDmg);
    e.crackAcc += (o.dmg || 0) + (o.heavy ? t.crackDmg : 0);
    const n = Math.floor(e.crackAcc / t.crackDmg);
    if (n >= t.cracks) { breakShell(e, t); return; }                         // el golpe que la rompe entra entero
    o.dmg = (o.dmg || 0) * t.shellMul; o.kx = (o.kx || 0) * 0.3;
    if (n > before) { say(e, '¡CRAC!', '#cdf6ff', true); sfx(k => SFXLIB.glass(k, 1)); } else say(e, 'BLOQUEO', '#c8d0dc');
  },
  draw(ctx, e, P) {
    const t = FT(e), claw = e.st === 'grabW' ? e.tell : e.st === 'grab' ? 1 : e.st === 'nipW' ? e.tell : 0, cracks = Math.min(t.cracks, Math.floor(e.crackAcc / t.crackDmg));
    if (e.broken) {                                                             // cangrejo al descubierto
      for (let i = 0; i < 4; i++) { const lx = -10 + i * 7, sw = Math.sin(e.wp * 2 + i * 1.7) * 4; E_line(ctx, P, [[lx, 0], [lx + sw - 4, 8], [lx + sw, e.ry]], 2.5, '#a58880'); }
      E_ell(ctx, P, 0, 0, 16, 11, '#c9a8a0'); E_ell(ctx, P, -2, -2, 12, 7, '#dcbcb4');
      E_poly(ctx, P, [[12, -2], [24 + claw * 6, -8], [26 + claw * 6, 2], [14, 4]], '#b8968e');
      E_line(ctx, P, [[6, -9], [8, -16]], 1.5, '#a58880'); E_line(ctx, P, [[10, -9], [13, -15]], 1.5, '#a58880');
      E_eye(ctx, P, e, 8, -17, 1.8); E_eye(ctx, P, e, 13, -16, 1.8);
      return;
    }
    for (let i = 0; i < 4; i++) { const lx = -22 + i * 13, sw = Math.sin(e.wp * 1.2 + i * 1.7) * (Math.abs(e.mx) > 8 ? 5 : 0); E_line(ctx, P, [[lx, 14], [lx + sw - 6, 22], [lx + sw, e.ry]], 4, '#a58880'); }
    // pinza (delante, sube al preparar)
    ctx.save(); ctx.translate(30, 6); ctx.rotate(-claw * 0.7);
    E_line(ctx, P, [[0, 0], [10, -4]], 6, '#a58880');
    E_poly(ctx, P, [[8, -10], [28 + claw * 4, -14], [30 + claw * 4, -6], [10, -2]], '#b8968e');
    E_poly(ctx, P, [[8, 2], [26, 6 + claw * 6], [24, 10 + claw * 6], [10, 6]], '#b8968e');
    E_poly(ctx, P, [[26 + claw * 4, -14], [34 + claw * 4, -12], [30 + claw * 4, -6]], ICE.light);
    ctx.restore();
    E_line(ctx, P, [[18, 0], [22, -12]], 2, '#a58880'); E_eye(ctx, P, e, 22, -13, 2);
    // coraza: bloque de hielo facetado (translúcido) con grietas
    ctx.save(); if (!P.mono) ctx.globalAlpha *= 0.92;
    E_poly(ctx, P, [[-32, 16], [-30, -18], [-14, -30], [16, -28], [28, -12], [26, 16]], ICE.light);
    E_poly(ctx, P, [[-24, -14], [-12, -24], [10, -22], [0, -6], [-18, 4]], ICE.pale);
    E_poly(ctx, P, [[-20, -18], [-12, -22], [-16, -10]], ICE.snow);
    ctx.restore();
    const R = rand(e.id * 7);
    for (let i = 0; i < cracks * 2; i++) { const x0 = -20 + R() * 40, y0 = -20 + R() * 30; E_line(ctx, P, [[x0, y0], [x0 + (R() - 0.5) * 20, y0 + (R() - 0.5) * 16], [x0 + (R() - 0.5) * 28, y0 + (R() - 0.5) * 24]], 1.6, '#ffffff'); }
  },
});
function breakShell(e, t) {
  e.broken = true; e.atkId++; e.busy = false; e.phase = null; e.tell = 0;
  e.hp = Math.min(e.hp, t.coreHp); e.hpMax = Math.max(e.hp, 1); e.rx = 16; e.ry = 12; e.foot = 12;
  go(e, 'scuttle'); e.cdT = 0.8; e.stunT = 0.4;
  say(e, '¡CORAZA ROTA!', '#cdf6ff', true); G.shake += 5; sfx(k => SFXLIB.shatter(k, 1));
  const x = e.x, y = e.y; hitFx('shatter', x, y);
}
