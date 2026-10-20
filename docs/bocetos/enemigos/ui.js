// ui.js — panel de ENEMIGOS (columna de la izquierda del panel): encuentro, resultado, fichas de diseño, opciones y ajustes.
// El panel de armas / build / opciones de juego es el de la jugable de armas (../habilidades/jugable-ui.js), con su propio
// guardado (JUGABLE_PAGE.key) para no pisar el de aquella página. Este archivo va ANTES que jugable-ui.js.
'use strict';
window.JUGABLE_PAGE = { key: 'enemigos.jugable.v1', slots: ['fan', 'star', 'sing'], noDifficulty: true };

(function () {
  const KEY = 'enemigos.ajustes.v1';
  const store = {
    load() { try { return JSON.parse(localStorage.getItem(KEY) || 'null'); } catch (e) { return null; } },
    save() { try { localStorage.setItem(KEY, JSON.stringify(snapshot(true))); } catch (e) { /* sin almacenamiento */ } },
  };
  const ids = Object.keys(FOES);
  const spec = id => FOES[id.slice(4)] && FOES[id.slice(4)].tuning;
  function tuningDiff() {
    const out = {};
    for (const id in G.tuning) { if (!id.startsWith('foe_')) continue; const sp = spec(id); if (!sp) continue; for (const k in G.tuning[id]) if (sp[k] && G.tuning[id][k] !== sp[k][0]) (out[id] = out[id] || {})[k] = G.tuning[id][k]; }
    return out;
  }
  let duel = { type: ids[0], n: 1 }, best = {};
  function snapshot(diff) { return { v: 1, es: G.es, enc: G.enc && G.enc.id, duel, best, tuning: diff ? tuningDiff() : Object.fromEntries(Object.entries(G.tuning).filter(([k]) => k.startsWith('foe_'))), cambiados: diff ? undefined : tuningDiff() }; }
  function apply(s) {
    if (!s || !s.v) return;
    if (s.es) Object.assign(G.es, s.es);
    if (s.duel && FOES[s.duel.type]) duel = s.duel;
    if (s.best) best = s.best;
    if (s.tuning) for (const id in s.tuning) if (G.tuning[id]) Object.assign(G.tuning[id], s.tuning[id]);
    if (s.enc) G.enc = encById(s.enc);
  }
  function encById(id) {
    if (!id) return null;
    if (id.startsWith('duelo:')) { const [, type, n] = id.split(':'); return FOES[type] ? duelEncounter(type, +n || 1) : null; }
    return ENCOUNTERS.find(e => e.id === id) || null;
  }
  apply(store.load());
  if (!G.enc) G.enc = duelEncounter(duel.type, duel.n);

  // ------------------------------------------------ panel
  const panel = document.getElementById('epanel');
  function el(tag, attrs = {}, ...kids) { const e = document.createElement(tag); for (const k in attrs) { if (k === 'on') for (const ev in attrs.on) e.addEventListener(ev, attrs.on[ev]); else if (k === 'style') e.setAttribute('style', attrs[k]); else if (k in e) e[k] = attrs[k]; else e.setAttribute(k, attrs[k]); } kids.flat().forEach(c => c != null && e.append(c)); return e; }
  const run = enc => { startEncounter(enc); store.save(); build(); document.getElementById('cv').focus(); };
  let resBox;
  function build() {
    panel.innerHTML = '';
    // ---- encuentro
    const ec = el('div', { className: 'card' }, el('h3', {}, 'Encuentro', el('button', { title: 'Reiniciar (R)', on: { click: () => run(G.enc) } }, '↺ Reiniciar (R)')));
    ec.append(el('div', { className: 'row', style: 'align-items:center' },
      el('select', { style: 'flex:1', on: { change: e => { duel.type = e.target.value; store.save(); } } }, ids.map(id => el('option', { value: id, selected: id === duel.type }, FOES[id].name + ' — ' + FOES[id].role))),
      el('select', { on: { change: e => { duel.n = +e.target.value; store.save(); } } }, [1, 2, 3, 4].map(n => el('option', { value: n, selected: n === duel.n }, '×' + n))),
      el('button', { on: { click: () => run(duelEncounter(duel.type, duel.n)) } }, '▶ Duelo')));
    const grid = el('div', { className: 'encs' });
    for (const enc of ENCOUNTERS) grid.append(el('button', { className: 'enc' + (G.enc && G.enc.id === enc.id ? ' on' : ''), title: enc.desc, on: { click: () => run(enc) } }, el('b', {}, enc.name), el('span', {}, enc.desc)));
    ec.append(grid);
    if (G.enc) ec.append(el('div', { className: 'd', style: 'margin-top:6px' }, 'Ahora: ', el('b', {}, G.enc.name), ' — ', G.enc.desc || ''));
    panel.append(ec);
    // ---- resultado (se refresca solo)
    resBox = el('div', { className: 'card' });
    panel.append(resBox); refreshResult();
    // ---- fichas
    const fc = el('div', { className: 'card' }, el('h3', {}, 'Enemigos (diseño)'));
    fc.append(el('div', { className: 'd' }, 'Lenguaje común: el cuerpo se pone naranja y el ojo blanco mientras prepara un ataque (si le haces bastante daño ahí, se interrumpe: barra blanca bajo la vida); destello blanco = sale el golpe; rojo en el suelo / aire = zona que va a doler, el relleno marca cuánto falta; estrellas = aturdido; anillo rosa = recibe más daño.'));
    for (const id of ids) {
      const d = FOES[id], det = el('details', { className: 'foe' });
      det.append(el('summary', {}, el('b', {}, d.name), ' — ', d.role));
      det.append(el('div', { className: 'q' }, el('b', {}, 'Pregunta al jugador: '), d.question));
      for (const m of d.moves) det.append(el('div', { className: 'mv' }, el('b', {}, m.name), ' — ', m.desc));
      if (d.combos) det.append(el('div', { className: 'mv' }, el('b', {}, 'Combina con: '), d.combos));
      if (d.art) det.append(el('div', { className: 'mv art' }, el('b', {}, 'Arte (idea, sin decidir): '), d.art));
      det.append(el('div', { className: 'row', style: 'margin-top:4px' }, el('button', { on: { click: () => { duel.type = id; run(duelEncounter(id, duel.n)); } } }, '▶ Duelo')));
      fc.append(det);
    }
    panel.append(fc);
    // ---- opciones
    const oc = el('div', { className: 'card' }, el('h3', {}, 'Opciones de enemigos'));
    const tog = (k, txt) => oc.append(el('label', {}, el('input', { type: 'checkbox', checked: !!G.es[k], on: { change: e => { G.es[k] = e.target.checked; store.save(); } } }), txt));
    tog('harmless', 'Inofensivos (atacan pero no hacen daño: para mirar patrones)');
    tog('frozenAI', 'Quietos (no piensan: dianas)');
    tog('dashIframes', 'Dash invulnerable (propuesta: los ataques se esquivan con el dash)');
    tog('respawn', 'Repetir el encuentro al despejarlo (3 s)');
    tog('showStates', 'Ver estado de cada enemigo (depuración)');
    const sl = (k, txt, mn, mx, st, fmt, after) => { const v = el('span', {}, fmt(G.es[k])); oc.append(el('label', {}, txt, el('input', { type: 'range', min: mn, max: mx, step: st, value: G.es[k], on: { input: e => { G.es[k] = +e.target.value; v.textContent = fmt(G.es[k]); }, change: () => { store.save(); if (after) after(); } } }), v)); };
    sl('hpMul', 'Vida', 0.25, 4, 0.25, v => '×' + v.toFixed(2), () => run(G.enc));
    sl('dmgMul', 'Daño que hacen', 0, 3, 0.1, v => '×' + v.toFixed(1));
    sl('maxAttackers', 'Atacando a la vez (como mucho)', 1, 8, 1, v => '' + v);
    panel.append(oc);
    // ---- ajustes
    const tc = el('div', { className: 'card' }, el('h3', {}, 'Ajustes de enemigos (los del encuentro)'));
    const used = new Set((G.enc ? G.enc.list : []).map(s => s.type)); if (used.has('hive')) used.add('drone');
    for (const id of ids) if (used.has(id)) tc.append(tuningBlock(FOES[id].name, 'foe_' + id, FOES[id].tuning, used.size === 1));
    tc.append(el('div', { className: 'row', style: 'margin-top:8px' },
      el('button', { on: { click: exportJson } }, '⬇ Exportar ajustes'),
      el('button', { on: { click: () => fileIn.click() } }, '⬆ Importar'),
      el('button', { on: { click: resetTuning } }, 'Valores por defecto')));
    panel.append(tc);
  }
  function refreshResult() {
    if (!resBox) return;
    const F = G.fight; resBox.innerHTML = '';
    resBox.append(el('h3', {}, 'Resultado', el('span', { className: 'd' }, F && F.tEnd ? 'despejado' : 'en curso')));
    if (!F) return;
    const tt = (F.tEnd || G.t) - F.t0, b = best[G.enc && G.enc.id];
    resBox.append(el('div', { className: 'stats' },
      el('span', {}, el('b', {}, tt.toFixed(1) + ' s'), 'tiempo'), el('span', {}, el('b', {}, F.dmgTaken + ''), 'daño recibido'),
      el('span', {}, el('b', {}, F.dodges + ''), 'esquivas (dash)'), el('span', {}, el('b', {}, F.interrupts + ''), 'interrupciones'),
      el('span', {}, el('b', {}, F.kills + ''), 'bajas'), F.deaths ? el('span', {}, el('b', {}, F.deaths + ''), 'muertes') : null));
    if (b) resBox.append(el('div', { className: 'd' }, `Mejor en este encuentro: ${b.t.toFixed(1)} s · ${b.dmg} de daño recibido`));
    const rows = Object.entries(F.hits).sort((a, c) => c[1].dmg - a[1].dmg || c[1].n - a[1].n);
    if (rows.length) {
      resBox.append(el('div', { className: 'sub' }, 'Qué te ha dado'));
      for (const [k, h] of rows) resBox.append(el('div', { className: 'hitrow' }, el('span', {}, k), el('b', {}, h.dmg ? `${h.dmg} (${h.n}×)` : `${h.n}×`)));
    }
  }
  EUI.onEncounter = () => { if (panel.childNodes.length) build(); };
  EUI.onClear = F => {
    const id = G.enc && G.enc.id, tt = F.tEnd - F.t0, b = best[id];
    if (id && (!b || tt < b.t)) best[id] = { t: tt, dmg: F.dmgTaken };
    store.save(); refreshResult();
  };
  setInterval(refreshResult, 300);
  function tuningBlock(title, id, sp, open) {
    const det = el('details', { open }); det.append(el('summary', {}, title));
    const grid = el('div', { className: 'tun' });
    for (const k in sp) {
      const [, mn, mx, st, lab] = sp[k];
      const num = el('input', { type: 'number', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const rng = el('input', { type: 'range', min: mn, max: mx, step: st, value: G.tuning[id][k] });
      const set = v => { G.tuning[id][k] = +v; num.value = v; rng.value = v; store.save(); };
      num.oninput = e => set(e.target.value); rng.oninput = e => set(e.target.value);
      grid.append(el('span', {}, lab), num, rng);
    }
    det.append(grid); return det;
  }
  function resetTuning() { for (const id of ids) G.tuning['foe_' + id] = Object.fromEntries(Object.entries(FOES[id].tuning).map(([k, v]) => [k, v[0]])); store.save(); run(G.enc); }
  function exportJson() {
    const blob = new Blob([JSON.stringify(Object.assign({ exportado: new Date().toISOString() }, snapshot(false)), null, 2)], { type: 'application/json' });
    el('a', { href: URL.createObjectURL(blob), download: 'ajustes-enemigos.json' }).click();
  }
  const fileIn = el('input', { type: 'file', accept: '.json', style: 'display:none', on: { change: e => { const f = e.target.files[0]; if (!f) return; f.text().then(txt => { try { apply(JSON.parse(txt)); store.save(); run(G.enc); } catch (err) { console.warn(err); } }); } } });
  document.body.append(fileIn);
  build();
})();
