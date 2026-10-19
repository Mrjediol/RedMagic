// jugable-ui.js — panel (armas con su árbol, build de items, dificultad, opciones, ajustes con exportar/importar), entrada y bucle.
// Modo libre: marcas lo que quieras. Modo run: «🎲 Subir de nivel» y «🎁 Cofre» sortean como lo haría la run (te ofrecen 2-3).
'use strict';
const UI = {};
(function () {
  const KEY = 'hab.jugable.v4';                              // v4: el navegador guarda sólo los ajustes cambiados a mano
  const store = { load() { try { return JSON.parse(localStorage.getItem(KEY) || 'null'); } catch (e) { return null; } },
                  save() { try { localStorage.setItem(KEY, JSON.stringify(snapshot(true))); } catch (e) { /* sin almacenamiento */ } } };
  const DEFAULT_SLOTS = ['fan', 'star', 'sing'];
  const offers = {};                                         // ofertas abiertas por hueco (sorteo de subida de nivel)
  let chestOffer = null;
  // diff = sólo los ajustes que difieren del valor por defecto del código (así un cambio de valor por defecto llega al navegador)
  const specOf = id => id === 'react' ? REACT_TUNING : id === 'items' ? ITEM_TUNING : WEAPONS[id] && WEAPONS[id].tuning;
  function tuningDiff() { const out = {}; for (const id in G.tuning) { const sp = specOf(id); if (!sp) continue; for (const k in G.tuning[id]) if (sp[k] && G.tuning[id][k] !== sp[k][0]) (out[id] = out[id] || {})[k] = G.tuning[id][k]; } return out; }
  function snapshot(diff) { return { v: 4, slotCount: G.slotCount, slots: G.slots.map(s => ({ w: s.w, arch: s.arch, ups: s.ups })), build: G.build, settings: G.settings, tuning: diff ? tuningDiff() : G.tuning, cambiados: diff ? undefined : tuningDiff() }; }
  function apply(snap) {
    if (!snap || snap.v < 2) return;
    if (snap.slotCount) G.slotCount = clamp(snap.slotCount, 2, 3);
    if (snap.slots) snap.slots.forEach((s, i) => { if (G.slots[i] && WEAPONS[s.w]) Object.assign(G.slots[i], { w: s.w, arch: s.arch || null, ups: Object.assign({}, s.ups) }); });
    if (snap.build) G.build = { items: Object.fromEntries(Object.entries(snap.build.items || {}).filter(([k, v]) => v && ITEMS.some(i => i.id === k))) };
    if (snap.settings) Object.assign(G.settings, snap.settings);
    if (snap.tuning) for (const id in snap.tuning) if (G.tuning[id]) Object.assign(G.tuning[id], snap.tuning[id]);
  }
  function makeInst(s) { s.inst = WEAPONS[s.w].create(s); }

  // ------------------------------------------------ estado inicial
  const ids = Object.keys(WEAPONS);
  G.slots = DEFAULT_SLOTS.map((w, i) => ({ w: WEAPONS[w] ? w : ids[i % ids.length], arch: null, ups: {} }));
  G.player = makePlayer();
  apply(store.load());
  resetEnemies();
  G.slots.forEach(makeInst);

  // ------------------------------------------------ sorteos (lo que haría la run)
  const pick = (arr, n) => { const a = arr.slice(), out = []; while (a.length && out.length < n) out.push(a.splice(Math.floor(Math.random() * a.length), 1)[0]); return out; };
  function levelOffer(s) {
    const d = WEAPONS[s.w];
    if (d.archetypes && !s.arch) return pick(d.archetypes, 2).map(a => ({ kind: 'arch', id: a.id, name: a.name, desc: a.desc, tag: 'Evolución' }));
    const pool = (d.archetypes ? d.archetypes.find(a => a.id === s.arch).ups : d.ups || []).filter(u => !s.ups[u.id]);
    return pick(pool, 2).map(u => ({ kind: 'up', id: u.id, name: u.name, desc: u.desc, tag: 'Mejora' }));
  }
  function chestRoll() {
    const pool = ITEMS.filter(it => !G.build.items[it.id]), out = [];
    while (pool.length && out.length < 3) {
      const tot = pool.reduce((a, it) => a + RARITY[it.rarity].w, 0); let r = Math.random() * tot;
      const i = pool.findIndex(it => (r -= RARITY[it.rarity].w) <= 0); out.push(pool.splice(Math.max(0, i), 1)[0]);
    }
    return out;
  }
  const itemCount = () => Object.values(G.build.items).filter(Boolean).length;
  function giveItem(it) { if (itemCount() < MAX_ITEMS) G.build.items[it.id] = true; }

  // ------------------------------------------------ panel
  const panel = document.getElementById('panel');
  function el(tag, attrs = {}, ...kids) { const e = document.createElement(tag); for (const k in attrs) { if (k === 'on') for (const ev in attrs.on) e.addEventListener(ev, attrs.on[ev]); else if (k === 'style') e.setAttribute('style', attrs[k]); else if (k in e) e[k] = attrs[k]; else e.setAttribute(k, attrs[k]); } kids.flat().forEach(c => c != null && e.append(c)); return e; }
  const desc = t => el('span', { className: 'd' }, '— ' + t);
  function build() {
    panel.innerHTML = '';
    // ---- armas
    const slotsCard = el('div', { className: 'card' }, el('h3', {}, 'Armas equipadas', el('label', {}, el('input', { type: 'checkbox', checked: G.slotCount === 3, on: { change: e => { G.slotCount = e.target.checked ? 3 : 2; if (G.cur >= G.slotCount) G.cur = 0; changed(); } } }), '3er hueco')));
    for (let i = 0; i < G.slotCount; i++) slotsCard.append(slotBox(i));
    panel.append(slotsCard);
    // ---- build de items: cada item = 1 etiqueta elemental + 1 universal; sinergias a 2 / 4 / 6 (como el juego)
    const full = itemCount() >= MAX_ITEMS;
    const bc = el('div', { className: 'card' }, el('h3', {}, `Build · ${itemCount()}/${MAX_ITEMS} items`, el('button', { on: { click: () => { chestOffer = full ? [] : chestRoll(); build(); } } }, '🎁 Cofre')));
    if (chestOffer) bc.append(chestOffer.length
      ? offerRow(chestOffer.map(it => ({ name: it.name, desc: it.desc + ' · ' + it.tags.map(t => TAGS[t].name).join(' + '), tag: it.rarity, color: RARITY[it.rarity].color, go: () => { giveItem(it); chestOffer = null; changed(); } })), () => { chestOffer = null; build(); })
      : el('div', { className: 'd' }, 'Build llena: quita un item para abrir otro cofre.'));
    // resumen de sinergias
    const syn = el('div', { className: 'syn' });
    for (const k in TAGS) {
      const c = tagCount(k), tr = tier(k), d = TAGS[k];
      const row = el('div', { className: 'synrow' + (c ? '' : ' off') }, el('b', { style: `color:${d.color}` }, `${d.name} ${c}`));
      d.tiers.forEach((txt, i) => row.append(el('span', { className: 'tier' + (tr > i ? ' on' : ''), title: txt }, `${[2, 4, 6][i]}: ${txt}`)));
      syn.append(row);
    }
    bc.append(el('details', { open: true }, el('summary', {}, 'Sinergias (2 / 4 / 6) — el elemento se aplica con 4'), syn));
    bc.append(el('div', { className: 'sub' }, 'Items'));
    for (const it of ITEMS) {
      const on = !!G.build.items[it.id];
      bc.append(el('label', { className: 'item', style: `color:${RARITY[it.rarity].color}`, title: it.rarity },
        el('input', { type: 'checkbox', checked: on, disabled: !on && full, on: { change: e => { G.build.items[it.id] = e.target.checked; changed(); } } }),
        it.name, el('span', { className: 'tags' }, it.tags.map(t => el('i', { style: `color:${TAGS[t].color}` }, TAGS[t].name))), desc(`${it.rarity} · ${it.desc}`)));
    }
    bc.append(el('div', { className: 'row', style: 'margin-top:6px' }, el('button', { on: { click: () => { G.build = { items: {} }; changed(); } } }, 'Vaciar build')));
    panel.append(bc);
    // ---- dificultad
    const dif = el('div', { className: 'card' }, el('h3', {}, 'Dificultad'));
    const hv = el('span', {}, '×' + G.settings.hpMul.toFixed(2));
    dif.append(el('label', {}, 'Vida de los enemigos', el('input', { type: 'range', min: 0.5, max: 4, step: 0.25, value: G.settings.hpMul, on: { input: e => { G.settings.hpMul = +e.target.value; hv.textContent = '×' + G.settings.hpMul.toFixed(2); }, change: () => { resetEnemies(); store.save(); } } }), hv));
    dif.append(el('label', {}, el('input', { type: 'checkbox', checked: !!G.settings.enemyAttacks, on: { change: e => { G.settings.enemyAttacks = e.target.checked; store.save(); } } }), 'Los enemigos disparan'));
    panel.append(dif);
    // ---- opciones
    const opts = el('div', { className: 'card' }, el('h3', {}, 'Opciones'));
    const tog = (k, txt) => opts.append(el('label', {}, el('input', { type: 'checkbox', checked: !!G.settings[k], on: { change: e => { G.settings[k] = e.target.checked; if (k === 'sound' && e.target.checked) audioInit(); store.save(); } } }), txt));
    tog('reactions', 'Reacciones (añicos, deshielo, vapor)'); tog('frames', 'Impact frames'); tog('hitstop', 'Hit-stop (micro-pausa al golpear fuerte)');
    tog('sound', '🔊 Sonido'); tog('hitboxes', 'Ver hitboxes');
    const spv = el('span', {}, G.timeScale.toFixed(2) + '×');
    opts.append(el('label', {}, 'Velocidad del juego', el('input', { type: 'range', min: 0.1, max: 1, step: 0.05, value: G.timeScale, on: { input: e => { G.timeScale = +e.target.value; spv.textContent = G.timeScale.toFixed(2) + '×'; } } }), spv));
    opts.append(el('div', { className: 'row', style: 'margin-top:8px' },
      el('button', { on: { click: resetEnemies } }, '↺ Enemigos (R)'),
      el('button', { on: { click: exportJson } }, '⬇ Exportar ajustes'),
      el('button', { on: { click: () => fileIn.click() } }, '⬆ Importar'),
      el('button', { on: { click: resetTuning } }, 'Valores por defecto')));
    panel.append(opts);
    // ---- ajustes
    const tun = el('div', { className: 'card' }, el('h3', {}, 'Ajustes (se guardan en este navegador; ⬇ para pasarlos al repo)'));
    const used = [...new Set(G.slots.slice(0, G.slotCount).map(s => s.w))];
    for (const id of used) tun.append(tuningBlock(WEAPONS[id].name, id, WEAPONS[id].tuning, false));
    tun.append(tuningBlock('Items generales', 'items', ITEM_TUNING, false));
    tun.append(tuningBlock('Reacciones (sistema común)', 'react', REACT_TUNING, false));
    panel.append(tun);
  }
  function slotBox(i) {
    const s = G.slots[i], d = WEAPONS[s.w];
    const box = el('div', { className: 'slot' + (i === G.cur ? ' on' : '') });
    box.append(el('select', { on: { change: e => { s.w = e.target.value; s.arch = null; s.ups = {}; delete offers[i]; makeInst(s); changed(); } } }, ids.map(id => el('option', { value: id, selected: id === s.w }, `${i + 1} · ${WEAPONS[id].name}`))));
    box.append(el('div', { className: 'row lvl' }, el('b', {}, `Nivel ${slotLevel(s)}`),
      el('button', { on: { click: () => { offers[i] = levelOffer(s); build(); } } }, '🎲 Subir de nivel'),
      el('button', { on: { click: () => { s.arch = null; s.ups = {}; delete offers[i]; changed(); } } }, '↺ Nv 1')));
    if (offers[i]) box.append(offers[i].length
      ? offerRow(offers[i].map(o => ({ name: o.name, desc: o.desc, tag: o.tag, color: o.kind === 'arch' ? '#f0c8ff' : '#ffe39a', go: () => { if (o.kind === 'arch') s.arch = o.id; else s.ups[o.id] = true; delete offers[i]; changed(); } })), () => { delete offers[i]; build(); })
      : el('div', { className: 'd' }, 'Nivel máximo: no quedan mejoras.'));
    if (d.archetypes) {
      box.append(el('div', { className: 'sub' }, 'Evolución (nivel 2)'));
      for (const a of [{ id: null, name: 'Ninguna (base)' }, ...d.archetypes])
        box.append(el('label', { className: 'arch' }, el('input', { type: 'radio', name: 'arch' + i, checked: s.arch === a.id, on: { change: () => { s.arch = a.id; s.ups = {}; changed(); } } }), a.name, a.desc ? desc(a.desc) : null));
      const arch = d.archetypes.find(a => a.id === s.arch);
      if (arch) { box.append(el('div', { className: 'sub' }, `Mejoras de ${arch.name} (niveles 3-5)`)); for (const u of arch.ups) box.append(upBox(s, u)); }
    } else if (d.ups && d.ups.length) {
      box.append(el('div', { className: 'sub' }, 'Mejoras (provisionales: árbol pendiente de diseñar)'));
      for (const u of d.ups) box.append(upBox(s, u));
    }
    return box;
  }
  const upBox = (s, u) => el('label', { className: 'mod', title: u.desc }, el('input', { type: 'checkbox', checked: !!s.ups[u.id], on: { change: e => { s.ups[u.id] = e.target.checked; changed(); } } }), u.name, desc(u.desc));
  function offerRow(list, cancel) {
    const row = el('div', { className: 'offer' });
    for (const o of list) row.append(el('button', { className: 'card-btn', on: { click: o.go } }, el('small', { style: `color:${o.color}` }, o.tag), el('b', {}, o.name), el('span', {}, o.desc)));
    row.append(el('button', { className: 'x', title: 'Descartar', on: { click: cancel } }, '✕'));
    return row;
  }
  function tuningBlock(title, id, spec, open) {
    const det = el('details', { open }); det.append(el('summary', {}, title));
    const grid = el('div', { className: 'tun' });
    for (const k in spec) {
      const [, mn, mx, st, lab] = spec[k];
      const num = el('input', { type: 'number', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const rng = el('input', { type: 'range', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const set = v => { G.tuning[id][k] = +v; num.value = v; rng.value = v; store.save(); };
      num.oninput = e => set(e.target.value); rng.oninput = e => set(e.target.value);
      grid.append(el('span', {}, lab), num, rng);
    }
    det.append(grid); return det;
  }
  function changed(rebuild = true) { store.save(); if (rebuild) build(); }
  UI.refreshSlots = () => { panel.querySelectorAll('.slot').forEach((b, i) => b.classList.toggle('on', i === G.cur)); };
  function resetTuning() {
    for (const id in WEAPONS) G.tuning[id] = Object.fromEntries(Object.entries(WEAPONS[id].tuning).map(([k, v]) => [k, v[0]]));
    G.tuning.react = Object.fromEntries(Object.entries(REACT_TUNING).map(([k, v]) => [k, v[0]]));
    G.tuning.items = Object.fromEntries(Object.entries(ITEM_TUNING).map(([k, v]) => [k, v[0]]));
    changed();
  }
  function exportJson() {
    const blob = new Blob([JSON.stringify(Object.assign({ exportado: new Date().toISOString() }, snapshot()), null, 2)], { type: 'application/json' });
    el('a', { href: URL.createObjectURL(blob), download: 'ajustes.json' }).click();
  }
  const fileIn = el('input', { type: 'file', accept: '.json', style: 'display:none', on: { change: e => { const f = e.target.files[0]; if (!f) return; f.text().then(txt => { try { apply(JSON.parse(txt)); G.slots.forEach(makeInst); resetEnemies(); changed(); } catch (err) { console.warn(err); } }); } } });
  document.body.append(fileIn);
  build();

  // ------------------------------------------------ entrada
  const cv = document.getElementById('cv'), ctx = cv.getContext('2d');
  cv.width = VW * 2; cv.height = VH * 2;
  const toWorld = ev => { const r = cv.getBoundingClientRect(); G.mouse.sx = (ev.clientX - r.left) / r.width * VW; G.mouse.sy = (ev.clientY - r.top) / r.height * VH; };
  cv.addEventListener('mousemove', toWorld);
  cv.addEventListener('mousedown', ev => {
    cv.focus(); toWorld(ev); if (ev.button !== 0) return;
    if (G.settings.sound) audioInit();
    G.mouse.down = true; const s = G.slots[G.cur]; s.inst.onPress && s.inst.onPress(aimInfo());
  });
  window.addEventListener('mouseup', ev => { if (ev.button !== 0 || !G.mouse.down) return; G.mouse.down = false; const s = G.slots[G.cur]; s.inst.onRelease && s.inst.onRelease(aimInfo()); });
  cv.addEventListener('contextmenu', e => e.preventDefault());
  cv.addEventListener('wheel', e => { e.preventDefault(); swapWeapon(e.deltaY > 0 ? 1 : -1); }, { passive: false });
  cv.addEventListener('keydown', e => { if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault(); if (!G.keys[e.code]) onKeyDown(e.code); G.keys[e.code] = true; });
  cv.addEventListener('keyup', e => { G.keys[e.code] = false; });
  cv.addEventListener('blur', () => { G.keys = {}; });

  // ------------------------------------------------ bucle
  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000); last = now;
    G.mouse.x = G.cam.x + G.mouse.sx; G.mouse.y = G.mouse.sy;
    step(dt);
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    render(ctx);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
  cv.focus();
  window.__G = G;
})();
