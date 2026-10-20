// e-atrapamoscas.js — Atrapamoscas (bosque): planta carnívora plantada que lanza una LIANA; si te pilla te arrastra hasta su boca y
// te muerde. Pregunta: ¿esquivas la liana (va recta por la línea marcada), y si te pilla te sueltas a tiempo (dash, o pegándole fuerte)?
'use strict';
addFoe({
  id: 'flytrap', name: 'Atrapamoscas', short: 'Atrap.', world: 'bosque', rx: 26, ry: 30, home: 'wait', trap: true,
  role: 'Trampa plantada que te arrastra',
  question: 'Apartarte de la línea de la liana; si te atrapa, soltarte antes de llegar a la boca: con el dash, o haciéndole daño suficiente mientras tira de ti (puedes seguir disparando). De cerca, cuidado con el mordisco.',
  moves: [
    { name: 'Liana', desc: 'Se encoge y apunta: una línea fina va de su boca a ti y se fija (blanca) justo antes de lanzar. La liana sale recta y rápida por esa línea; si falla, la recoge despacio (castigo).' },
    { name: 'Arrastrar', desc: 'Si la liana te pilla, tira de ti hacia su boca (no puedes andar ni saltar; sí disparar). Te sueltas con el dash o si le haces daño bastante mientras tira (su aguante). Si llegas a la boca, mordisco fuerte que te lanza.' },
    { name: 'Mordisco', desc: 'Si estás pegado a ella, cierra la boca de golpe (preparación corta).' },
  ],
  combos: 'Con Zarza: te deja quieto delante del muro de espinas. Con Saltasetas: te arrastra debajo de un gran salto. Plantada: es una planta, no se mueve (pero te sigue con la cabeza).',
  art: 'Planta atrapamoscas grande: dos lóbulos de boca con dientes de cristal turquesa y el interior rojo; tallo grueso, hojas anchas en la base y raíces; la liana es un zarcillo verde con espinitas. Partes: base (hojas), tallo, boca (abierta / cerrada), liana (segmentos). ~1,3×1,5 u.',
  tuning: Object.assign(baseTuning(130, 45, 0, 0), {
    snapRange: [80, 20, 200, 5, 'Mordisco: si estás a menos de'], snapWindup: [0.35, 0.1, 1, 0.05, 'Mordisco: preparación (s)'], snapDmg: [12, 0, 50, 1, 'Mordisco: daño'],
    vineRange: [330, 80, 700, 10, 'Liana: alcance'], vineWindup: [0.7, 0.2, 2, 0.05, 'Liana: apuntar (s)'], lockAt: [0.75, 0, 1, 0.05, 'Liana: fija la línea a esta fracción'],
    vineSpeed: [950, 200, 2000, 10, 'Liana: velocidad'], reelSpeed: [260, 50, 600, 10, 'Arrastrar: velocidad'], biteDmg: [16, 0, 60, 1, 'Arrastrar: mordisco al llegar'],
    missRec: [0.8, 0, 3, 0.05, 'Liana fallida: recogiendo (s, castigo)'], cooldown: [2.2, 0.2, 6, 0.1, 'Pausa entre lianas (s)'],
  }),
  init(e) { e.vlen = 0; },
  think(e, dt) {
    const t = FT(e), p = G.player, c = pCenter(), m = flytrapMouth(e), ad = Math.abs(dxP(e)), dist = Math.hypot(c.x - m.x, c.y - m.y);
    switch (e.st) {
      case 'wait':
        faceTo(e, p.x); e.cdT -= dt;
        if (!canStart(e)) break;
        if (ad < t.snapRange && Math.abs(p.y - (e.y + e.foot)) < 70) { begin(e, 'Mordisco'); go(e, 'snapW'); }
        else if (e.cdT <= 0 && dist < t.vineRange) {
          begin(e, 'Liana'); go(e, 'vineW'); e.vA = Math.atan2(c.y - m.y, c.x - m.x);
          warn({ e, kind: 'line', t: t.vineWindup, follow: w => { const mm = flytrapMouth(e); if (G.t - w.t0 < t.vineWindup * t.lockAt) { const cc = pCenter(); e.vA = Math.atan2(cc.y - mm.y, cc.x - mm.x); } else w.locked = true; w.ax = mm.x; w.ay = mm.y; w.bx = mm.x + Math.cos(e.vA) * t.vineRange; w.by = mm.y + Math.sin(e.vA) * t.vineRange; } });
        }
        break;
      case 'snapW':
        faceTo(e, p.x);
        if (windup(e, t.snapWindup)) {
          go(e, 'snap'); release(e);
          if (rectHitsPlayer(e.x, e.y - e.ry - 10, e.x + e.face * (e.rx + 44), e.y + e.ry)) hitP(e, 'Mordisco', t.snapDmg, e.face * 380, -260);
          sfx(k => { const t0 = nowT(); vThump(t0, 260, 120, 0.1 * k, 0.3, 1); });
        }
        break;
      case 'snap':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.5) { go(e, 'wait'); e.cdT = Math.max(e.cdT, 0.8); }
        break;
      case 'vineW':
        e.face = Math.cos(e.vA) >= 0 ? 1 : -1;
        if (windup(e, t.vineWindup)) { go(e, 'vine'); release(e); e.vlen = 0; sfx(k => SFXLIB.whoosh(k, 1800, 900, 0.2, 0.14)); }
        break;
      case 'vine': {
        e.phase = 'active'; e.vlen += t.vineSpeed * dt;
        const tx = m.x + Math.cos(e.vA) * e.vlen, ty = m.y + Math.sin(e.vA) * e.vlen;
        if (circleHitsPlayer(tx, ty, 10)) {
          if (G.es.dashIframes && p.dashT > 0) { hitP(e, 'Liana', 1); }          // con el dash la atraviesas («ESQUIVA»)
          else if (!G.es.harmless) {
            p.dragBy = e; p.dragSt = 'reel'; go(e, 'reel'); e.phase = 'windup'; e.poiseDmg = 0;
            text(p.x, p.y - 96, '¡ATRAPADO!', '#c9e08a', true);
            const F = G.fight; if (F) { const k = 'Atrapamoscas · Liana (te atrapa)'; (F.hits[k] = F.hits[k] || { n: 0, dmg: 0 }).n++; }
            break;
          }
        }
        if (e.vlen > t.vineRange || ty > GROUND) { go(e, 'retract'); recover(e); }
        break;
      }
      case 'reel': {
        e.phase = 'windup'; e.tell = 0.5 + 0.5 * Math.sin(G.t * 20);   // tirando: se le puede interrumpir con daño (te suelta)
        if (p.dashT > 0) { p.dragBy = null; say(e, '¡SUELTA!', '#c9e08a', true); go(e, 'retract'); recover(e); break; }
        const tgt = e.x + e.face * (e.rx + 14);
        p.x += clamp(tgt - p.x, -t.reelSpeed * dt, t.reelSpeed * dt); if (!p.onGround) p.vy = Math.max(p.vy, 160);
        e.vlen = Math.hypot(c.x - m.x, c.y - m.y); e.vA = Math.atan2(c.y - m.y, c.x - m.x);
        if (Math.abs(tgt - p.x) < 4 && p.y > GROUND - 40) {
          p.dragBy = null; go(e, 'bite'); release(e);
          hitP(e, 'Mordisco (arrastrado)', t.biteDmg, e.face * 460, -360);
          sfx(k => { const t0 = nowT(); vThump(t0, 240, 100, 0.12 * k, 0.4, 1); });
        }
        if (e.stT > 4) { p.dragBy = null; go(e, 'retract'); recover(e); }
        break;
      }
      case 'bite':
        e.phase = 'recover'; e.busy = false; e.vlen = Math.max(0, e.vlen - 1500 * dt);
        if (e.stT > 0.7) { go(e, 'wait'); e.cdT = t.cooldown; }
        break;
      case 'retract':
        e.phase = 'recover'; e.busy = false; e.vlen = Math.max(0, e.vlen - t.vineRange / Math.max(0.1, t.missRec) * dt);
        if (e.vlen <= 0 && e.stT > 0.2) { go(e, 'wait'); e.cdT = t.cooldown; }
        break;
    }
  },
  onInterrupt(e) { if (G.player.dragBy === e) { G.player.dragBy = null; text(G.player.x, G.player.y - 96, '¡LIBRE!', '#c9e08a', true); } go(e, 'retract'); },
  onDeath(e) { if (G.player.dragBy === e) G.player.dragBy = null; },
  draw(ctx, e, P) {
    const open = e.st === 'vineW' || e.st === 'vine' || e.st === 'reel' ? 0.7 : e.st === 'snapW' ? e.tell : e.st === 'snap' || e.st === 'bite' ? 0 : 0.35 + 0.1 * Math.sin(G.t * 2);
    const sway = Math.sin(G.t * 1.8 + e.id) * 3;
    for (const [x, r, a] of [[-16, 16, -0.5], [14, 15, 0.5], [0, 12, 0]]) { ctx.save(); ctx.translate(x, e.ry - 4); ctx.rotate(a); E_ell(ctx, P, 0, -6, r * 0.5, r, WOOD.leaf); ctx.restore(); }
    E_line(ctx, P, [[0, e.ry - 6], [sway * 0.5, 4], [sway, -10]], 8, '#3c7a46');
    // liana (en coordenadas del cuerpo: el dibujo está girado según face)
    if (e.vlen > 2) {
      const m = flytrapMouth(e), pts = [];
      for (let i = 0; i <= 10; i++) { const s = i / 10, wx = m.x + Math.cos(e.vA) * e.vlen * s, wy = m.y + Math.sin(e.vA) * e.vlen * s + Math.sin(s * 9 + G.t * 12) * 4 * (1 - s); pts.push([(wx - e.x) * e.face, wy - e.y]); }
      E_line(ctx, P, pts, 3, WOOD.mossL);
    }
    // boca: dos lóbulos con dientes de cristal
    ctx.save(); ctx.translate(sway + 8, -16);
    for (const s of [-1, 1]) {
      ctx.save(); ctx.rotate(s * open * 0.7);
      E_ell(ctx, P, 10, s * 6, 18, 9, '#3f8a46'); E_ell(ctx, P, 12, s * 3, 14, 4, '#c0304a');
      for (let k = 0; k < 4; k++) E_poly(ctx, P, [[2 + k * 6, s * 2], [5 + k * 6, s * -4], [8 + k * 6, s * 2]], WOOD.crystal);
      ctx.restore();
    }
    E_eye(ctx, P, e, 2, -12, 2.4);
    ctx.restore();
  },
});
function flytrapMouth(e) { return { x: e.x + e.face * 22, y: e.y - 16 }; }
