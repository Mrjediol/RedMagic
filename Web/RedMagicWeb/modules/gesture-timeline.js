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
//   leadNote()                (optional) whose warning it is — "the telegraph of attack X" — or null (a number
//                             only for the preview). setLead writes wherever it belongs.
//   hasAimPart()              the body has a part that aims (a step can end pointing it at the target)
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
//   attackUses()              (optional) an attack launches this gesture: only then is "no release written" shown
//                             as the attack leaving at the end (an idle / defence / transition gesture releases nothing)
//   soundClips()              (optional) what a gesture's sound can be: [{ value, label, group: 'Biblioteca' | 'Unity' }]
//   playClip(value, {gain, rate})  (optional) plays one of them
//   uploadSound()             (optional) asks for audio files, saves them in the library → their clip values
//
// Like a video editor: Ctrl+wheel (or − / +) zooms the track, ← / → move the playhead one frame (Shift: ten),
// Ctrl+← / → move what is selected, Ctrl+D duplicates it, Ctrl+B cuts the step under the playhead in two.
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
const r3 = (v) => Math.round(v * 1000) / 1000;
const FRAME = 1 / 60;      // what one arrow key press moves
const MAX_ZOOM = 40;
const clamp01 = (x) => Math.min(1, Math.max(0, x));
const secs = (t) => `${t.toFixed(2)} s`;

export function createGestureTimeline(host) {
  let sel = null;      // { kind: 'step' | 'release' | 'fx' | 'sound' | 'recover', index }
  let dock = null;     // the dock's live elements: { root, scroll, track, playhead, time, play, total }
  let zoom = 1;        // 1 = the whole gesture fits the dock; more = the track is wider and scrolls
  let scrollLeft = 0;  // kept across rebuilds of the dock
  let clipFilter = ''; // the sound panel's clip filter
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
    const list = { step: g.steps, release: g.releases, fx: g.fx || [], sound: g.sound ? [g.sound] : [] }[sel.kind];
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
      else if (kind === 'sound') host.seek(G.timeOf(tl, Math.min(g.sound.step, tl.count - 1), g.sound.at));
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

  /** The gesture's sound, at the playhead (the first release when the playhead is at rest). */
  function addSound() {
    const g = gesture();
    if (!g) return;
    if (!g.sound) {
      const tl = timelineOf(g);
      const inst = G.instantAt(tl, Math.min(host.time(), tl.recoverStart));
      const step = Math.min(inst.step, tl.count - 1);
      g.sound = G.createSound(step, host.time() <= 0 ? 0 : inst.step > step ? 1 : r2(inst.at));
      edited();
    }
    select('sound', 0);
    host.status('Sonido del gesto: a la izquierda, sube un archivo o elige un clip; arrástralo por la línea de tiempo al instante en que suena.');
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
    else if (s.kind === 'sound') { g.sound = null; sel = null; }
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
    const note = host.leadNote ? host.leadNote() : null;
    if (note) leadWrap.title = note;
    leadWrap.append(lead, ' s');
    bar.appendChild(leadWrap);

    bar.appendChild(el('span', 'gtSpacer'));
    const addS = el('button', 'small', '＋ Paso');
    addS.title = 'Un paso más: otra pose a la que llega el cuerpo. Se añade tras el paso elegido.';
    addS.onclick = addStep;
    const addR = el('button', 'small', '◆ Suelta');
    addR.title = 'Un instante en el que sale el ataque, donde está el cursor. Varias = dispara varias veces.';
    addR.onclick = addRelease;
    const addA = el('button', 'small', '🔊 Sonido');
    addA.title = g.sound ? 'El sonido de este gesto (está en su pista, abajo).' : 'El sonido propio del gesto, en el instante donde está el cursor.';
    addA.onclick = addSound;
    bar.append(addS, addR, addA);

    const entries = [...host.fxEntries().values()];
    const addF = document.createElement('select');
    addF.className = 'gtAddFx';
    addF.title = 'Cuelga un efecto de la biblioteca en el instante donde está el cursor. También se puede arrastrar desde Efectos a la línea de tiempo.';
    addF.appendChild(Object.assign(el('option', null, entries.length ? '✨ Efecto…' : '✨ (no hay efectos en la biblioteca)'), { value: '' }));
    entries.forEach((e) => addF.appendChild(Object.assign(el('option', null, e.enemyName), { value: e.id })));
    addF.disabled = !entries.length;
    addF.onchange = () => { if (addF.value) addFx(addF.value); };
    bar.appendChild(addF);
    const zoomOut = el('button', 'small', '−');
    zoomOut.dataset.nodirty = '1';
    zoomOut.title = 'Alejar la línea de tiempo. También Ctrl + rueda.';
    zoomOut.onclick = () => setZoom(zoom / 1.6);
    const zoomIn = el('button', 'small', '+');
    zoomIn.dataset.nodirty = '1';
    zoomIn.title = 'Acercar la línea de tiempo (hasta ver milisegundos). También Ctrl + rueda sobre ella.';
    zoomIn.onclick = () => setZoom(zoom * 1.6);
    const zoomFit = el('button', 'small', zoom > 1 ? `${zoom.toFixed(1)}×` : '1×');
    zoomFit.dataset.nodirty = '1';
    zoomFit.title = 'Zoom actual. Clic: ver el gesto entero.';
    zoomFit.onclick = () => setZoom(1);
    bar.append(zoomOut, zoomFit, zoomIn);
    root.appendChild(bar);

    // --- track (inside a scroller: zoomed in, it is wider than the dock)
    const scroll = el('div', 'gtScroll');
    scroll.addEventListener('scroll', () => { scrollLeft = scroll.scrollLeft; });
    scroll.addEventListener('wheel', (e) => {
      if (!e.ctrlKey) return;
      e.preventDefault();   // not the page's zoom
      setZoom(zoom * (e.deltaY < 0 ? 1.25 : 0.8), timeAtX(e.clientX, tl.total), e.clientX);
    }, { passive: false });
    const track = el('div', 'gtTrack');
    track.style.width = `${zoom * 100}%`;
    // What each lane is, in a narrow column at its left (it does not scroll with the track).
    const heads = el('div', 'gtHeads');
    const head = (cls, text, title, onClick = null) => {
      const h = el('div', `gtHead ${cls}`, text);
      h.title = title;
      if (onClick) { h.classList.add('clickable'); h.onclick = onClick; }
      heads.appendChild(h);
    };
    const timeAt = (clientX) => timeAtX(clientX, tl.total);

    // ruler: a tick every 0.1 / 0.25 / 0.5 / 1 s, whichever leaves room to read them
    const ruler = el('div', 'gtRuler');
    const every = [0.005, 0.01, 0.02, 0.05, 0.1, 0.25, 0.5, 1, 2, 5].find((k) => tl.total / k <= 12 * zoom) || 10;
    for (let t = 0; t <= tl.total + 1e-6; t += every) {
      const tick = el('span', 'gtTick', `${r3(t)}`);
      tick.style.left = pct(t);
      ruler.appendChild(tick);
    }
    ruler.title = 'Arrastra para parar el gesto en cualquier instante.';
    ruler.addEventListener('mousedown', (e) => { e.preventDefault(); drag = { kind: 'scrub' }; host.scrub(timeAt(e.clientX)); });
    track.appendChild(ruler);
    head('gtRuler', '⏱ Tiempo', 'Segundos del gesto. Arrastra sobre la regla para mover el cursor.');

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
      // Its right edge: drag = how long it lasts. A step that lasts the warning has no time of its own:
      // dragging it changes the warning (the attack's telegraph when an attack uses the gesture).
      const edge = el('span', 'gtEdge');
      edge.title = stretched ? 'Arrastra: el aviso del ataque (lo que dura este paso).' : 'Arrastra: cuánto dura el paso.';
      edge.addEventListener('mousedown', (e) => {
        e.preventDefault(); e.stopPropagation();
        const perPx = tl.total / Math.max(1, track.getBoundingClientRect().width);
        drag = stretched ? { kind: 'lead', x0: e.clientX, d0: host.lead(), perPx }
          : { kind: 'duration', target: step, key: 'duration', x0: e.clientX, d0: tl.durations[i], perPx };
      });
      block.appendChild(edge);
      if (step.aim) block.appendChild(el('span', 'gtAim', '🎯'));
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
    head('gtLane gtSteps', '▭ Pasos', 'Las poses del cuerpo, una tras otra, y la vuelta al reposo.');

    // releases
    const releases = el('div', 'gtLane gtReleases');
    releases.title = 'Sueltas: los instantes en que sale el ataque.';
    const launches = !host.attackUses || host.attackUses();
    tl.releases.forEach((r) => {
      const implicit = r.index < 0;
      // No release written: an attack would leave at the end — a gesture no attack uses releases nothing.
      if (implicit && !launches) return;
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
    head('gtLane gtReleases', '◆ Ataque', 'Salidas de ataque: los instantes en que el gesto suelta el ataque que lo usa. "◆ Suelta" añade una.', addRelease);

    // the gesture's sound: a marker at the instant it plays (the lane is always there: click it to add one)
    head('gtLane gtFxLane', '🔊 Sonido', g.sound ? 'El sonido propio del gesto: clic para editarlo.' : 'Este gesto no tiene sonido: clic para añadirlo (subiendo un archivo o eligiendo uno).', addSound);
    if (!g.sound) {
      const lane = el('div', 'gtLane gtFxLane gtSoundLane empty');
      lane.appendChild(el('span', 'gtLaneHint', '＋ añadir sonido'));
      lane.title = 'Clic: añade el sonido del gesto en el instante donde está el cursor.';
      lane.onclick = addSound;
      track.appendChild(lane);
    } else {
      const a = g.sound;
      const when = G.timeOf(tl, Math.min(a.step, tl.count - 1), a.at);
      const lane = el('div', 'gtLane gtFxLane gtSoundLane');
      const mark = el('div', `gtFx gtSound${s?.kind === 'sound' ? ' selected' : ''}${a.clips.length ? '' : ' missing'}`);
      mark.style.left = pct(when);
      mark.appendChild(el('span', null, `🔊 ${a.clips.length ? clipLabel(a.clips[0]) + (a.clips.length > 1 ? ` +${a.clips.length - 1}` : '') : '(sin clip)'}`));
      mark.title = `El sonido del gesto — suena a los ${secs(when)}. Arrástralo para moverlo.`;
      mark.addEventListener('mousedown', (e) => {
        e.preventDefault(); e.stopPropagation();
        sel = { kind: 'sound', index: 0 };
        if (host.posingStep() >= 0) host.pose(-1);
        drag = { kind: 'sound', tl, grab: timeAt(e.clientX) - when, moved: false };
        host.seek(when);
        host.refresh();
      });
      lane.appendChild(mark);
      track.appendChild(lane);
    }

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
      head('gtLane gtFxLane', (g.fx || []).length > 1 ? `✨ VFX ${i + 1}` : '✨ VFX', `Efecto: ${fxName(f)}. Clic para editarlo.`, () => select('fx', i));
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
    scroll.appendChild(track);
    const body = el('div', 'gtBody');
    body.append(heads, scroll);
    root.appendChild(body);
    scroll.scrollLeft = scrollLeft;

    dock = { root, scroll, track, playhead, time, play, total: tl.total };
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
    dock.time.textContent = `${t.toFixed(zoom > 3 ? 3 : 2)} / ${dock.total.toFixed(2)} s`;
    // Zoomed in and playing: the track follows the playhead.
    if (zoom > 1 && host.playing()) {
      const x = (t / dock.total) * dock.track.clientWidth, view = dock.scroll.clientWidth;
      if (x < dock.scroll.scrollLeft || x > dock.scroll.scrollLeft + view - 20) { dock.scroll.scrollLeft = Math.max(0, x - view * 0.2); scrollLeft = dock.scroll.scrollLeft; }
    }
    dock.play.textContent = host.playing() ? '⏸ Pausa' : '▶ Probar';
    dock.play.classList.toggle('active', host.playing());
  }

  window.addEventListener('mousemove', (e) => {
    if (!drag) return;
    const g = gesture();
    if (!g || !dock) { drag = null; return; }
    if (drag.kind === 'scrub') { host.scrub(timeAtX(e.clientX, dock.total)); return; }

    if (drag.kind === 'duration') {
      drag.target[drag.key] = Math.max(0.01, (zoom > 2 ? r3 : r2)(drag.d0 + (e.clientX - drag.x0) * drag.perPx));
    } else if (drag.kind === 'lead') {
      host.setLead(Math.max(0.05, r2(drag.d0 + (e.clientX - drag.x0) * drag.perPx)));
    } else if (drag.kind === 'release') {
      // On the timeline as it was when the drag began: moving the first release restretches the steps under it.
      const r = g.releases[drag.index];
      const inst = G.instantAt(drag.tl, Math.min(timeAtX(e.clientX, drag.tl.total), drag.tl.recoverStart));
      r.step = Math.min(inst.step, drag.tl.count - 1);
      r.at = inst.step > r.step ? 1 : (zoom > 2 ? r3 : r2)(inst.at);
      host.seek(G.timeOf(timelineOf(g), r.step, r.at));
    } else if (drag.kind === 'fx') {
      const f = g.fx[drag.index];
      const raw = G.instantAt(drag.tl, timeAtX(e.clientX, drag.tl.total) - drag.grab);
      const inst = zoom > 2 ? { step: raw.step, at: r3(raw.at) } : roundInstant(raw);
      f.step = inst.step;
      f.at = inst.at;
      if (f.untilStep >= 0 && f.untilStep < f.step) f.untilStep = -1;
      host.seek(G.timeOf(timelineOf(g), f.step, f.at));
    } else if (drag.kind === 'sound') {
      const a = g.sound;
      const inst = G.instantAt(drag.tl, Math.min(timeAtX(e.clientX, drag.tl.total) - drag.grab, drag.tl.recoverStart));
      a.step = Math.min(inst.step, drag.tl.count - 1);
      a.at = inst.step > a.step ? 1 : r3(inst.at);
      host.seek(G.timeOf(timelineOf(g), a.step, a.at));
    }
    drag.moved = true;
    renderDock(dock.root);
    host.redraw();
  });

  window.addEventListener('mouseup', () => {
    if (!drag) return;
    const moved = drag.moved;
    drag = null;
    if (moved) edited();
  });

  // ============================================================ duplicate / split / nudge / zoom (video-editor style)

  /** Ctrl+D: a copy of what is selected — a step right after itself, an effect or a release at the same instant. */
  function duplicateSelected() {
    const g = gesture();
    const s = selection();
    if (!g || !s) { host.status('Duplicar: elige antes algo en la línea de tiempo.'); return; }
    if (s.kind === 'step') {
      const at = G.insertStep(g, s.index, JSON.parse(JSON.stringify(g.steps[s.index])));
      sel = { kind: 'step', index: at };
    } else if (s.kind === 'fx') {
      g.fx.splice(s.index + 1, 0, JSON.parse(JSON.stringify(g.fx[s.index])));
      sel = { kind: 'fx', index: s.index + 1 };
    } else if (s.kind === 'release') {
      g.releases.splice(s.index + 1, 0, JSON.parse(JSON.stringify(g.releases[s.index])));
      sel = { kind: 'release', index: s.index + 1 };
    } else { host.status('Eso no se duplica (un gesto tiene un solo sonido y una sola vuelta).'); return; }
    edited();
    host.status('Duplicado. Arrástralo (o Ctrl+← / →) para separarlo del original.');
  }

  /** Ctrl+B: cuts the step under the playhead in two at that instant; the first half ends in the pose the body has there. */
  function splitAtPlayhead() {
    const g = gesture();
    if (!g) return;
    const tl = timelineOf(g);
    const inst = G.instantAt(tl, host.time());
    if (inst.step >= tl.count) { host.status('Dividir: lleva el cursor a un paso (la vuelta al reposo no se divide).'); return; }
    if (!G.splitStep(g, inst.step, inst.at)) { host.status('Dividir: el cursor está en el borde del paso; llévalo a mitad.'); return; }
    if (host.posingStep() >= 0) host.pose(-1);
    sel = { kind: 'step', index: inst.step };
    edited();
    host.status(`"${stepName(g, inst.step)}" dividido en dos en ese instante: la primera mitad acaba en la pose que tenía ahí.`);
  }

  /** Ctrl+← / →: what is selected, `dt` seconds along the clock (a step or the way back: its duration). */
  function nudgeSelected(dt) {
    const g = gesture();
    const s = selection();
    if (!g || !s) return false;
    const tl = timelineOf(g);
    if (s.kind === 'step') {
      const step = g.steps[s.index];
      const first = tl.releases.reduce((a, b) => (!a || b.time < a.time ? b : a), null);
      if (step.stretch && (!first || s.index <= first.step)) host.setLead(Math.max(0.05, host.lead() + dt));
      else step.duration = Math.max(0.01, r3(tl.durations[s.index] + dt));
    } else if (s.kind === 'recover') g.recover = Math.max(0.01, r3(g.recover + dt));
    else {
      const item = s.kind === 'release' ? g.releases[s.index] : s.kind === 'fx' ? g.fx[s.index] : g.sound;
      const limit = s.kind === 'fx' ? tl.total : tl.recoverStart;
      const inst = G.instantAt(tl, Math.min(limit, Math.max(0, G.timeOf(tl, Math.min(item.step, s.kind === 'fx' ? tl.count : tl.count - 1), item.at) + dt)));
      item.step = s.kind === 'fx' ? inst.step : Math.min(inst.step, tl.count - 1);
      item.at = inst.step > item.step ? 1 : r3(inst.at);
      host.seek(G.timeOf(timelineOf(g), item.step, item.at));
    }
    edited();
    return true;
  }

  /** Zoom of the track (1 = the whole gesture fits), keeping the instant `anchorT` under screen x `anchorX`. */
  function setZoom(next, anchorT = null, anchorX = null) {
    zoom = Math.min(MAX_ZOOM, Math.max(1, next));
    if (!dock) return;
    const root = dock.root;
    const t = anchorT ?? host.time();
    renderDock(root);
    const view = dock.scroll.getBoundingClientRect();
    const x = (t / dock.total) * dock.track.getBoundingClientRect().width;
    dock.scroll.scrollLeft = x - (anchorX !== null ? anchorX - view.left : view.width / 2);
    scrollLeft = dock.scroll.scrollLeft;
  }

  window.addEventListener('keydown', (e) => {
    const g = gesture();
    if (!g || !dock || !dock.track.isConnected || dock.track.offsetParent === null) return;
    if (e.target instanceof Element && e.target.closest('input, textarea, select, [contenteditable]')) return;
    if (e.altKey || e.metaKey) return;
    const tl = timelineOf(g);
    const key = e.key;
    if (key === 'ArrowLeft' || key === 'ArrowRight') {
      // One frame (1/60 s), ten with Shift. Ctrl moves what is selected instead of the playhead.
      const dt = (key === 'ArrowRight' ? 1 : -1) * (e.shiftKey ? 10 : 1) * FRAME;
      e.preventDefault();
      if (e.ctrlKey) nudgeSelected(dt);
      else host.scrub(Math.min(tl.total, Math.max(0, host.time() + dt)));
      return;
    }
    if (key === 'Home' && !e.ctrlKey) { e.preventDefault(); host.scrub(0); return; }
    if (key === 'End' && !e.ctrlKey) { e.preventDefault(); host.scrub(tl.total); return; }
    if (!e.ctrlKey || e.shiftKey) return;
    if (key.toLowerCase() === 'd') { e.preventDefault(); duplicateSelected(); }
    else if (key.toLowerCase() === 'b') { e.preventDefault(); splitAtPlayhead(); }
  });

  // ============================================================ the selected thing's fields

  function renderFields(root) {
    const g = gesture();
    if (!g) return;
    const s = selection();
    const box = el('div', 'gtFields');
    if (!s) {
      box.appendChild(el('p', 'hint', 'Elige algo en la línea de tiempo (bajo el lienzo) y sus campos salen aquí: un paso (su pose y su tiempo), una ◆ salida de ataque, un ✨ efecto o el 🔊 sonido.'));
      box.appendChild(el('p', 'hint', '← / → mueven el cursor un frame (Mayús: diez) · Ctrl+← / → alargan o acortan el paso elegido (o mueven la salida, el efecto o el sonido elegido) · Ctrl+D lo duplica · Ctrl+B divide el paso bajo el cursor · Supr lo quita · Ctrl+rueda = zoom.'));
      root.appendChild(box);
      return;
    }
    if (s.kind === 'step') stepFields(box, g, s.index);
    else if (s.kind === 'release') releaseFields(box, g, s.index);
    else if (s.kind === 'fx') fxFields(box, g, s.index);
    else if (s.kind === 'sound') soundFields(box, g);
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
    if (step.stretch && !tooLate) {
      // It has no time of its own: what is typed here IS the warning (the attack's telegraph).
      const note = host.leadNote ? host.leadNote() : null;
      grid.appendChild(F.numberField({ label: 'Aviso (s)', value: host.lead(), min: 0.05, step: '0.05',
        hint: `${note || 'Sólo para probar: en el juego es el aviso (telegraph) del ataque que use el gesto.'} Este paso dura ${secs(tl.durations[index])}. Para darle un tiempo fijo, desmarca "Dura el aviso del ataque".`,
        onChange: (v) => { host.setLead(v > 0 ? Math.max(0.05, v) : 0.6); refreshDock(); } }).row);
    } else {
      grid.appendChild(F.numberField({ label: 'Duración (s)', value: step.duration, min: 0.01, step: '0.05',
        onChange: (v) => { step.duration = Math.max(0.01, v); refreshDock(); } }).row);
    }
    grid.appendChild(F.enumField({ label: 'Cómo llega', value: step.ease, options: G.EASES.map((e) => ({ value: e, label: G.EASE_LABELS[e] })),
      onChange: (v) => { step.ease = v; host.redraw(); } }).row);
    grid.appendChild(F.numberField({ label: 'Temblor (°)', value: step.shake, step: '0.5',
      hint: 'Tiembla el cuerpo en el último 30% del paso. 0 = nada.', onChange: (v) => { step.shake = v; host.redraw(); } }).row);
    box.appendChild(grid);

    box.appendChild(F.boolField({ label: '🎯 Acaba apuntando al objetivo', value: !!step.aim,
      hint: host.hasAimPart && !host.hasAimPart()
        ? 'Este cuerpo no tiene pieza que apunte (inspector ▸ Roles ▸ "Apunta al objetivo"): no hará nada.'
        : 'Al llegar a esta pose la línea pivote → boca del arma pasa por el objetivo (el 🎯 del lienzo; en el juego, el jugador), esté donde esté. Manda sobre el giro que tenga el arma en la pose. Márcalo también en el paso siguiente si debe seguir apuntando al disparar.',
      onChange: (v) => { step.aim = v; edited(); } }).row);

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
    box.appendChild(el('p', 'rigGroupTitle', `◆ Salida de ataque ${index + 1} de ${g.releases.length}`));

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
    del.title = g.releases.length === 1 ? 'Sin sueltas, el gesto no suelta nada por sí mismo; si un ataque lo usa, sale una vez al acabar el último paso, desde la boca.' : '';
    del.onclick = removeSelected;
    box.appendChild(del);
  }

  const clipName = (path) => (path || '').split('/').pop().replace(/\.(wav|ogg|mp3)$/i, '');
  /** A clip as the user knows it: a library sound by its name, a project clip by its file name. */
  const clipLabel = (value) => (host.soundClips ? host.soundClips() : []).find((c) => c.value === value)?.label.split('  ·  ')[0] || clipName(value);

  /** The gesture's own sound: which clips (one is picked at random), how loud, what pitch, and WHEN. */
  function soundFields(box, g) {
    const s = g.sound;
    // What a clip can be: a sound of the library (uploaded here) or a clip of the Unity project (the bridge).
    const catalog = host.soundClips ? host.soundClips() : [];
    const labelOf = (value) => catalog.find((c) => c.value === value)?.label || clipName(value);
    box.appendChild(el('p', 'rigGroupTitle', '🔊 Sonido del gesto'));

    const filter = document.createElement('input');
    filter.type = 'search';
    filter.placeholder = 'Filtrar clips por nombre…';
    filter.value = clipFilter;
    filter.dataset.nodirty = '1';
    filter.onchange = () => { clipFilter = filter.value.trim().toLowerCase(); host.refresh(); };
    if (catalog.length > 12) box.appendChild(filter);

    const shown = catalog.filter((c) => !clipFilter || c.label.toLowerCase().includes(clipFilter));
    const fill = (select, current) => {
      if (current && !catalog.some((c) => c.value === current)) select.appendChild(Object.assign(el('option', null, `${clipName(current)} (no disponible aquí)`), { value: current }));
      ['Biblioteca', 'Unity'].forEach((group) => {
        // The one in use always stays in its list, whatever the filter says.
        const list = catalog.filter((c) => c.group === group && (c.value === current || shown.includes(c)));
        if (!list.length) return;
        const og = document.createElement('optgroup');
        og.label = group === 'Unity' ? 'Proyecto de Unity' : 'Biblioteca (subidos aquí)';
        list.forEach((c) => og.appendChild(Object.assign(el('option', null, c.label), { value: c.value })));
        select.appendChild(og);
      });
    };

    s.clips.forEach((clip, i) => {
      const row = el('div', 'gtClipRow');
      const pick = document.createElement('select');
      fill(pick, clip);
      pick.value = clip;
      pick.title = labelOf(clip);
      pick.onchange = () => { s.clips[i] = pick.value; edited(); if (host.playClip) host.playClip(pick.value, { gain: s.volume, rate: s.pitchMin }); };
      const hear = el('button', 'small', '▶');
      hear.dataset.nodirty = '1';
      hear.title = 'Oír este clip, con su volumen y su tono.';
      hear.onclick = () => host.playClip && host.playClip(clip, { gain: s.volume, rate: s.pitchMin + Math.random() * (s.pitchMax - s.pitchMin) });
      const drop = el('button', 'small danger', '✕');
      drop.title = 'Quitar esta variante.';
      drop.onclick = () => { s.clips.splice(i, 1); edited(); };
      row.append(pick, hear, drop);
      box.appendChild(row);
    });
    if (!s.clips.length) box.appendChild(el('p', 'efSummaryError', 'Sin clip todavía: sube un archivo o elige uno.'));

    const acts = el('div', 'rigActions');
    const upload = el('button', 'small primary', '⬆ Subir archivo…');
    upload.title = 'Un wav / ogg / mp3 de tu ordenador: se guarda en la biblioteca (📚 ▸ Sonidos) y se usa aquí. Viaja a Unity al guardar o exportar.';
    upload.onclick = async () => {
      const values = host.uploadSound ? await host.uploadSound() : [];
      if (!values.length) return;
      s.clips.push(...values);
      edited();
      if (host.playClip) host.playClip(values[0], { gain: s.volume, rate: s.pitchMin });
    };
    const add = el('button', 'small', s.clips.length ? '＋ Otra variante' : '＋ Elegir uno existente');
    add.title = 'De la biblioteca o del proyecto de Unity. Con varias variantes se elige una al azar cada vez que suena.';
    add.disabled = !shown.length;
    add.onclick = () => { s.clips.push(shown[0].value); edited(); };
    acts.append(upload, add);
    box.appendChild(acts);
    if (!catalog.some((c) => c.group === 'Unity')) box.appendChild(el('p', 'hint', 'Los clips del proyecto de Unity sólo aparecen (y se oyen) si abres el enemigo desde Unity con "Editar en la web".'));

    const grid = el('div', 'rigGestureGrid');
    grid.appendChild(F.enumField({ label: 'Suena en', value: String(Math.min(s.step, g.steps.length - 1)), options: stepOptions(g, false),
      onChange: (v) => { s.step = parseInt(v, 10); seekTo(g, s.step, s.at); edited(); } }).row);
    grid.appendChild(F.numberField({ label: 'Momento (0–1)', value: s.at, min: 0, max: 1, step: '0.05',
      hint: '0 = al empezar el paso · 1 = al acabarlo. También se arrastra en la línea de tiempo.',
      onChange: (v) => { s.at = clamp01(v); seekTo(g, s.step, s.at); refreshDock(); } }).row);
    grid.appendChild(F.numberField({ label: 'Volumen (0–1)', value: s.volume, min: 0, max: 1, step: '0.05',
      onChange: (v) => { s.volume = clamp01(v); host.redraw(); } }).row);
    grid.appendChild(F.numberField({ label: 'Tono mínimo', value: s.pitchMin, min: 0.1, max: 3, step: '0.05',
      hint: '1 = como está grabado. Entre mínimo y máximo se elige uno al azar cada vez.',
      onChange: (v) => { s.pitchMin = Math.min(3, Math.max(0.1, v)); if (s.pitchMax < s.pitchMin) s.pitchMax = s.pitchMin; host.redraw(); } }).row);
    grid.appendChild(F.numberField({ label: 'Tono máximo', value: s.pitchMax, min: 0.1, max: 3, step: '0.05',
      onChange: (v) => { s.pitchMax = Math.min(3, Math.max(s.pitchMin, v)); host.redraw(); } }).row);
    box.appendChild(grid);
    box.appendChild(el('p', 'hint', 'Un clip que no sonaba antes en el juego hay que puntuarlo en Unity: Biblioteca Jere ▸ Sounds.'));

    const del = el('button', 'small danger', '🗑 Quitar el sonido');
    del.onclick = removeSelected;
    box.appendChild(del);
  }

  function seekTo(g, step, at) { host.seek(G.timeOf(timelineOf(g), step, at)); }

  /** After a number that changes the layout of the track but not which fields are shown. */
  function refreshDock() {
    if (dock) renderDock(dock.root);
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
    renderDock, renderFields, updatePlayhead, select, reset, addFx, removeSelected, duplicateSelected, splitAtPlayhead,
    get selection() { return selection(); },
  };
}
