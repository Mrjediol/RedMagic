// e-tejedora.js — Tejedora: voladora a distancia que apunta con un hilo de luz y se teletransporta. Pregunta: ¿te mueves
// cuando el hilo se fija (no antes) y la persigues cuando huye?
'use strict';
const WEAVER_PAL = { core: '#ffffff', light: '#ffc8f0', mid: '#ff6ad5', deep: '#b0208a', g: [255, 100, 210] };
addFoe({
  id: 'weaver', name: 'Tejedora', short: 'Tejed.', rx: 18, ry: 22, fly: true, home: 'hover',
  role: 'Voladora a distancia que se teletransporta',
  question: 'Apartarte justo cuando el hilo de puntería se queda fijo (blanco), no antes; perseguirla o derribarla con armas a distancia porque huye y reaparece.',
  moves: [
    { name: 'Aguja', desc: 'Traza un hilo fino hacia ti que te sigue; un instante antes de disparar se queda fijo y se pone blanco. Luego, aguja muy rápida por esa línea.' },
    { name: 'Telaraña', desc: 'Lanza 3 nudos de tela lentos en abanico. No hacen daño: si te tocan te ENREDAN (vas a menos de la mitad de velocidad un rato). Se pueden devolver con Égida, Espejo, Ráfaga…' },
    { name: 'Parpadeo', desc: 'Tras unos ataques, o si le haces mucho daño seguido, desaparece y reaparece en otro sitio (al otro lado de ti). Antes de aparecer marca el sitio.' },
  ],
  combos: 'Con Acechadores (el enredo deja que el salto acierte). Con Excavador: la aguja te obliga a moverte mientras buscas dónde va a salir.',
  art: 'Cuerpo pequeño de hada o polilla con cuatro alas translúcidas y abdomen brillante; cuelga un hilo. Polilla de cristal, araña voladora.',
  tuning: Object.assign(baseTuning(80, 20, 1.4, 0), {
    distMin: [230, 50, 600, 10, 'Distancia mínima (huye)'], distMax: [380, 100, 800, 10, 'Distancia máxima'],
    height: [140, 0, 280, 5, 'Altura sobre ti'], speed: [160, 30, 400, 5, 'Velocidad'],
    aimTime: [0.8, 0.2, 2, 0.05, 'Aguja: apuntar (s)'], lockTime: [0.25, 0, 1, 0.05, 'Aguja: hilo fijo antes de disparar (s)'],
    needleSpeed: [1050, 300, 2000, 10, 'Aguja: velocidad'], needleDmg: [14, 0, 60, 1, 'Aguja: daño'],
    webChance: [0.35, 0, 1, 0.05, 'Probabilidad de telaraña'], webN: [3, 1, 7, 1, 'Telaraña: nudos'], webSpeed: [150, 40, 400, 5, 'Telaraña: velocidad'],
    slowTime: [1.6, 0, 5, 0.1, 'Enredado: duración (s)'], slowK: [0.45, 0.1, 1, 0.05, 'Enredado: × velocidad'],
    blinkAfter: [3, 1, 10, 1, 'Parpadeo: cada N ataques'], blinkHurt: [28, 0, 200, 1, 'Parpadeo: si recibe este daño en 1,5 s'],
    blinkCd: [4, 0, 15, 0.5, 'Parpadeo: pausa mínima (s)'], blinkMark: [0.5, 0.1, 2, 0.05, 'Parpadeo: aviso antes de aparecer (s)'],
    cooldown: [1.5, 0.2, 5, 0.1, 'Pausa entre ataques (s)'],
  }),
  init(e) { e.attacks = 0; e.blinkCdT = 1.5; e.side = 1; e.aimA = Math.PI; e.fade = 0; },
  think(e, dt) {
    const t = FT(e), p = G.player, c = pCenter();
    e.blinkCdT -= dt;
    switch (e.st) {
      case 'hover': {
        e.cdT -= dt; faceTo(e, p.x);
        if (Math.abs(e.x - p.x) < t.distMin * 0.7) e.side = Math.sign(e.x - p.x) || e.side;
        const mid = (t.distMin + t.distMax) / 2;
        let tx = p.x + e.side * mid; if (tx < ARENA.x0 + 50 || tx > ARENA.x1 - 50) { e.side = -e.side; tx = p.x + e.side * mid; }
        const ty = clamp(c.y - t.height + Math.sin(G.t * 1.7 + e.id) * 25, 50, GROUND - 120);
        steer(e, tx, ty, Math.abs(e.x - p.x) < t.distMin ? t.speed * 1.7 : t.speed, dt);
        if (e.attacks >= t.blinkAfter && e.blinkCdT <= 0) { weaverBlink(e); break; }
        if (e.cdT > 0 || !canStart(e)) break;
        if (Math.random() < t.webChance) { begin(e, 'Telaraña'); go(e, 'webW'); break; }
        begin(e, 'Aguja'); go(e, 'aim'); { const a = aimPoint(e); e.aimA = Math.atan2(a.y - e.y, a.x - e.x); }
        e.line = warn({ e, kind: 'line', t: t.aimTime + 0.05, follow: w => {
          if (!w.locked) { const a = aimPoint(e); e.aimA = Math.atan2(a.y - e.y, a.x - e.x); }
          w.ax = e.x; w.ay = e.y; w.bx = e.x + Math.cos(e.aimA) * 1500; w.by = e.y + Math.sin(e.aimA) * 1500; } });
        break;
      }
      case 'aim':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 4)); e.my = lerp(e.my, 0, Math.min(1, dt * 4)); faceTo(e, e.x + Math.cos(e.aimA));
        if (e.line && e.stT > t.aimTime - t.lockTime) e.line.locked = true;
        if (!windup(e, t.aimTime)) break;
        go(e, 'shot'); release(e); e.attacks++;
        foeShot({ e, atk: 'Aguja', x: e.x, y: e.y, vx: Math.cos(e.aimA) * t.needleSpeed, vy: Math.sin(e.aimA) * t.needleSpeed, r: 4, style: 'needle', dmg: t.needleDmg, pal: WEAVER_PAL, life: 2 });
        sfx(k => SFXLIB.whoosh(k, 4200, 1800, 0.12, 0.14));
        break;
      case 'webW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 4)); e.my = lerp(e.my, 0, Math.min(1, dt * 4)); faceTo(e, p.x);
        if (!windup(e, 0.5)) break;
        go(e, 'shot'); release(e); e.attacks++;
        { const a = aimPoint(e), a0 = Math.atan2(a.y - e.y, a.x - e.x);
          for (let i = 0; i < t.webN; i++) {
            const an = a0 + (i - (t.webN - 1) / 2) * 0.3;
            foeShot({ e, atk: 'Telaraña', x: e.x, y: e.y, vx: Math.cos(an) * t.webSpeed, vy: Math.sin(an) * t.webSpeed, r: 9, style: 'web', dmg: 0, life: 4.5,
              onHitP: () => { if (G.es.harmless) return; slowPlayer(t.slowTime, t.slowK); text(G.player.x, G.player.y - 96, 'ENREDADO', '#e8d8ff', true); const F = G.fight; if (F) { const k = 'Tejedora · Telaraña (enredo)'; (F.hits[k] = F.hits[k] || { n: 0, dmg: 0 }).n++; } } });
          } }
        break;
      case 'shot':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.35) { go(e, 'hover'); e.cdT = t.cooldown * (0.8 + Math.random() * 0.4); }
        break;
      case 'blinkOut':
        e.mx = 0; e.my = 0; e.fade = clamp(e.stT / 0.25);
        if (e.stT < 0.25) break;
        { const mid = (t.distMin + t.distMax) / 2; let side = -Math.sign(e.x - p.x) || 1, x = p.x + side * mid;
          if (x < ARENA.x0 + 50 || x > ARENA.x1 - 50) { side = -side; x = p.x + side * mid; }
          const y = clamp(c.y - t.height, 50, GROUND - 120);
          e.side = side; e.dest = { x, y }; hide(e); go(e, 'blinkIn');
          warn({ kind: 'mark', x, y, r: e.rx * 1.3, t: t.blinkMark }); }
        break;
      case 'blinkIn':
        if (e.stT < t.blinkMark) break;
        unhide(e, e.dest.x, e.dest.y); e.fade = 0; go(e, 'hover'); e.attacks = 0; e.blinkCdT = t.blinkCd; e.cdT = Math.max(e.cdT, 0.6);
        { const x = e.x, y = e.y; addFx(0.6, (ctx, layer, t0) => sparks(ctx, G.t, t0, x, y, layer, { n: 10, pal: WEAVER_PAL, sp: 200, seed: 5, len: 10, life: 0.4 })); }
        break;
    }
  },
  onHurt(e) {
    const t = FT(e);
    if (e.blinkCdT <= 0 && e.st !== 'blinkOut' && !e.hidden && recentDmg(e, 1.5) > t.blinkHurt) { say(e, '¡HUYE!', '#ffc8f0', true); weaverBlink(e); }
  },
  draw(ctx, e, P) {
    if (e.fade > 0) ctx.globalAlpha *= 1 - e.fade;
    const fl = Math.sin(G.t * 18 + e.id) * 0.5, wing = P.mono ? 1 : 0.55;
    ctx.save(); ctx.globalAlpha *= wing;
    for (const [s, ax, len, w] of [[-1, -4, 26, 11], [1, -4, 26, 11], [-1, 6, 18, 8], [1, 6, 18, 8]]) {
      ctx.save(); ctx.translate(-2, ax); ctx.rotate((s < 0 ? -2.3 : -0.8) + fl * s * 0.6);
      E_ell(ctx, P, len * 0.5, 0, len * 0.5, w, '#d8c8ff'); ctx.restore();
    }
    ctx.restore();
    E_line(ctx, P, [[-2, 18], [-4, 30 + Math.sin(G.t * 3) * 3]], 1, '#efe8ff');
    E_ell(ctx, P, -2, 8, 7, 12, '#5a3c8a');
    if (!P.mono) glow(ctx, -2, 16, 12, WEAVER_PAL.g, 0.4 + 0.4 * e.tell);
    E_ell(ctx, P, -2, 16, 4, 5, WEAVER_PAL.mid);
    E_ell(ctx, P, 0, -8, 6, 8, '#7a5ab0');
    E_ell(ctx, P, 3, -18, 7, 6, '#7a5ab0');
    E_line(ctx, P, [[4, -23], [10, -32], [14, -31]], 1.2, '#cdb8f0'); E_line(ctx, P, [[1, -23], [2, -33], [6, -35]], 1.2, '#cdb8f0');
    E_eye(ctx, P, e, 6, -18, 2.4);
  },
});
function weaverBlink(e) { e.atkId++; e.busy = false; go(e, 'blinkOut'); sfx(k => SFXLIB.whoosh(k, 3000, 6000, 0.18, 0.1)); }
