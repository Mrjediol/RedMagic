// modules/gesture-timeline.js
// -----------------------------------------------------------------------------
// THE GESTURE TIMELINE EDITOR — one gesture (rig-gestures.js) laid on a clock: its steps as blocks,
// the instants the attack leaves (releases) and its effects (rig-fx.js) as markers, and a playhead
// that stops the gesture at any instant.
//
// It is ONE editor meant to be mounted wherever a gesture is edited (today 🧬 Enemy Studio ▸ Gestos;
// later the focused "Gestos y ataques" tab — docs/plan-gesture-attack-editor.md): it owns the
// timeline's DOM, what is selected on it, and every edit of the gesture's steps / releases / effects.
// It knows nothing of the body or the canvas — whoever mounts it hands in a small `host` interface:
//
//   gesture()                 the gesture being edited (null = none open)
//   lead() / setLead(s)       seconds of warning the preview launches it with (the attack's telegraph)
//   time()                    where the playhead is (s)
//   playing() / togglePlay()  playback
//   seek(t)                   playhead to t, paused (keeps a pose being edited)
//   scrub(t)                  the same, by the user's hand: leaves pose editing
//   posingStep()              index of the step whose pose is being edited on the canvas, or -1
//   pose(step | -1)           start / switch / stop editing a step's pose
//   renderPose(el, step)      the pose's own panel (which parts are in it, their numbers)
//   placing()                 { kind: 'fx' | 'release', index } while the next canvas click places it
//   place(kind, index)        toggle that
//   parts()                   [{ uid, name }] — what an effect can hang from / a release can leave from
//   hasMuzzle()               the body has a weapon tip
//   defaultAnchor()           { anchor, part } for a new effect
//   fxEntries()               Map<library id, entry> — effects with frames
//   changed()                 the gesture was edited: panels, canvas, export
//   refresh()                 only what is shown changed (selection): panels + canvas
//   redraw()                  canvas + export, no panels (a number being typed)
//   status(text)
//
// Two renders: `renderDock` (transport + track, under the canvas) and `renderFields` (the selected
// step / release / effect, in the gesture's card). `updatePlayhead` moves the playhead without
// rebuilding anything (called every frame while it plays).

import * as G from './rig-gestures.js';
import * as FX from './rig-fx.js';
import * as F from './enemy-form-fields.js';

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};
const r2 = (v) => Math.round(v * 100) / 100;
const clamp01 = (x) => Math.min(1, Math.max(0, x));
const secs = (t) => `${t.toFixed(2)} s`;

export function createGestureTimeline(host) {
  let sel = null;      // { kind: 'step' | 'release' | 'fx' | 'recover', index }
  let dock = null;     // the dock's live elements: { track, playhead, time, play, total }
  let drag = null;     // a pointer drag on the track (scrub, a marker, a block's edge)
  let fxMore = false;  // the effect's "fine tuning" fold stays as the user left it

  const gesture = () => host.gesture();

  /** The gesture was edited. The shell counts unsaved edits by `change` events: a drag on the track is one too. */
  function edited() {
    host.changed();
    if (dock) dock.track.dispatchEvent(new Event('change', { bubbles: true }));
  }
  const timelineOf = (g) => G.timeline(g, host.lead());
  const stepName = (g, i) => (i >= g.steps.length ? 'Vuelta al reposo' : g.steps[i].name || `Paso ${i + 1}`);

  /** The selection, dropped when what it named is gone. */
  function selection() {
    const g = gesture();
    if (!g || !sel) return null;
    const list = { step: g.steps, release: g.releases, fx: g.fx || [] }[sel.kind];
    if (list && !list[sel.index]) sel = null;
    return sel;
  }

  /** Selects something on the timeline; the playhead goes to it so the canvas shows that instant. */
  function select(kind, index) {
    const g = gesture();
    if (!g) return;
    sel = kind ? { kind, index } : null;
    const tl = timelineOf(g);
    const wasPosing = host.posingStep() >= 0;
    if (kind === 'step') {
      host.seek(G.timeOf(tl, index, 1));           // a step IS its pose: the end of the step
      if (wasPosing) host.pose(index);             // editing poses: carry on with this one
    } else {
      if (wasPosing) host.pose(-1);
      if (kind === 'release') host.seek(G.timeOf(tl, Math.min(g.releases[index].step, tl.count - 1), g.releases[index].at));
      else if (kind === 'fx') host.seek(G.timeOf(tl, g.fx[index].step, g.fx[index].at));
      else if (kind === 'recover') host.seek(tl.recoverStart);
    }
    host.refresh();
  }

  function reset() { sel = null; drag = null; }

  // ============================================================ edits

  function addStep() {
    const g = gesture();
    if (!g) return;
    const after = sel?.kind === 'step' ? sel.index : g.steps.length - 1;
    const at = G.insertStep(g, after, G.createStep(`Paso ${g.steps.length + 1}`));
    sel = { kind: 'step', index: at };
    host.seek(G.timeOf(timelineOf(g), at, 1));
    edited();
    host.status(`Paso añadido a "${g.name}". "✋ Posar este paso" y arrastra las piezas a donde llegan.`);
  }

  function addRelease() {
    const g = gesture();
    if (!g) return;
    const tl = timelineOf(g);
    const inst = G.instantAt(tl, Math.min(host.time(), tl.recoverStart));
    const step = Math.min(inst.step, tl.count - 1);
    // The playhead at rest (0) means "no instant chosen": the end of the last step, where an attack usually leaves.
    const release = host.time() <= 0 ? G.createRelease(tl.count - 1, 1) : G.createRelease(step, inst.step > step ? 1 : r2(inst.at));
    g.releases.push(release);
    sel = { kind: 'release', index: g.releases.length - 1 };
    host.seek(G.timeOf(timelineOf(g), release.step, release.at));
    edited();
    host.status('Suelta añadida: en ese instante sale el ataque. Arrástrala por la línea de tiempo; el punto de salida, en el lienzo.');
  }

  /** Hangs a library effect at an instant (default: where the playhead is). */
  function addFx(libraryId, instant = null) {
    const g = gesture();
    if (!g || !host.fxEntries().has(libraryId)) return false;
    const tl = timelineOf(g);
    const inst = instant || G.instantAt(tl, host.time());
    const anchor = host.defaultAnchor();
    const f = FX.createFx(libraryId, inst.step, r2(inst.at), anchor.anchor, anchor.part);
    // At the start of a step that lasts the warning it is a charge (it loops until the step ends); else a burst.
    f.loop = inst.step < tl.count && !!g.steps[inst.step].stretch && inst.at < 0.05;
    FX.fxList(g).push(f);
    sel = { kind: 'fx', index: g.fx.length - 1 };
    edited();
    return true;
  }

  function removeSelected() {
    const g = gesture();
    const s = selection();
    if (!g || !s) return;
    if (s.kind === 'step') {
      if (!G.removeStep(g, s.index)) { host.status('Un gesto tiene al menos un paso.'); return; }
      if (host.posingStep() >= 0) host.pose(-1);
      sel = { kind: 'step', index: Math.min(s.index, g.steps.length - 1) };
    } else if (s.kind === 'release') { g.releases.splice(s.index, 1); sel = null; }
    else if (s.kind === 'fx') { g.fx.splice(s.index, 1); sel = null; }
    else { host.status('La vuelta al reposo no se quita: sólo se cambia cuánto dura.'); return; }
    edited();
  }

  // ============================================================ the dock: transport + track

  function renderDock(root) {
    root.innerHTML = '';
    dock = null;
    const g = gesture();
    if (!g) return;
    const tl = timelineOf(g);
    const pct = (t) => `${(t / tl.total) * 100}%`;
    const s = selection();

    // --- transport
    const bar = el('div', 'gtBar');
    const play = el('button', 'small');
    play.dataset.action = 'gesturePlay';     // Space plays it (app.js ▸ playActions)
    play.dataset.open = '1';
    play.dataset.nodirty = '1';
    play.title = 'Reproduce el gesto con los tiempos del juego (RigDriverCode). Espacio.';
    play.onclick = () => host.togglePlay();
    bar.appendChild(play);
    const rewind = el('button', 'small', '⏮');
    rewind.dataset.nodirty = '1';
    rewind.title = 'Al principio (el cuerpo en reposo).';
    rewind.onclick = () => host.scrub(0);
    bar.appendChild(rewind);
    const time = el('span', 'gtTime');
    bar.appendChild(time);

    const leadWrap = el('label', 'gtLead', 'Aviso ');
    leadWrap.title = 'Sólo para probar: los segundos de aviso con que se lanza el gesto. En el juego es el "telegraph" del ataque que lo use: los pasos marcados "dura el aviso" se reparten ese tiempo hasta la primera suelta.';
    const lead = document.createElement('input');
    lead.type = 'number'; lead.min = '0.05'; lead.step = '0.05'; lead.value = String(host.lead());
    lead.dataset.nodirty = '1';
    lead.onchange = () => { host.setLead(parseFloat(lead.value) > 0 ? parseFloat(lead.value) : 0.6); host.refresh(); };
    leadWrap.append(lead, ' s');
    bar.appendChild(leadWrap);

    bar.appendChild(el('span', 'gtSpacer'));
    const addS = el('button', 'small', '＋ Paso');
    addS.title = 'Un paso más: otra pose a la que llega el cuerpo. Se añade tras el paso elegido.';
    addS.onclick = addStep;
    const addR = el('button', 'small', '◆ Suelta');
    addR.title = 'Un instante en el que sale el ataque, donde está el cursor. Varias = dispara varias veces.';
    addR.onclick = addRelease;
    bar.append(addS, addR);

    const entries = [...host.fxEntries().values()];
    const addF = document.createElement('select');
    addF.className = 'gtAddFx';
    addF.title = 'Cuelga un efecto de la biblioteca en el instante donde está el cursor. También se puede arrastrar desde Efectos a la línea de tiempo.';
    addF.appendChild(Object.assign(el('option', null, entries.length ? '✨ Efecto…' : '✨ (no hay efectos en la biblioteca)'), { value: '' }));
    entries.forEach((e) => addF.appendChild(Object.assign(el('option', null, e.enemyName), { value: e.id })));
    addF.disabled = !entries.length;
    addF.onchange = () => { if (addF.value) addFx(addF.value); };
    bar.appendChild(addF);
    root.appendChild(bar);

    // --- track
    const track = el('div', 'gtTrack');
    const timeAt = (clientX) => timeAtX(clientX, tl.total);

    // ruler: a tick every 0.1 / 0.25 / 0.5 / 1 s, whichever leaves room to read them
    const ruler = el('div', 'gtRuler');
    const every = [0.1, 0.25, 0.5, 1, 2, 5].find((k) => tl.total / k <= 12) || 10;
    for (let t = 0; t <= tl.total + 1e-6; t += every) {
      const tick = el('span', 'gtTick', `${r2(t)}`);
      tick.style.left = pct(t);
      ruler.appendChild(tick);
    }
    ruler.title = 'Arrastra para parar el gesto en cualquier instante.';
    ruler.addEventListener('mousedown', (e) => { e.preventDefault(); drag = { kind: 'scrub' }; host.scrub(timeAt(e.clientX)); });
    track.appendChild(ruler);

    // steps
    const first = tl.releases.reduce((a, b) => (!a || b.time < a.time ? b : a), null);
    const steps = el('div', 'gtLane gtSteps');
    g.steps.forEach((step, i) => {
      const stretched = step.stretch && (!first || i <= first.step);
      const block = el('div', `gtStep${s?.kind === 'step' && s.index === i ? ' selected' : ''}${host.posingStep() === i ? ' posing' : ''}${stretched ? ' stretch' : ''}`);
      block.style.left = pct(tl.starts[i]);
      block.style.width = pct(tl.durations[i]);
      block.appendChild(el('span', 'gtStepName', stepName(g, i)));
      block.appendChild(el('span', 'gtStepTime', stretched ? `⇔ ${secs(tl.durations[i])}` : secs(tl.durations[i])));
      block.title = `${stepName(g, i)} — ${secs(tl.durations[i])}${stretched ? ' (dura el aviso del ataque)' : ''} · ${step.poses.length} pieza(s) en su pose. Clic: elegirlo y ver su pose. Arrástralo sobre otro para cambiar el orden.`;
      block.onclick = () => select('step', i);
      block.draggable = true;
      block.addEventListener('dragstart', (e) => { e.dataTransfer.setData('text/rig-step', String(i)); e.dataTransfer.effectAllowed = 'move'; });
      block.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('text/rig-step')) { e.preventDefault(); block.classList.add('dropTarget'); } });
      block.addEventListener('dragleave', () => block.classList.remove('dropTarget'));
      block.addEventListener('drop', (e) => {
        if (!e.dataTransfer.types.includes('text/rig-step')) return;
        e.preventDefault();
        e.stopPropagation();
        const from = parseInt(e.dataTransfer.getData('text/rig-step'), 10);
        if (!G.moveStep(g, from, i)) return;
        if (host.posingStep() >= 0) host.pose(-1);
        sel = { kind: 'step', index: i };
        host.seek(G.timeOf(timelineOf(g), i, 1));
        edited();
      });
      if (!stretched) {   // its right edge: drag = its duration
        const edge = el('span', 'gtEdge');
        edge.title = 'Arrastra: cuánto dura el paso.';
        edge.addEventListener('mousedown', (e) => {
          e.preventDefault(); e.stopPropagation();
          drag = { kind: 'duration', target: step, key: 'duration', x0: e.clientX, d0: tl.durations[i], perPx: tl.total / Math.max(1, track.getBoundingClientRect().width) };
        });
        block.appendChild(edge);
      }
      steps.appendChild(block);
    });
    const back = el('div', `gtStep gtRecover${s?.kind === 'recover' ? ' selected' : ''}`);
    back.style.left = pct(tl.recoverStart);
    back.style.width = pct(tl.recover);
    back.appendChild(el('span', 'gtStepName', '↩ vuelta'));
    back.appendChild(el('span', 'gtStepTime', secs(tl.recover)));
    back.title = `La vuelta al reposo — ${secs(tl.recover)}.`;
    back.onclick = () => select('recover', 0);
    const backEdge = el('span', 'gtEdge');
    backEdge.title = 'Arrastra: cuánto tarda en volver al reposo.';
    backEdge.addEventListener('mousedown', (e) => {
      e.preventDefault(); e.stopPropagation();
      drag = { kind: 'duration', target: g, key: 'recover', x0: e.clientX, d0: tl.recover, perPx: tl.total / Math.max(1, track.getBoundingClientRect().width) };
    });
    back.appendChild(backEdge);
    steps.appendChild(back);
    track.appendChild(steps);

    // releases
    const releases = el('div', 'gtLane gtReleases');
    releases.title = 'Sueltas: los instantes en que sale el ataque.';
    tl.releases.forEach((r) => {
      const implicit = r.index < 0;
      const mark = el('span', `gtRelease${implicit ? ' implicit' : ''}${s?.kind === 'release' && s.index === r.index ? ' selected' : ''}`, '◆');
      mark.style.left = pct(r.time);
      mark.title = implicit
        ? 'Sin sueltas escritas, el ataque sale aquí: al acabar el último paso, desde la boca. "◆ Suelta" pone una donde quieras.'
        : `Suelta ${r.index + 1}: sale el ataque a los ${secs(r.time)}. Arrástrala para moverla.`;
      if (!implicit) {
        mark.addEventListener('mousedown', (e) => {
          e.preventDefault(); e.stopPropagation();
          sel = { kind: 'release', index: r.index };
          if (host.posingStep() >= 0) host.pose(-1);
          drag = { kind: 'release', index: r.index, tl, moved: false };
          host.seek(r.time);
          host.refresh();
        });
      }
      releases.appendChild(mark);
    });
    track.appendChild(releases);

    // effects: one thin row each (a loop is a bar until it goes out, a burst lasts its frames)
    (g.fx || []).forEach((f, i) => {
      const entry = host.fxEntries().get(f.fx);
      const frames = FX.fxFrames(entry, f.loop);
      const start = G.timeOf(tl, f.step, f.at || 0);
      const endSeg = G.segment(tl, FX.fxEndStep(f, tl.count));
      const end = f.loop ? endSeg.start + endSeg.length : Math.min(tl.total, start + (frames ? frames.duration : 0));
      const lane = el('div', 'gtLane gtFxLane');
      const mark = el('div', `gtFx${f.loop ? ' loop' : ''}${s?.kind === 'fx' && s.index === i ? ' selected' : ''}${entry ? '' : ' missing'}`);
      mark.style.left = pct(start);
      mark.style.width = `max(18px, ${pct(Math.max(0, end - start))})`;
      mark.appendChild(el('span', null, `✨ ${fxName(f)}`));
      mark.title = `${fxName(f)} — sale a los ${secs(start)}${f.loop ? `, en bucle hasta ${secs(end)}` : ''}. Arrástralo para moverlo.`;
      mark.addEventListener('mousedown', (e) => {
        e.preventDefault(); e.stopPropagation();
        sel = { kind: 'fx', index: i };
        if (host.posingStep() >= 0) host.pose(-1);
        drag = { kind: 'fx', index: i, tl, grab: timeAt(e.clientX) - start, moved: false };
        host.seek(start);
        host.refresh();
      });
      lane.appendChild(mark);
      track.appendChild(lane);
    });

    // an effect dragged from the library list lands at the instant it is dropped on
    track.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('text/rig-fx')) { e.preventDefault(); track.classList.add('dropTarget'); } });
    track.addEventListener('dragleave', () => track.classList.remove('dropTarget'));
    track.addEventListener('drop', (e) => {
      track.classList.remove('dropTarget');
      const id = e.dataTransfer.getData('text/rig-fx');
      if (!id) return;
      e.preventDefault();
      e.stopPropagation();
      const t = timeAt(e.clientX);
      if (addFx(id, roundInstant(G.instantAt(tl, t)))) host.status(`Efecto añadido a "${g.name}" a los ${secs(t)}. Colócalo en el lienzo con "⌖ Colocar".`);
    });

    const playhead = el('div', 'gtPlayhead');
    track.appendChild(playhead);
    root.appendChild(track);

    dock = { track, playhead, time, play, total: tl.total };
    updatePlayhead();
  }

  const roundInstant = (inst) => ({ step: inst.step, at: r2(inst.at) });

  /** The clock time under a screen x, on a track `total` seconds long (the track on screen right now). */
  function timeAtX(clientX, total) {
    const rect = dock.track.getBoundingClientRect();
    return clamp01((clientX - rect.left) / Math.max(1, rect.width)) * total;
  }

  function fxName(f) {
    const entry = host.fxEntries().get(f.fx);
    if (entry) return entry.enemyName;
    if (f.prefab) return f.prefab.split('/').pop().replace(/\.prefab$/i, '');
    return '(efecto borrado)';
  }

  /** The playhead and the clock, without rebuilding the dock. */
  function updatePlayhead() {
    if (!dock) return;
    const t = Math.min(dock.total, Math.max(0, host.time()));
    dock.playhead.style.left = `${(t / dock.total) * 100}%`;
    dock.time.textContent = `${t.toFixed(2)} / ${dock.total.toFixed(2)} s`;
    dock.play.textContent = host.playing() ? '⏸ Pausa' : '▶ Probar';
    dock.play.classList.toggle('active', host.playing());
  }

  window.addEventListener('mousemove', (e) => {
    if (!drag) return;
    const g = gesture();
    if (!g || !dock) { drag = null; return; }
    if (drag.kind === 'scrub') { host.scrub(timeAtX(e.clientX, dock.total)); return; }

    if (drag.kind === 'duration') {
      drag.target[drag.key] = Math.max(0.01, r2(drag.d0 + (e.clientX - drag.x0) * drag.perPx));
    } else if (drag.kind === 'release') {
      // On the timeline as it was when the drag began: moving the first release restretches the steps under it.
      const r = g.releases[drag.index];
      const inst = G.instantAt(drag.tl, Math.min(timeAtX(e.clientX, drag.tl.total), drag.tl.recoverStart));
      r.step = Math.min(inst.step, drag.tl.count - 1);
      r.at = inst.step > r.step ? 1 : r2(inst.at);
      host.seek(G.timeOf(timelineOf(g), r.step, r.at));
    } else if (drag.kind === 'fx') {
      const f = g.fx[drag.index];
      const inst = roundInstant(G.instantAt(drag.tl, timeAtX(e.clientX, drag.tl.total) - drag.grab));
      f.step = inst.step;
      f.at = inst.at;
      if (f.untilStep >= 0 && f.untilStep < f.step) f.untilStep = -1;
      host.seek(G.timeOf(timelineOf(g), f.step, f.at));
    }
    drag.moved = true;
    renderDock(dock.track.parentElement);
    host.redraw();
  });

  window.addEventListener('mouseup', () => {
    if (!drag) return;
    const moved = drag.moved;
    drag = null;
    if (moved) edited();
  });

  // ============================================================ the selected thing's fields

  function renderFields(root) {
    const g = gesture();
    if (!g) return;
    const s = selection();
    const box = el('div', 'gtFields');
    if (!s) {
      box.appendChild(el('p', 'hint', 'La línea de tiempo está bajo el lienzo. Elige en ella un paso (su pose y su tiempo), una ◆ suelta (cuándo y de dónde sale el ataque) o un ✨ efecto para editarlo aquí.'));
      root.appendChild(box);
      return;
    }
    if (s.kind === 'step') stepFields(box, g, s.index);
    else if (s.kind === 'release') releaseFields(box, g, s.index);
    else if (s.kind === 'fx') fxFields(box, g, s.index);
    else recoverFields(box, g);
    root.appendChild(box);
  }

  const stepOptions = (g, withRecover) => [
    ...g.steps.map((_, i) => ({ value: String(i), label: `${i + 1} · ${stepName(g, i)}` })),
    ...(withRecover ? [{ value: String(g.steps.length), label: '↩ Vuelta al reposo' }] : []),
  ];

  function stepFields(box, g, index) {
    const step = g.steps[index];
    const tl = timelineOf(g);
    box.appendChild(el('p', 'rigGroupTitle', `Paso ${index + 1} de ${g.steps.length}`));

    const name = F.textField({ label: 'Nombre', value: step.name, hint: 'Sólo para reconocerlo ("Carga", "Lanza", "Recoge"…).', onChange: () => {} });
    name.input.addEventListener('change', () => { step.name = name.input.value.trim(); edited(); });
    box.appendChild(name.row);

    const posing = host.posingStep() === index;
    const acts = el('div', 'rigActions');
    const pose = el('button', `small${posing ? ' active' : ' primary'}`, posing ? '■ Salir de posar' : '✋ Posar este paso');
    pose.title = 'La pose a la que llega el cuerpo al acabar este paso, medida desde el reposo: arrastra las piezas en el lienzo (Mover / Rotar / Escalar). Una pieza que no esté en la pose vuelve a su reposo.';
    pose.onclick = () => host.pose(posing ? -1 : index);
    acts.appendChild(pose);
    acts.appendChild(el('span', 'hint', `${step.poses.length} pieza(s) en la pose`));
    box.appendChild(acts);
    if (posing) host.renderPose(box, index);

    const first = tl.releases.reduce((a, b) => (!a || b.time < a.time ? b : a), null);
    const tooLate = step.stretch && first && index > first.step;
    box.appendChild(F.boolField({ label: 'Dura el aviso del ataque', value: !!step.stretch,
      hint: tooLate ? 'Sólo se estiran los pasos hasta la primera suelta: éste viene después y dura su propio tiempo.'
        : 'Su duración la pone el aviso (telegraph) del ataque que use el gesto, para que el mismo gesto sirva a ataques con avisos distintos. Con varios pasos así se lo reparten.',
      onChange: (v) => { step.stretch = v; edited(); } }).row);

    const grid = el('div', 'rigGestureGrid');
    grid.appendChild(F.numberField({ label: 'Duración (s)', value: step.duration, min: 0.01, step: '0.05',
      hint: step.stretch && !tooLate ? `Ahora la pone el aviso: ${secs(tl.durations[index])}.` : '',
      onChange: (v) => { step.duration = Math.max(0.01, v); refreshDock(); } }).row);
    grid.appendChild(F.enumField({ label: 'Cómo llega', value: step.ease, options: G.EASES.map((e) => ({ value: e, label: G.EASE_LABELS[e] })),
      onChange: (v) => { step.ease = v; host.redraw(); } }).row);
    grid.appendChild(F.numberField({ label: 'Temblor (°)', value: step.shake, step: '0.5',
      hint: 'Tiembla el cuerpo en el último 30% del paso. 0 = nada.', onChange: (v) => { step.shake = v; host.redraw(); } }).row);
    box.appendChild(grid);

    const more = el('div', 'rigActions');
    const left = el('button', 'small', '◀');
    left.title = 'Antes en el orden.';
    left.disabled = index === 0;
    left.onclick = () => reorder(g, index, index - 1);
    const right = el('button', 'small', '▶');
    right.title = 'Después en el orden.';
    right.disabled = index === g.steps.length - 1;
    right.onclick = () => reorder(g, index, index + 1);
    const dup = el('button', 'small', '⧉ Duplicar paso');
    dup.onclick = () => {
      const at = G.insertStep(g, index, JSON.parse(JSON.stringify(step)));
      sel = { kind: 'step', index: at };
      edited();
    };
    const del = el('button', 'small danger', '🗑 Quitar paso');
    del.disabled = g.steps.length <= 1;
    del.title = 'Sus sueltas y efectos pasan al paso anterior.';
    del.onclick = removeSelected;
    more.append(left, right, dup, del);
    box.appendChild(more);
  }

  function reorder(g, from, to) {
    if (!G.moveStep(g, from, to)) return;
    if (host.posingStep() >= 0) host.pose(-1);
    sel = { kind: 'step', index: to };
    host.seek(G.timeOf(timelineOf(g), to, 1));
    edited();
  }

  function recoverFields(box, g) {
    box.appendChild(el('p', 'rigGroupTitle', 'Vuelta al reposo'));
    box.appendChild(F.numberField({ label: 'Duración (s)', value: g.recover, min: 0.01, step: '0.05',
      hint: 'Lo que tarda el cuerpo en volver del último paso a su reposo. Los efectos puestos aquí salen durante la vuelta.',
      onChange: (v) => { g.recover = Math.max(0.01, v); refreshDock(); } }).row);
  }

  function releaseFields(box, g, index) {
    const r = g.releases[index];
    box.appendChild(el('p', 'rigGroupTitle', `◆ Suelta ${index + 1} de ${g.releases.length} — aquí sale el ataque`));

    const grid = el('div', 'rigGestureGrid');
    grid.appendChild(F.enumField({ label: 'En el paso', value: String(Math.min(r.step, g.steps.length - 1)), options: stepOptions(g, false),
      onChange: (v) => { r.step = parseInt(v, 10); seekTo(g, r.step, r.at); edited(); } }).row);
    grid.appendChild(F.numberField({ label: 'Momento (0–1)', value: r.at, min: 0, max: 1, step: '0.05',
      hint: '0 = al empezar el paso · 1 = al acabarlo.',
      onChange: (v) => { r.at = clamp01(v); seekTo(g, r.step, r.at); refreshDock(); } }).row);
    box.appendChild(grid);

    box.appendChild(F.enumField({ label: 'Sale de', value: r.point || '',
      options: [{ value: '', label: 'La boca del arma' }, ...host.parts().map((p) => ({ value: p.uid, label: `Pieza: ${p.name}` }))],
      hint: !r.point && !host.hasMuzzle() ? 'No hay boca puesta (herramienta ◉ Boca): saldrá de la pieza que apunta, o del centro del cuerpo.' : '',
      onChange: (v) => { r.point = v || null; r.offset = { x: 0, y: 0 }; edited(); } }).row);   // the old offset was measured on the other point

    placeButton(box, 'release', index, 'Clic en el lienzo: el ataque sale de ese punto (se guarda relativo a la pieza o a la boca). También se puede arrastrar la diana roja. Esc cancela.');
    box.appendChild(F.vector2Field({ label: 'Desplazamiento (u, desde ese punto)', value: r.offset,
      onChange: (v) => { r.offset = v; host.redraw(); } }).row);
    box.appendChild(el('p', 'hint', 'Con varias sueltas el ataque sale varias veces: el disparo propio dispara en cada una; un ataque de catálogo empieza en la primera (y una salva por suelta, si lo tiene activado).'));

    const del = el('button', 'small danger', '🗑 Quitar suelta');
    del.title = g.releases.length === 1 ? 'Sin sueltas escritas, el ataque sale una vez al acabar el último paso, desde la boca.' : '';
    del.onclick = removeSelected;
    box.appendChild(del);
  }

  function seekTo(g, step, at) { host.seek(G.timeOf(timelineOf(g), step, at)); }

  /** After a number that changes the layout of the track but not which fields are shown. */
  function refreshDock() {
    if (dock) renderDock(dock.track.parentElement);
    host.redraw();
  }

  function placeButton(box, kind, index, title) {
    const on = host.placing()?.kind === kind && host.placing().index === index;
    const place = el('button', `small${on ? ' active' : ''}`, on ? '⌖ Haz clic en el lienzo…' : '⌖ Colocar en el lienzo');
    place.title = title;
    place.onclick = () => host.place(kind, index);
    box.appendChild(place);
  }

  function fxFields(box, g, index) {
    const f = g.fx[index];
    const entries = host.fxEntries();
    const entry = entries.get(f.fx);
    box.appendChild(el('p', 'rigGroupTitle', `✨ Efecto: ${fxName(f)}`));
    if (f.prefab && !entry) box.appendChild(el('p', 'hint', `Prefab de Unity (${f.prefab}): se exporta tal cual; aquí no se puede ver. Elige uno de la biblioteca para sustituirlo.`));
    else if (!entry) box.appendChild(el('p', 'efSummaryError', 'Este efecto ya no está en la biblioteca: elige otro o quítalo.'));

    box.appendChild(F.enumField({ label: 'Efecto', value: entry ? f.fx : '',
      options: [...(entry ? [] : [{ value: '', label: f.prefab ? '(prefab de Unity)' : '(borrado)' }]), ...[...entries.values()].map((e) => ({ value: e.id, label: e.enemyName }))],
      onChange: (v) => { if (!v) return; f.fx = v; delete f.prefab; edited(); } }).row);

    const grid = el('div', 'rigGestureGrid');
    grid.appendChild(F.enumField({ label: 'Sale en', value: String(Math.min(f.step, g.steps.length)), options: stepOptions(g, true),
      onChange: (v) => { f.step = parseInt(v, 10); if (f.untilStep >= 0 && f.untilStep < f.step) f.untilStep = -1; seekTo(g, f.step, f.at); edited(); } }).row);
    grid.appendChild(F.numberField({ label: 'Momento (0–1)', value: f.at, min: 0, max: 1, step: '0.05',
      hint: '0 = al empezar · 1 = al acabar.', onChange: (v) => { f.at = clamp01(v); seekTo(g, f.step, f.at); refreshDock(); } }).row);
    box.appendChild(grid);

    box.appendChild(F.boolField({ label: 'En bucle', value: !!f.loop,
      hint: 'Encendido = una carga que dura hasta que acabe un paso. Apagado = un estallido que se reproduce una vez.',
      onChange: (v) => { f.loop = v; if (!v) f.untilStep = -1; edited(); } }).row);
    if (f.loop) {
      box.appendChild(F.enumField({ label: 'Se apaga al acabar', value: String(f.untilStep >= 0 ? Math.max(f.untilStep, f.step) : -1),
        options: [{ value: '-1', label: 'El paso en el que sale' }, ...stepOptions(g, true).filter((o) => parseInt(o.value, 10) > f.step)],
        onChange: (v) => { f.untilStep = parseInt(v, 10); edited(); } }).row);
    }

    box.appendChild(F.enumField({ label: 'Cuelga de', value: f.anchor === 'part' ? `part:${f.part}` : f.anchor,
      options: [...host.parts().map((p) => ({ value: `part:${p.uid}`, label: `Pieza: ${p.name}` })),
        { value: 'muzzle', label: 'Boca del arma' }, { value: 'body', label: 'Cuerpo entero' }, { value: 'feet', label: 'Pies (suelo)' }],
      hint: f.anchor === 'muzzle' && !host.hasMuzzle() ? 'No hay boca puesta (herramienta ◉ Boca): saldrá de la pieza que apunta.' : '',
      onChange: (v) => {
        if (v.startsWith('part:')) { f.anchor = 'part'; f.part = v.slice(5); } else { f.anchor = v; f.part = null; }
        f.offset = { x: 0, y: 0 };   // the old offset was measured in the other anchor's space
        edited();
      } }).row);
    placeButton(box, 'fx', index, 'Clic en el lienzo: el efecto se queda en ese punto de su ancla. También se puede arrastrar su cruz. Esc cancela.');

    const look = el('div', 'rigGestureGrid');
    look.appendChild(F.enumField({ label: 'Mira', value: f.facing || 'direction', options: FX.FX_FACINGS.map((k) => ({ value: k, label: FX.FX_FACING_LABELS[k] })),
      hint: 'Hacia donde va la acción: en la boca, hacia el disparo; en el cuerpo o los pies, hacia donde se mueve.',
      onChange: (v) => { f.facing = v; host.redraw(); } }).row);
    look.appendChild(F.numberField({ label: 'Giro extra (°)', value: f.angle || 0, step: '5', onChange: (v) => { f.angle = v; host.redraw(); } }).row);
    look.appendChild(F.enumField({ label: 'Capa', value: f.layer || 'front', options: [{ value: 'front', label: 'Delante del cuerpo' }, { value: 'behind', label: 'Detrás del cuerpo' }],
      onChange: (v) => { f.layer = v; host.redraw(); } }).row);
    box.appendChild(look);

    const more = el('details', 'efFold');
    more.open = fxMore;
    more.addEventListener('toggle', () => { fxMore = more.open; });
    more.appendChild(el('summary', null, 'Tamaño y ajuste fino'));
    more.appendChild(F.numberField({ label: 'Tamaño (× el del efecto)', value: f.scale, min: 0.01, step: '0.1',
      onChange: (v) => { f.scale = v > 0 ? v : 1; host.redraw(); } }).row);
    more.appendChild(F.vector2Field({ label: 'Desplazamiento (u, en su ancla)', value: f.offset,
      onChange: (v) => { f.offset = v; host.redraw(); } }).row);
    more.appendChild(F.boolField({ label: 'Sigue a su ancla mientras dura', value: f.followsPart !== false,
      hint: 'Apagado = se queda en el punto del mundo donde salió (un impacto en el suelo).',
      onChange: (v) => { f.followsPart = v; host.redraw(); } }).row);
    box.appendChild(more);

    const del = el('button', 'small danger', '🗑 Quitar efecto');
    del.onclick = removeSelected;
    box.appendChild(del);
  }

  return {
    renderDock, renderFields, updatePlayhead, select, reset, addFx, removeSelected,
    get selection() { return selection(); },
  };
}
