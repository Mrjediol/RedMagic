// g-espejo.js — 13 · Espejo, versión jugable. Ficha y decisiones: docs/armas/espejo.md · progresión: docs/armas/progresion.md
// Clic = un panel de cristal flotando donde apuntas (mirando hacia ti). TUS proyectiles que lo cruzan (de cualquier arma) salen
// DESVIADOS HACIA EL ENEMIGO MÁS CERCANO y con más daño; el Rayo rebota en él igual; las balas enemigas vuelven como tuyas.
// Además destella solo de vez en cuando. Es el arma que más depende de la otra. Árbol: base → Facetas / Lupa / Portal → 3 mejoras.
'use strict';
const SND_MIRROR = {
  place: sound('mirror_place', 'Espejo', 'Aparece el espejo', k => { const t = nowT(); vRing(t, [1568, 2349, 3136], 0.5 * k, 0.05, 0.4); vNoise(t, 0.12 * k, 0.12, 'highpass', 4000, 9000, 1, { rising: true }); }),
  bounce: sound('mirror_bounce', 'Espejo', 'Desvía un proyectil', k => { const t = nowT(); vTone(t, 2800, 4200, 0.06 * k, 0.04, 'triangle', 0.25); }),
  glint: sound('mirror_glint', 'Espejo', 'Destello', k => { const t = nowT(); vRing(t, [3520, 5274], 0.18 * k, 0.025, 0.3); }),
  portal: sound('mirror_portal', 'Espejo', 'Portal · atraviesa', k => { const t = nowT(); vNoise(t, 0.18 * k, 0.15, 'bandpass', 400, 2400, 2, { rising: true, att: 0.005 }); vTone(t, 220, 660, 0.15 * k, 0.04, 'sine', 0.3); }),
  shatter: sound('mirror_shatter', 'Espejo', 'Se rompe en esquirlas', k => SFXLIB.shatter(k, 0.9)),
};
addWeapon({
  id: 'mirror', name: 'Espejo', short: 'Espejo', pal: 'ice',  fresh: true,   // arma nueva (2026-10-20), sin probar por el usuario
  archetypes: [
    { id: 'facet', name: 'Facetas', desc: 'Cada proyectil que desvía se parte en dos; el Rayo que lo toca suelta además un haz al segundo enemigo.',
      ups: [{ id: 'three', name: 'Tres caras', desc: 'Se parte en tres.' },
            { id: 'shine', name: 'Brillo', desc: 'Destella mucho más a menudo y también cada vez que desvía algo.' },
            { id: 'shatter', name: 'Añicos', desc: 'Al acabarse, el espejo revienta en esquirlas que van a por los enemigos.' }] },
    { id: 'lens', name: 'Lupa', desc: 'Ya no desvía: CONCENTRA. Lo que la atraviesa sale con mucho más daño (el Rayo, más grueso y fuerte).',
      ups: [{ id: 'focus', name: 'Foco', desc: 'Concentra la luz en el suelo de debajo: quema a quien pise el punto.' },
            { id: 'wide', name: 'Gran angular', desc: 'Lupa mucho más grande.' },
            { id: 'converge', name: 'Convergencia', desc: 'Además de concentrar, desvía lo que pasa hacia el enemigo más cercano.' }] },
    { id: 'portal', name: 'Portal', desc: 'Dos espejos enlazados: lo que entra por uno sale por el otro — tus proyectiles, el Rayo y los ENEMIGOS que pasan por él.',
      ups: [{ id: 'loop', name: 'Bucle', desc: 'Cada vez que un proyectil tuyo atraviesa, gana daño (hasta 3 veces).' },
            { id: 'trap', name: 'Trampa', desc: 'Los enemigos que atraviesan salen aturdidos y vulnerables.' },
            { id: 'walk', name: 'Atajo', desc: 'Tú también puedes atravesarlos.' }] },
  ],
  tuning: {
    cooldown: [1.2, 0.2, 6, 0.05, 'Base: recarga (s)'], range: [380, 80, 800, 10, 'Base: distancia máxima'], half: [44, 10, 120, 1, 'Base: medio largo del espejo'],
    life: [7, 1, 20, 0.5, 'Base: duración (s)'], maxMirrors: [2, 1, 5, 1, 'Base: espejos a la vez'], mirrorMul: [1.25, 1, 3, 0.05, 'Base: × daño de lo desviado'],
    redirectRange: [600, 100, 1200, 10, 'Base: alcance para buscar enemigo al desviar'], reflectDamage: [14, 1, 60, 1, 'Base: daño de una bala enemiga devuelta'],
    glintEvery: [1.4, 0.2, 5, 0.05, 'Base: destello propio cada (s)'], glintDamage: [10, 0, 60, 1, 'Base: daño del destello'], glintRange: [300, 50, 600, 10, 'Base: alcance del destello'],
    facetSpread: [0.22, 0.05, 0.8, 0.01, 'Facetas: abertura de las copias (rad)'], sideBeamDps: [30, 0, 150, 1, 'Facetas: daño/s del haz extra del Rayo'],
    shineEvery: [0.5, 0.1, 2, 0.05, 'Facetas · Brillo: destello cada (s)'], shatterShards: [6, 1, 16, 1, 'Facetas · Añicos: esquirlas'], shatterDamage: [15, 1, 80, 1, 'Facetas · Añicos: daño'],
    lensMul: [1.8, 1, 4, 0.05, 'Lupa: × daño de lo que la atraviesa'], wideMul: [1.8, 1, 3, 0.05, 'Lupa · Gran angular: × tamaño'], focusDps: [16, 0, 80, 1, 'Lupa · Foco: daño/s en el punto'],
    loopMul: [1.3, 1, 3, 0.05, 'Portal · Bucle: × daño por pasada'], loopMax: [3, 1, 6, 1, 'Portal · Bucle: pasadas que suman'], trapStun: [1, 0, 3, 0.1, 'Portal · Trampa: aturdido (s)'],
    trapVuln: [1.3, 1, 3, 0.05, 'Portal · Trampa: × daño que reciben'], portalCd: [0.6, 0.1, 3, 0.05, 'Portal: s antes de que el mismo enemigo pueda volver a pasar'],
  },
  create(slot) {
    const id = 'mirror';
    let cd = 0, mirrors = [];
    const inst = {
      mirrors: () => mirrors,
      onPress(aim) {
        if (cd > 0) return;
        const t = T(id); cd = t.cooldown * cdMul(); G.player.castT = 0.25;
        const p = G.player, d = Math.min(t.range, Math.hypot(aim.tx - aim.x, aim.ty - aim.y)), cx = aim.x + aim.dx * d, cy = Math.min(GROUND - 30, aim.y + aim.dy * d);
        const portal = slot.arch === 'portal';
        const m = mirrorEnt(slot, inst, cx, cy, portal ? Math.PI / 2 : Math.atan2(cy - (p.y - 40), cx - p.x) + Math.PI / 2);
        mirrors.push(m); G.ents.push(m); G.mirrors = (G.mirrors || []).filter(o => o.alive && G.ents.includes(o)); G.mirrors.push(m);
        const max = portal ? 2 : t.maxMirrors;
        while (mirrors.filter(o => o.alive).length > max) mirrors.find(o => o.alive).expire();
        if (portal) { const live = mirrors.filter(o => o.alive); if (live.length === 2) { live[0].link = live[1]; live[1].link = live[0]; live[0].label = 'A'; live[1].label = 'B'; } else { m.link = null; m.label = 'A'; } }
        sfx(SND_MIRROR.place.fn);
      },
      update(dt) { cd -= dt; mirrors = mirrors.filter(m => m.alive); },
      ready() { return 1 - cd / Math.max(0.01, T(id).cooldown * cdMul()); },
      refund(s) { cd -= s; },
    };
    return inst;
  },
});

function mirrorEnt(slot, owner, cx, cy, ang) {
  const t = T('mirror'), el = elemOf(), arch = slot.arch, mode = arch === 'facet' ? 'facet' : arch === 'lens' ? 'lens' : arch === 'portal' ? 'portal' : 'mirror';
  const half = t.half * (mode === 'lens' && hasMod(slot, 'wide') ? t.wideMul : 1) * (copies() > 1 && mode !== 'portal' ? 1.4 : 1);
  const pal = elemPal(mode === 'portal' ? 'void' : mode === 'lens' ? 'gold' : 'ice'), t0 = G.t, prev = new Map();
  let glintT = 0.6, sideT = 0, focusT = 0, ended = false;
  const toward = (x, y, fallback) => { const n = nearestEnemy(x, y, t.redirectRange); return n ? Math.atan2(n.y - y, n.x - x) : fallback; };
  const reflect = a => { const nx = Math.cos(ang + Math.PI / 2), ny = Math.sin(ang + Math.PI / 2), vx = Math.cos(a), vy = Math.sin(a), d = vx * nx + vy * ny; return Math.atan2(vy - 2 * d * ny, vx - 2 * d * nx); };
  const glint = (n = copies()) => {
    const seen = new Set();
    for (let i = 0; i < n; i++) {
      const e = nearestEnemy(cx, cy, t.glintRange, seen); if (!e) break; seen.add(e);
      damage(e, { dmg: t.glintDamage, slot, silentFlash: true, color: '#e4f9ff', elem: el, freeze: el === 'ice' ? 0.15 : 0, burn: el === 'fire' ? 1 : 0 });
      const x1 = e.x, y1 = e.y; addFx(0.25, (ctx, layer, f0) => glintRay(ctx, G.t, layer, f0, cx, cy, x1, y1, pal));
    }
    if (seen.size && (!G._glSfx || G.t - G._glSfx > 0.1)) { G._glSfx = G.t; sfx(SND_MIRROR.glint.fn); }
  };
  const m = {
    kind: 'mirror', alive: true, cx, cy, link: null, label: '', litT: -9,
    x0: cx - Math.cos(ang) * half, y0: cy - Math.sin(ang) * half, x1: cx + Math.cos(ang) * half, y1: cy + Math.sin(ang) * half,
    expire() {
      if (ended) return; ended = true; this.alive = false;
      if (this.link) { this.link.link = null; }
      if (mode === 'facet' && hasMod(slot, 'shatter')) {
        for (let i = 0; i < t.shatterShards; i++) G.ents.push(mirrorShard(slot, cx, cy, toward(cx, cy, i / t.shatterShards * TAU) + (i - t.shatterShards / 2) * 0.12));
        sfx(SND_MIRROR.shatter.fn);
      }
      const P = pal; addFx(0.6, (ctx, layer, f0) => { const u = seg(G.t, f0, f0 + 0.5); for (let i = 0; i < 7; i++) { const a = i / 7 * TAU, dd = 40 * easeOut(u); chunk(ctx, cx + Math.cos(a) * dd, cy + Math.sin(a) * dd + 90 * u * u, 4, u * 7 + i, i + 2, P.mid, P.core, 1 - u, layer); } });
    },
    // haz del Rayo que lo toca → { x, y, ang, mul, jump, from }
    beam(x, y, a) {
      this.litT = G.t;
      if (mode === 'portal') {
        if (!this.link || !this.link.alive) return { x, y, ang: reflect(a) };
        const L = this.link, s = Math.hypot(x - this.x0, y - this.y0) / (half * 2);
        return { x: lerp(L.x0, L.x1, s) + Math.cos(a) * 3, y: lerp(L.y0, L.y1, s) + Math.sin(a) * 3, ang: a, jump: true, from: L };
      }
      if (mode === 'lens') return { x, y, ang: hasMod(slot, 'converge') ? toward(x, y, a) : a, mul: t.lensMul };
      return { x, y, ang: toward(x, y, reflect(a)), mul: t.mirrorMul };
    },
    pass(q, x, y) {                                             // un proyectil lo cruza en (x, y)
      const sp = Math.max(400, Math.hypot(q.vx, q.vy)), a = Math.atan2(q.vy, q.vx);
      q.mirrorT = G.t;
      if (q.kind === 'enemyShot' && q.hostile && !q.friendly) {   // bala enemiga: vuelve como tuya
        const na = toward(x, y, reflect(a)); q.vx = Math.cos(na) * sp * 1.5; q.vy = Math.sin(na) * sp * 1.5; q.hostile = false; q.friendly = true; q.slot = slot; q.dmg = t.reflectDamage; q.life = 3; q.x = x; q.y = y;
        sfx(SND_MIRROR.bounce.fn); return;
      }
      if (mode === 'portal') {
        if (!this.link || !this.link.alive) return;
        const L = this.link, s = clamp(Math.hypot(x - this.x0, y - this.y0) / (half * 2));
        q.x = lerp(L.x0, L.x1, s) + Math.cos(a) * 6; q.y = lerp(L.y0, L.y1, s) + Math.sin(a) * 6; q.mirrorT = G.t;
        if (hasMod(slot, 'loop') && (q.loops || 0) < t.loopMax) { q.loops = (q.loops || 0) + 1; q.dmgMul = (q.dmgMul || 1) * t.loopMul; }
        if (q.age !== undefined) q.age = Math.max(0, q.age - 0.25);
        if (q.trail) q.trail.length = 0;
        prev.delete(q); sfx(SND_MIRROR.portal.fn); return;
      }
      let na = a;
      if (mode === 'lens') { q.dmgMul = (q.dmgMul || 1) * t.lensMul; if (hasMod(slot, 'converge')) na = toward(x, y, a); }
      else { q.dmgMul = (q.dmgMul || 1) * t.mirrorMul; na = toward(x, y, reflect(a)); }
      q.vx = Math.cos(na) * sp; q.vy = Math.sin(na) * sp; q.x = x + Math.cos(na) * 4; q.y = y + Math.sin(na) * 4; if (q.age !== undefined) q.age = Math.max(0, q.age - 0.3); if (q.ang !== undefined) q.ang = na; if (q.hit && q.hit.clear && mode !== 'lens') q.hit.clear();
      if (mode === 'facet' && q.clone) {                        // Facetas: se parte
        const extra = hasMod(slot, 'three') ? 2 : 1;
        for (let i = 0; i < extra; i++) { const c = q.clone(), ca = na + (i === 0 ? 1 : -1) * t.facetSpread; c.vx = Math.cos(ca) * sp; c.vy = Math.sin(ca) * sp; c.x = q.x; c.y = q.y; c.mirrorT = G.t; c.dmgMul = q.dmgMul; if (c.ang !== undefined) c.ang = ca; G.ents.push(c); }
      }
      if (mode === 'facet' && hasMod(slot, 'shine')) glint(1);
      const P = pal; addFx(0.3, (ctx, layer, f0) => { if (layer === 'front') { const u = seg(G.t, f0, f0 + 0.25); star4(ctx, x, y, 16 * (1 - u), ang, P.core, 1 - u, layer); glow(ctx, x, y, 24, P.g2, 0.6 * (1 - u)); } });
      if (!G._mbSfx || G.t - G._mbSfx > 0.05) { G._mbSfx = G.t; sfx(SND_MIRROR.bounce.fn); }
    },
    update(dt) {
      if (!this.alive) return false;
      const age = G.t - t0;
      if (age > t.life) { this.expire(); return false; }
      // proyectiles que lo cruzan entre el frame anterior y este
      for (const q of G.ents) {
        if (typeof q.vx !== 'number' || q.captured || (q.state !== 'fly' && q.kind !== 'enemyShot') || q === this) continue;
        const pv = prev.get(q); prev.set(q, { x: q.x, y: q.y });
        if (!pv || G.t - (q.mirrorT || -9) < 0.1) continue;
        const u = segSeg(pv.x, pv.y, q.x, q.y, this.x0, this.y0, this.x1, this.y1);
        if (u !== null) this.pass(q, lerp(pv.x, q.x, u), lerp(pv.y, q.y, u));
      }
      if (prev.size > 300) for (const k of prev.keys()) if (!G.ents.includes(k)) prev.delete(k);
      // destello propio (no el portal)
      if (mode !== 'portal') { glintT -= dt; if (glintT <= 0) { glintT = mode === 'facet' && hasMod(slot, 'shine') ? t.shineEvery : t.glintEvery; glint(); } }
      // Facetas: con el Rayo encima, haz extra al segundo enemigo
      if (mode === 'facet' && G.t - this.litT < 0.08) {
        sideT -= dt;
        const first = nearestEnemy(cx, cy, t.redirectRange), second = first ? nearestEnemy(cx, cy, t.redirectRange, new Set([first])) : null;
        this.side = second;
        if (second && sideT <= 0) { sideT = 0.1; damage(second, { dmg: t.sideBeamDps * 0.1, slot, silentFlash: true, elem: el, freeze: el === 'ice' ? 0.05 : 0, burn: el === 'fire' ? 0.6 : 0 }); }
      } else this.side = null;
      // Lupa · Foco: quema el punto de suelo de debajo
      if (mode === 'lens' && hasMod(slot, 'focus')) { focusT -= dt; if (focusT <= 0) { focusT = 0.2; for (const e of alive()) if (Math.abs(e.x - cx) < e.rx + 18 && GROUND - (e.y + e.ry) < 30) damage(e, { dmg: t.focusDps * 0.2, slot, silentFlash: true, color: '#ffe9a0', elem: el, burn: el === 'fire' ? 1.5 : 0, freeze: el === 'ice' ? 0.06 : 0 }); } }
      // Portal: enemigos (y tú con Atajo) que lo atraviesan
      if (mode === 'portal' && this.link && this.link.alive) {
        const L = this.link;
        for (const e of alive()) {
          if (G.t - (e.portalT || -9) < t.portalCd || e.stasis) continue;
          if (Math.abs(e.x - cx) < e.rx * 0.6 && Math.abs(e.y - cy) < half + e.ry * 0.3) {
            const dir = Math.sign(e.vx || e.dir || (e.x - G.player.x)) || 1;
            e.portalT = G.t; e.x = L.cx + dir * (e.rx + 14); if (e.fly) { e.y = L.cy; } else { e.y = Math.min(e.y, L.cy); e.vy = 0; }
            if (e.fly) { e.homeY = clamp(L.cy, 80, GROUND - 80); }
            if (hasMod(slot, 'trap')) { e.stunT = Math.max(e.stunT, t.trapStun); makeVuln(e, t.trapVuln, t.trapStun + 0.6); }
            const ox = L.cx, oy = L.cy, P = pal; addFx(0.5, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, ox, oy, 0.6, P, { spread: TAU, seed: 4 }));
            sfx(SND_MIRROR.portal.fn);
          }
        }
        const p = G.player;
        if (hasMod(slot, 'walk') && G.t - (p.portalT || -9) > t.portalCd && Math.abs(p.x - cx) < 14 && Math.abs(p.y - 40 - cy) < half + 30) {
          p.portalT = G.t; const dir = Math.sign(p.vx) || p.face; p.x = clamp(L.cx + dir * 22, 20, WW - 20); p.y = Math.min(GROUND, L.cy + 40); p.vy = Math.min(p.vy, 0);
          sfx(SND_MIRROR.portal.fn);
        }
      }
      return true;
    },
    draw(ctx, layer) {
      const age = G.t - t0, a = easeOut(seg(age, 0, 0.15)) * (1 - seg(age, t.life - 0.4, t.life));
      mirrorPane(ctx, G.t, layer, this.x0, this.y0, this.x1, this.y1, pal, a, { mode, lit: G.t - this.litT < 0.1, label: this.label + (this.link ? '' : mode === 'portal' ? ' (pon otro)' : '') });
      if (this.side && layer === 'front') energyBeam(ctx, G.t, layer, cx, cy, this.side.x, this.side.y, 0.7 * a, pal, { sc: 0.4, seed: 7 });
      if (mode === 'lens' && hasMod(slot, 'focus')) {                       // cono de luz al suelo y punto ardiente
        if (layer === 'back') { ctx.save(); ctx.globalAlpha = clamp(0.18 * a); ctx.fillStyle = col(pal.light, layer); ctx.beginPath(); ctx.moveTo(this.x0, this.y0); ctx.lineTo(this.x1, this.y1); ctx.lineTo(cx + 6, GROUND); ctx.lineTo(cx - 6, GROUND); ctx.closePath(); ctx.fill(); ctx.restore(); }
        else { glow(ctx, cx, GROUND - 4, 26, pal.g1, (0.6 + 0.2 * Math.sin(G.t * 20)) * a); star4(ctx, cx, GROUND - 4, 12, G.t * 3, pal.core, a, layer); }
      }
    },
  };
  return m;
}

// esquirla de espejo (Facetas · Añicos): proyectil capturable que busca
function mirrorShard(slot, x, y, ang) {
  const t = T('mirror'), el = elemOf(), pal = elemPal('ice'), trail = [];
  return {
    kind: 'shard', capturable: true, state: 'fly', slot, x, y, vx: Math.cos(ang) * 700, vy: Math.sin(ang) * 700, age: 0, dmgMul: 1, hit: new Set(),
    clone() { const c = mirrorShard(slot, this.x, this.y, Math.atan2(this.vy, this.vx)); c.dmgMul = this.dmgMul; return c; },
    update(dt) {
      this.age += dt; if (this.captured) return true; if (this.age > 0.9 + (this.ejected ? 0.6 : 0)) return false;
      const nx = this.x + this.vx * dt, ny = this.y + this.vy * dt, h = raycastEnemy(this.x, this.y, nx, ny, this.hit);
      if (h) { damage(h.e, { dmg: t.shatterDamage * this.dmgMul, slot, kx: Math.sign(this.vx) * 80, elem: el, freeze: el === 'ice' ? 0.25 : 0, burn: el === 'fire' ? 1.2 : 0 }); const hx = h.x, hy = h.y; addFx(0.6, (ctx, layer, f0) => impact(ctx, G.t, layer, f0, hx, hy, 0.4, pal, { spread: 2, seed: Math.floor(hx) })); return false; }
      if (ny >= GROUND - 2) return false;
      this.x = nx; this.y = ny; trail.unshift({ x: nx, y: ny }); if (trail.length > 6) trail.pop();
      return true;
    },
    draw(ctx, layer) { if (layer === 'back' && trail.length > 1) ribbon(ctx, trail, s => 4 * (1 - s), pal.mid, 0.7, layer); crystalShard(ctx, this.x, this.y, Math.atan2(this.vy, this.vx), 0.6, pal, 1, layer); },
  };
}
