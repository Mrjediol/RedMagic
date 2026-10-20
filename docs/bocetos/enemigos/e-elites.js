// e-elites.js — ÉLITES: versiones de un común con un set de movimientos más rico (3-4), más vida y aguante; 1 por encuentro.
// Reutilizan la IA, el dibujo y los ajustes del común (eliteTuning / drawAs / onEnter) y añaden giros: el jugador ya sabe
// leerlos y sólo aprende lo nuevo. Ocupan G.es.eliteSlots turnos de ataque (dejan menos sitio a los demás).
'use strict';

// ---------------------------------------------------------------- Embestidor alfa
addFoe(Object.assign({}, FOES.charger, {
  id: 'chargerAlpha', name: 'Embestidor alfa', short: 'Emb. α', tier: 'élite', rx: 54, ry: 37,
  role: 'Élite del Embestidor: cargas encadenadas y furia',
  question: 'Lo mismo que el Embestidor, pero sin respiro: tras una carga que esquivas viene otra enseguida. Ponerte delante de la pared corta la cadena (el choque sigue siendo tu premio).',
  moves: [
    { name: 'Embestida encadenada', desc: 'Como la del común, pero al derrapar se gira y vuelve a cargar con una preparación corta (flechas otra vez). Si choca con la pared, la cadena se acaba y queda aturdido.' },
    { name: 'Pisotón con ondas', desc: 'El pisotón del común y además suelta dos ondas por el suelo a los lados: saltarlas.' },
    { name: 'Furia', desc: 'A media vida ruge: todo un 25 % más rápido (preparaciones, cargas y pausas) hasta que muere.' },
  ],
  combos: 'Solo ya es un encuentro. Con un Artillero detrás: carga + morteros. Con Fatuos: atropella fatuos y le revientan encima.',
  art: 'El Embestidor más grande, con una cresta de cristal en el lomo y cicatrices; que se reconozca como el mismo bicho.',
  tuning: eliteTuning('charger', { hp: 600, poise: 140, kb: 0.15, chargeSpeed: 700, cooldown: 1.1, chargeDmg: 20 }, {
    chain: [1, 0, 3, 1, 'Embestida: cargas extra encadenadas'], windup2: [0.4, 0.1, 1.5, 0.05, 'Carga encadenada: preparación (s)'],
    waveSpeed: [360, 80, 800, 10, 'Ondas del pisotón: velocidad'], waveDist: [480, 50, 1200, 10, 'Ondas del pisotón: recorrido'],
    waveH: [30, 10, 90, 1, 'Ondas del pisotón: altura'], waveDmg: [10, 0, 60, 1, 'Ondas del pisotón: daño'],
    rageAt: [0.5, 0, 1, 0.05, 'Furia: a esta fracción de vida'], rageK: [1.25, 1, 2, 0.05, 'Furia: × rapidez'],
  }),
  think(e, dt) {
    const t = FT(e);
    if (!e.rage && e.hp < e.hpMax * t.rageAt) { e.rage = t.rageK; say(e, '¡FURIA!', '#ff9a6a', true); G.shake += 4; sfx(k => { const t0 = nowT(); vSaw(t0, 120, 70, 0.6 * k, 0.12, 900, 300, 2); }); }
    if (e.st === 'rechargeW') {
      e.busy = true; e.mx = lerp(e.mx, 0, Math.min(1, dt * 8));
      if (e.stT < t.windup2 * 0.6) { e.cdir = Math.sign(dxP(e)) || e.cdir; e.face = e.cdir; }
      if (Math.random() < dt * 6) dustPuff(e.x - e.cdir * e.rx * 0.8, GROUND);
      if (windup(e, t.windup2 / (e.rage || 1))) { go(e, 'charge'); release(e); e.run = 0; e.hitP = false; e.bowled = new Set(); sfx(k => SFXLIB.whoosh(k, 900, 300, 0.4, 0.25)); }
      return;
    }
    FOES.charger.think(e, dt);
  },
  onEnter(e, st) {
    const t = FT(e);
    if (st === 'chargeW') e.combo = 0;
    if (st === 'skid' && (e.combo || 0) < t.chain) {
      e.combo = (e.combo || 0) + 1; begin(e, 'Embestida encadenada'); go(e, 'rechargeW'); e.cdir = Math.sign(dxP(e)) || -e.cdir;
      warn({ e, kind: 'arrow', x: e.x, dir: e.cdir, len: e.rx * 2.4, h: e.ry * 2, t: t.windup2 / (e.rage || 1), follow: w => { w.x = e.x + e.cdir * e.rx * 0.6; w.dir = e.cdir; } });
    }
    if (st === 'stomp') for (const d of [-1, 1]) groundWave(e, e.x + d * e.rx, d, { speed: t.waveSpeed, dist: t.waveDist, h: t.waveH, dmg: t.waveDmg, atk: 'Pisotón (ondas)' });
  },
  draw(ctx, e, P) {
    if (!P.mono && e.rage) glow(ctx, 0, -6, 70, [255, 70, 40], 0.3 + 0.1 * Math.sin(G.t * 10));
    drawAs(ctx, e, P, 'charger', e.ry / FOES.charger.ry);
    for (let i = 0; i < 4; i++) E_poly(ctx, P, [[-34 + i * 15, -24], [-28 + i * 15, -46 - (i % 2) * 10], [-20 + i * 15, -24]], i % 2 ? '#bff6ff' : '#7fe8ff');
    E_line(ctx, P, [[18, -14], [26, -4]], 2, '#2a2018');
  },
}));

// ---------------------------------------------------------------- Acechador alfa
addFoe(Object.assign({}, FOES.stalker, {
  id: 'stalkerAlpha', name: 'Acechador alfa', short: 'Ace. α', tier: 'élite', rx: 38, ry: 25,
  role: 'Élite del Acechador: jefe de manada',
  question: 'Prioridad y lectura: si no lo matas, aúlla y su manada va más rápido (y si está solo, la llama). De cerca no huye: zarpazo. Sus saltos vienen de dos en dos.',
  moves: [
    { name: 'Aullido', desc: 'Se para y aúlla (se puede interrumpir con daño): los acechadores cercanos (él incluido) van más rápido unos segundos (brillo rojo). Si quedan pocos, LLAMA a dos más, que entran por el borde de la arena.' },
    { name: 'Doble salto', desc: 'El abalanzarse del común, pero al caer se vuelve a agachar un instante y salta otra vez a donde estás.' },
    { name: 'Zarpazo', desc: 'Si te pegas a él no salta atrás como los demás: golpe rápido delante (preparación corta).' },
  ],
  combos: 'Con 2 Acechadores (su manada). Con una Tejedora: el enredo + la manada acelerada.',
  art: 'El Acechador más grande, con melena de cristal y una cicatriz; que se lea como el jefe de los mismos lobos.',
  tuning: eliteTuning('stalker', { hp: 300, poise: 80, kb: 0.6, leapDmg: 16, feint: 0.15, landRecover: 0.7, cooldown: 1.3, ringMin: 190, ringMax: 320, contact: 4 }, {
    chain: [1, 0, 3, 1, 'Saltos encadenados'], crouch2: [0.25, 0.05, 1, 0.05, 'Salto encadenado: preparación (s)'],
    howlEvery: [11, 2, 40, 0.5, 'Aullido: cada (s)'], howlWindup: [0.9, 0.2, 3, 0.05, 'Aullido: preparación (s)'],
    howlR: [700, 100, 1500, 10, 'Aullido: alcance'], howlTime: [5, 0, 15, 0.5, 'Aullido: duración del acelerón (s)'],
    callN: [2, 0, 5, 1, 'Llamada: si quedan menos de N acechadores, llama hasta N'], callCd: [16, 2, 60, 1, 'Llamada: pausa (s)'],
    swipeRange: [110, 30, 250, 5, 'Zarpazo: si estás a menos de'], swipeWindup: [0.3, 0.1, 1, 0.05, 'Zarpazo: preparación (s)'],
    swipeDmg: [14, 0, 60, 1, 'Zarpazo: daño'], swipeRec: [0.45, 0, 2, 0.05, 'Zarpazo: recuperación (s, castigo)'],
  }),
  init(e) { e.howlT = FT(e).howlEvery - 1.5; e.callAt = 0; e.combo = 0; },
  think(e, dt) {
    const t = FT(e), p = G.player, ad = Math.abs(dxP(e));
    e.howlT += dt;
    switch (e.st) {
      case 'prowl':
        if (e.howlT > t.howlEvery && e.onGround && canStart(e)) { begin(e, 'Aullido'); go(e, 'howlW'); return; }
        if (e.onGround && ad < t.swipeRange && e.cdT <= 0.5 && Math.abs(p.y - (e.y + e.foot)) < 60 && canStart(e)) { begin(e, 'Zarpazo'); go(e, 'swipeW'); return; }
        break;
      case 'howlW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); faceTo(e, p.x);
        if (windup(e, t.howlWindup)) alphaHowl(e, t);
        return;
      case 'howl':
        e.phase = 'recover'; e.busy = false;
        if (e.stT > 0.5) go(e, 'prowl');
        return;
      case 'swipeW':
        e.mx = lerp(e.mx, 0, Math.min(1, dt * 12)); faceTo(e, p.x);
        if (windup(e, t.swipeWindup)) { go(e, 'swipe'); release(e); e.mx = e.face * 140; e.hitP = false; sfx(k => SFXLIB.whoosh(k, 2600, 900, 0.12, 0.18)); }
        return;
      case 'swipe':
        e.phase = 'active';
        if (!e.hitP && rectHitsPlayer(e.x, e.y - e.ry - 20, e.x + e.face * (e.rx + 34), e.y + e.ry) && hitP(e, 'Zarpazo', t.swipeDmg, e.face * 420, -220)) e.hitP = true;
        if (e.stT > 0.15) { go(e, 'swipeRec'); recover(e); }
        return;
      case 'swipeRec':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 10));
        if (e.stT > t.swipeRec) { go(e, 'prowl'); e.cdT = t.cooldown; }
        return;
      case 'recrouch':
        e.busy = true; e.mx = lerp(e.mx, 0, Math.min(1, dt * 12)); faceTo(e, p.x);
        if (windup(e, t.crouch2 / (e.rageUntil > G.t ? 1.4 : 1))) stalkerLeap(e, t, 'Doble salto');
        return;
    }
    FOES.stalker.think(e, dt);
  },
  onEnter(e, st) {
    if (st === 'crouch') e.combo = 0;
    if (st === 'land' && (e.combo || 0) < FT(e).chain) { e.combo = (e.combo || 0) + 1; begin(e, 'Doble salto'); go(e, 'recrouch'); }
  },
  draw(ctx, e, P) {
    if (!P.mono && e.st === 'howl') glow(ctx, 30, -14, 60, [255, 230, 200], 0.4 * (1 - e.stT * 2));
    drawAs(ctx, e, P, 'stalker', e.ry / FOES.stalker.ry);
    const howl = e.st === 'howlW' ? e.tell : 0;
    for (let i = 0; i < 4; i++) E_poly(ctx, P, [[16 + i * 6, -14], [12 + i * 7, -32 - (i % 2) * 6 - howl * 6], [22 + i * 6, -12]], i % 2 ? '#bff6ff' : '#7fe8ff');
    E_line(ctx, P, [[40, -14], [46, -6]], 1.6, '#e8d8c8');
  },
}));
function alphaHowl(e, t) {
  go(e, 'howl'); release(e); e.howlT = 0;
  const pack = foes().filter(o => !o.dead && (o.def.pack || o.type) === 'lobo');
  for (const o of pack) if (Math.hypot(o.x - e.x, o.y - e.y) < t.howlR) o.rageUntil = G.t + t.howlTime;
  say(e, '¡AÚÚÚ!', '#ffe0c8', true); G.shake += 3;
  const x = e.x, y = e.y; addFx(0.6, (ctx, layer, t0) => shockRing(ctx, G.t, layer, t0, x, y, 160, PAL.wind));
  sfx(k => { const t0 = nowT(); vSaw(t0, 320, 520, 0.7 * k, 0.1, 1400, 700, 1, 0.3); });
  const others = pack.filter(o => o !== e).length;
  if (others < t.callN && G.t > e.callAt) {
    e.callAt = G.t + t.callCd;
    const p = G.player, edge = Math.abs(p.x - ARENA.x0) > Math.abs(p.x - ARENA.x1) ? ARENA.x0 + 40 : ARENA.x1 - 40;
    for (let i = others; i < t.callN; i++) { const n = spawnFoe('stalker', edge + (edge < 700 ? 1 : -1) * (i - others) * 60, undefined, { aware: true }); n.rageUntil = G.t + t.howlTime; n.cdT = 1.2; }
    text(edge, GROUND - 120, '¡LLAMADA!', '#ffb08a', true);
  }
}

// ---------------------------------------------------------------- Morsa matriarca (mundo de hielo)
addFoe(Object.assign({}, FOES.walrus, {
  id: 'walrusAlpha', name: 'Morsa matriarca', short: 'Mors. α', tier: 'élite', rx: 58, ry: 38,
  role: 'Élite de la Morsa: muros dobles y tobogán',
  question: 'La Morsa, pero te encierra: los muros salen a los dos lados y cuando estás entre pinchos se tira en tobogán, rebota en la pared y vuelve. Hay que saltarla dos veces sin caer en los pinchos.',
  moves: [
    { name: 'Muro doble', desc: 'El muro de carámbanos de la Morsa, hacia ti y a la vez otro más corto hacia atrás.' },
    { name: 'Tobogán', desc: 'Se tumba (flechas del alto de su cuerpo) y se lanza deslizándose sobre la tripa; rebota en la pared y vuelve (rebotes ajustables). Al acabar se queda boca arriba jadeando: recibe más daño.' },
    { name: 'Témpano', desc: 'Como la Morsa: si estás lejos o subido, te lanza un bloque de hielo.' },
  ],
  combos: 'Con un Colgante encima: mientras saltas el tobogán, mira arriba.',
  art: 'La Morsa más grande y vieja, con colmillos rotos y vueltos a crecer en hielo, cicatrices y una costra de escarcha gruesa en el lomo; que se lea como la misma especie.',
  tuning: eliteTuning('walrus', { hp: 650, poise: 160, kb: 0.1, spikeN: 6, cooldown: 1.6, walk: 60 }, {
    backN: [3, 0, 10, 1, 'Muro doble: carámbanos hacia atrás'],
    slideChance: [0.45, 0, 1, 0.05, 'Probabilidad de tobogán'], slideRange: [750, 100, 1400, 10, 'Tobogán: si estás a menos de (también acorta distancias)'], slideWindup: [0.7, 0.2, 2, 0.05, 'Tobogán: preparación (s)'],
    slideSpeed: [540, 150, 1000, 10, 'Tobogán: velocidad'], bounces: [1, 0, 4, 1, 'Tobogán: rebotes en la pared'],
    slideDmg: [18, 0, 60, 1, 'Tobogán: daño'], backDist: [520, 100, 1400, 10, 'Tobogán: tras el último rebote, recorre (y se para pasado de ti)'], rest: [1.6, 0, 5, 0.1, 'Tobogán: boca arriba al acabar (s)'], restVuln: [1.4, 1, 3, 0.05, 'Boca arriba: × daño recibido'],
  }),
  think(e, dt) {
    const t = FT(e), p = G.player, ad = Math.abs(dxP(e));
    switch (e.st) {
      case 'idle':
        if (e.cdT - dt <= 0 && e.slideRoll === undefined) e.slideRoll = Math.random() < t.slideChance;   // una tirada por ataque
        if (e.cdT - dt <= 0 && e.slideRoll && ad <= t.slideRange && playerUpTime() <= 0.5 && canStart(e)) {
          begin(e, 'Tobogán'); go(e, 'slideW'); e.sdir = Math.sign(dxP(e)) || e.face;
          warn({ e, kind: 'arrow', x: e.x, dir: e.sdir, len: e.rx * 2.2, h: e.ry * 1.6, t: t.slideWindup, follow: w => { w.x = e.x + e.sdir * e.rx * 0.6; w.dir = e.sdir; } });
          e.cdT = 0; return;
        }
        break;
      case 'slideW':
        e.mx = lerp(e.mx, -e.sdir * 20, Math.min(1, dt * 6)); e.face = e.sdir;
        if (windup(e, t.slideWindup)) { go(e, 'slide'); release(e); e.bounce = 0; e.hitP = false; sfx(k => SFXLIB.whoosh(k, 700, 250, 0.5, 0.2)); }
        return;
      case 'slide':
        e.phase = 'active'; e.mx = e.sdir * t.slideSpeed; e.face = e.sdir;
        if (!e.hitP && ellipseHitsPlayer(e, 4) && hitP(e, 'Tobogán', t.slideDmg, e.sdir * 520, -340)) e.hitP = true;
        if (Math.random() < dt * 14) dustPuff(e.x - e.sdir * e.rx, GROUND);
        if (e.bounce >= t.bounces && e.bounce > 0 && (e.back = (e.back || 0) + t.slideSpeed * dt) > t.backDist && (G.player.x - e.x) * e.sdir < 0) { go(e, 'restUp'); recover(e); e.mx = 0; makeVuln(e, t.restVuln, t.rest); return; }
        if (e.wall === e.sdir && e.stT > 0.1) {
          if (e.bounce < t.bounces) { e.bounce++; e.sdir = -e.sdir; e.stT = 0.05; e.hitP = false; e.back = 0; G.shake += 5; say(e, '¡BOING!', '#cdf6ff'); sfx(k => { const t0 = nowT(); vThump(t0, 100, 50, 0.25 * k, 0.6, 2); }); }
          else { go(e, 'restUp'); recover(e); e.mx = 0; makeVuln(e, t.restVuln, t.rest); G.shake += 6; }
        }
        return;
      case 'restUp':
        e.phase = 'recover'; e.mx = lerp(e.mx, 0, Math.min(1, dt * 6));
        if (e.stT > t.rest) { go(e, 'idle'); e.cdT = t.cooldown; }
        return;
    }
    FOES.walrus.think(e, dt);
  },
  onEnter(e, st) { if (st === 'spikeW' || st === 'tossW' || st === 'slideW') e.slideRoll = undefined; if (st === 'spike') { const t = FT(e); if (t.backN > 0) walrusSpikes(e, t, -e.sdir, t.backN); } },
  draw(ctx, e, P) {
    if (e.st === 'slide' || e.st === 'slideW' || e.st === 'restUp') {            // tumbada (tobogán) o boca arriba
      const k = e.ry / FOES.walrus.ry, belly = e.st === 'restUp';
      ctx.save(); ctx.translate(0, 10); ctx.scale(k, k * 0.75); if (belly) ctx.scale(1, -1);
      E_ell(ctx, P, 0, 6, 50, 24, belly ? '#c8d2da' : '#5a6f80'); E_ell(ctx, P, -4, 2, 44, 18, belly ? '#dfe6ec' : '#7a90a2');
      E_poly(ctx, P, [[40, 10], [74, 14], [42, 18]], ICE.pale);
      ctx.restore();
      if (!belly) E_eye(ctx, P, e, 34, 6, 2.6);
      return;
    }
    drawAs(ctx, e, P, 'walrus', e.ry / FOES.walrus.ry);
    for (let i = 0; i < 5; i++) E_poly(ctx, P, [[-40 + i * 13, -18], [-36 + i * 13, -30 - (i % 2) * 6], [-30 + i * 13, -18]], i % 2 ? ICE.snow : ICE.pale);
  },
}));

// ---------------------------------------------------------------- Seta madre (bosque)
addFoe(Object.assign({}, FOES.shroom, {
  id: 'shroomAlpha', name: 'Seta madre', short: 'Seta α', tier: 'élite', rx: 34, ry: 36,
  role: 'Élite de la Saltaseta: ondas y se divide',
  question: 'El ritmo de la Saltaseta, más corto (gran salto cada dos) y con ondas al caer que hay que saltar; y al perder vida suelta Saltasetas: ¿la rematas o limpias a las crías?',
  moves: [
    { name: 'Gran salto con ondas', desc: 'Como la Saltaseta, pero cada dos saltitos; al caer, además del ¡PLAF!, dos ondas de esporas corren por el suelo a los lados.' },
    { name: 'Esporada', desc: 'Al bajar de 2/3 y de 1/3 de vida se sacude y suelta dos Saltasetas, que saltan hacia los lados.' },
  ],
  combos: 'Con una Zarza: muros mientras saltas ondas.',
  art: 'La Saltaseta grande y vieja: sombrero ancho lleno de motas-cristal que brillan, musgo y setas pequeñas creciéndole encima (las crías que suelta).',
  tuning: eliteTuning('shroom', { hp: 420, poise: 120, kb: 0.3, hopX: 150, hopV: 340, bigEvery: 2, slamR: 80, slamDmg: 18, bigH: 170, keep: 140 }, {
    waveSpeed: [320, 80, 800, 10, 'Ondas: velocidad'], waveDist: [420, 50, 1200, 10, 'Ondas: recorrido'], waveH: [26, 10, 80, 1, 'Ondas: altura'], waveDmg: [10, 0, 40, 1, 'Ondas: daño'],
    splitN: [2, 0, 5, 1, 'Esporada: crías'],
  }),
  init(e) { FOES.shroom.init(e); e.splits = 0; },
  onEnter(e, st) { if (st === 'rest') { const t = FT(e); for (const d of [-1, 1]) groundWave(e, e.x + d * e.rx, d, { speed: t.waveSpeed, dist: t.waveDist, h: t.waveH, dmg: t.waveDmg, atk: 'Gran salto (ondas)', pal: { a: WOOD.moss, b: WOOD.pal.light } }); } },
  onHurt(e) {
    const t = FT(e), want = e.hp < e.hpMax / 3 ? 2 : e.hp < e.hpMax * 2 / 3 ? 1 : 0;
    while (e.splits < want) {
      e.splits++; say(e, '¡ESPORADA!', '#d8f5a8', true); e.stunT = Math.max(e.stunT, 0.4);
      for (let i = 0; i < t.splitN; i++) { const s = i % 2 ? 1 : -1, n = spawnFoe('shroom', e.x + s * 20, undefined, { aware: true }); n.y = e.y; groundHop(n, s * 200, 330); n.restT = 0.6; }
    }
  },
  draw(ctx, e, P) {
    drawAs(ctx, e, P, 'shroom', e.ry / FOES.shroom.ry);
    if (!P.mono) glow(ctx, 0, -e.ry * 0.5, 40, WOOD.pal.g, 0.25);
    for (const [x, y] of [[-14, -34], [10, -38]]) { E_ell(ctx, P, x, y, 7, 4, WOOD.cap); E_line(ctx, P, [[x, y + 2], [x, y + 8]], 2, WOOD.stem); }
  },
}));

// ---------------------------------------------------------------- Fénix (volcán)
addFoe(Object.assign({}, FOES.emberbird, {
  id: 'phoenix', name: 'Fénix', short: 'Fénix', tier: 'élite', rx: 28, ry: 22,
  role: 'Élite del Ave brasa: ida y vuelta, llamas y renace',
  question: 'La pasada del Ave brasa, pero de ida y vuelta y dejando llamas en el suelo donde caen las brasas; al morir su huevo aguanta más y renace antes: hay que ir a romperlo sí o sí.',
  moves: [
    { name: 'Pasada de ida y vuelta', desc: 'Cruza soltando brasas (franja marcada) y, al llegar, se da la vuelta y vuelve por encima (otra franja).' },
    { name: 'Llamas', desc: 'Las brasas que tocan el suelo dejan llamas un momento.' },
    { name: 'Renacer', desc: 'Como el Ave brasa, pero el huevo tiene más vida y eclosiona antes (renace a media vida, una sola vez).' },
  ],
  combos: 'Con el Escorpión: no puedes esperar su guardia quieto bajo las llamas.',
  art: 'El Ave brasa grande: alas largas de fuego con plumas de cristal ardiente, cresta de llamas, cola de tres plumas largas; brilla más que nada en pantalla.',
  tuning: eliteTuning('emberbird', { hp: 260, poise: 60, kb: 0.5, runSpeed: 560, dropEvery: 0.09, emberDmg: 8, eggHp: 70, reviveTime: 2.5, reviveHp: 0.5, cooldown: 1.8 }, {
    emberZone: [1.4, 0, 5, 0.1, 'Llamas: duran (s)'], backWindup: [0.35, 0.1, 1.5, 0.05, 'Vuelta: preparación (s)'],
  }),
  think(e, dt) {
    if (e.st === 'backW') {
      const t = FT(e); e.busy = true; e.mx = lerp(e.mx, 0, Math.min(1, dt * 10)); e.my = 0; e.face = e.rdir;
      if (windup(e, t.backWindup)) { go(e, 'run'); release(e); e.dropT = 0; e.hitP = false; }
      return;
    }
    FOES.emberbird.think(e, dt);
  },
  onEnter(e, st) {
    if (st === 'toStart') e.backDone = false;
    if (st === 'circle' && e.backDone === false) {
      const t = FT(e); e.backDone = true; e.rdir = -e.rdir; e.rx1 = clamp(e.x + e.rdir * t.runDist, ARENA.x0 + 20, ARENA.x1 - 20);
      begin(e, 'Pasada'); go(e, 'backW'); warn({ e, kind: 'band', x0: e.x, x1: e.rx1, t: t.backWindup + 0.3 });
    }
  },
  draw(ctx, e, P) {
    drawAs(ctx, e, P, 'emberbird', e.ry / FOES.emberbird.ry);
    for (let i = 0; i < 3; i++) E_poly(ctx, P, [[16 + i * 4, -18], [18 + i * 5, -34 - i * 4 + Math.sin(G.t * 10 + i) * 3], [22 + i * 4, -18]], i % 2 ? MAGMA.hot : MAGMA.glow);
  },
}));
