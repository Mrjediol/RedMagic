// modules/enemy-studio.js
// -----------------------------------------------------------------------------
// The 🧬 Enemy Studio tab: one workflow from loose part PNGs to a finished enemy, as steps.
//
// Step 3 (Efectos) is the library's effects as a palette: ＋ or drag one onto a gesture and it hangs
// at the instant its timeline's playhead is on (the hooks themselves are the composer's, rig-fx.js).
//
//   left   — the steps, in the order of the work (one open at a time; the later ones unlock once
//            the body is usable): 1 Cuerpo · 2 Plantilla · 3 Efectos · 4 Comportamiento · 5 Exportar
//   centre — the composer's canvas
//   right  — the inspector of what is selected; in step 4 (enemies) the behaviour form
//
// The body editor is the rig composer (rig-composer.js): it renders into hosts that live in this tab
// — the canvas and the inspector in place, the rest (document + parts, templates, state sprites,
// boss link, export) moved once under their step. The behaviour is the one Enemy Creator form
// (enemy-form.js), which the 🧩 tab also uses for sprite-sheet enemies: while this tab is visible
// its panels are BORROWED into step 4 (mount) and put back when another tab is shown (unmount).
//
// What the Studio adds on top: the step order and locks, and the glue that makes the two halves one
// enemy — opening step 4 saves the body and links the form to it; the form is saved when the step is
// left and before the body is saved or exported (the composer's hooks).
//
// Plan and what each later phase adds to these steps: docs/web-enemy-studio-plan.md.
//
// TWO DOORS (docs/plan-gesture-attack-editor.md): the same panel is also the 🎬 "Gestos y ataques" tab
// — `show('focus')` — for an enemy that already exists: no steps, no body building. The left column
// becomes a short panel (which enemy, its state sprites, save / partial export), the composer goes
// into focus mode (rig-composer.js ▸ setFocusMode: only gestures are edited) and the right column
// shows the gestures with, under them, the form's Ataques and Defensa sections. Same editors, same
// document: nothing is implemented twice.

import { listEntries, getEnemy } from './enemy-library.js';
import { kindOf } from './entry-kinds.js';

const STEPS = [
  { id: 'body', title: 'Cuerpo' },
  { id: 'template', title: 'Plantilla' },
  { id: 'effects', title: 'Efectos' },
  { id: 'behaviour', title: 'Comportamiento' },
  { id: 'export', title: 'Exportar' },
];

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

/**
 * @param {object} o
 * @param {HTMLElement} o.panel - #tab-studio
 * @param {HTMLElement} o.stepsEl / o.centerEl / o.rightEl - its three columns
 * @param {object} o.rig - initRigComposer's API
 * @param {object} o.enemy - initEnemyCreator's API
 * @param {object} o.nodes - the borrowed panels: rigBody, rigTemplate, rigCenter, rigMain, rigStates, rigLinks,
 *                           rigExport (the composer's hosts), enemyForm, enemyPreview (Enemy Creator)
 * @param {(tab: string) => void} o.openTab - switches to another tab of the app
 */
export function initEnemyStudio({ panel, stepsEl, centerEl, rightEl, rig, enemy, nodes, openTab }) {
  let active = 'body';
  let mode = 'full';             // 'full' = the workflow steps · 'focus' = the "Gestos y ataques" tab
  let mounted = false;
  let effects = [];              // library projectiles / fx (step 3)
  let behaviourNote = '';        // why the behaviour form is not on screen (step 4)
  let effectsGesture = null;     // the open gesture step 3's hint was written for

  // ============================================================ layout (built once)

  stepsEl.appendChild(el('h1', null, '🧬 Enemy Studio'));
  const stepEls = {};
  STEPS.forEach((step, i) => {
    const box = el('div', 'studioStep');
    const head = el('button', 'studioStepHead');
    const state = el('span', 'studioStepState');
    head.append(el('span', 'studioStepNum', String(i + 1)), el('span', 'studioStepTitle', step.title), state);
    head.onclick = () => open(step.id);
    const body = el('div', 'studioStepBody');
    box.append(head, body);
    stepsEl.appendChild(box);
    stepEls[step.id] = { box, head, state, body };
  });

  // Where each borrowed panel goes, and the Studio's own bits around them.
  const startHint = el('div', 'rigBlock');
  startHint.appendChild(el('p', 'hint', 'Suelta los PNG de las piezas en el lienzo, o empieza de una forma ya hecha.'));
  const startBtn = el('button', 'small', '🧬 Empezar desde una plantilla →');
  startBtn.onclick = () => open('template');
  startHint.appendChild(startBtn);
  stepEls.body.body.appendChild(startHint);

  const effectsBox = el('div');
  stepEls.effects.body.appendChild(effectsBox);

  const behaviourInfo = el('p', 'hint');
  stepEls.behaviour.body.appendChild(behaviourInfo);

  // --- the focus mode's left column: which enemy, its state sprites, save / export.
  const focusEl = el('div', 'studioFocus');
  focusEl.id = 'studioFocus';
  focusEl.appendChild(el('h1', null, '🎬 Gestos y ataques'));
  focusEl.appendChild(el('p', 'hint', 'Añadir o cambiar gestos y ataques de un enemigo que ya existe. El cuerpo no se toca: para eso, 🧬 Enemy Studio.'));
  const pickRow = el('div', 'rigBlock');
  const picker = document.createElement('select');
  picker.dataset.nodirty = '1';
  pickRow.appendChild(picker);
  const focusInfo = el('p', 'hint');
  pickRow.appendChild(focusInfo);
  const toStudio = el('button', 'small', '🧬 Abrirlo en Enemy Studio (cuerpo, comportamiento, export completo)');
  toStudio.onclick = () => openTab('studio');
  pickRow.appendChild(toStudio);
  focusEl.appendChild(pickRow);
  const focusStates = el('div');
  const focusExport = el('div');
  const focusPreview = el('div');
  focusEl.append(focusStates, focusExport, focusPreview);
  stepsEl.appendChild(focusEl);

  picker.onchange = async () => {
    if (!picker.value) return;
    await flushBehaviour(true);
    await rig.linkLibraryEntry(picker.value);
    await ensureLinked(false);
    gestureSignature = signatureOf(rig.getGestures());
    rig.refreshInspector();
    render();
  };

  /** The part-based enemies of the library, in the picker. */
  async function refreshPicker() {
    const rigs = (await listEntries('rig')).filter((e) => e.draft);
    const current = rig.getStatus().id;
    picker.innerHTML = '';
    picker.appendChild(Object.assign(el('option', null, rigs.length ? '— elige un enemigo —' : '(no hay enemigos por partes en la biblioteca)'), { value: '' }));
    rigs.forEach((e) => picker.appendChild(Object.assign(el('option', null, `${e.enemyName}${e.draft.kind === 'boss' ? ' (jefe)' : ''}`), { value: e.id })));
    picker.value = rigs.some((e) => e.id === current) ? current : '';
  }

  /** The hosts that change column with the mode: state sprites, export, the form's validation. */
  function placeHosts() {
    const focus = mode === 'focus';
    (focus ? focusStates : slots.rigStates).appendChild(nodes.rigStates);
    (focus ? focusExport : slots.rigExport).appendChild(nodes.rigExport);
    if (mounted) (focus ? focusPreview : slots.enemyPreview).appendChild(nodes.enemyPreview);
  }

  const slots = {
    rigBody: stepEls.body.body,
    rigTemplate: stepEls.template.body,
    rigStates: stepEls.behaviour.body,
    rigLinks: stepEls.behaviour.body,
    enemyPreview: stepEls.behaviour.body,
    rigExport: stepEls.export.body,
    rigCenter: centerEl,
    rigMain: rightEl,
    enemyForm: rightEl,
  };

  // ============================================================ borrowing the panels

  // The composer's step hosts go under their steps for good; only the Enemy Creator's panels come and go.
  ['rigBody', 'rigTemplate', 'rigStates', 'rigLinks', 'rigExport'].forEach((key) => slots[key].appendChild(nodes[key]));
  const order = ['enemyPreview', 'enemyForm'];
  let homes = null;

  function mount() {
    if (mounted) return;
    homes = order.map((key) => ({ node: nodes[key], parent: nodes[key].parentNode, next: nodes[key].nextSibling }));
    order.forEach((key) => slots[key].appendChild(nodes[key]));
    mounted = true;
    // While the Studio is on screen the form's attacks hang from the composer's LIVE gestures, and
    // the gesture card mirrors the cards of the attacks that use it.
    enemy.setGestureSource(() => rig.getGestures());
    enemy.setDeckMirror(() => rig.refreshInspector(), () => rig.refreshStage());
    gestureSignature = signatureOf(rig.getGestures());
  }

  function unmount() {
    if (!mounted) return;
    // Last one first: each `next` is then already back in place.
    [...homes].reverse().forEach(({ node, parent, next }) => parent.insertBefore(node, next && next.parentNode === parent ? next : null));
    nodes.enemyForm.hidden = false;
    nodes.enemyPreview.hidden = false;
    mounted = false;
    enemy.setGestureSource(null);
    enemy.setDeckMirror(null);
  }

  // ============================================================ gestures ⇄ attacks
  //
  // An attack is made with a gesture and launches something at each of its releases. The gestures
  // are the composer's, the attacks the behaviour form's: this is where the two meet.

  let gestureSignature = '';

  /** What of the gestures the attacks depend on: names and how many times each releases, and where. */
  function signatureOf(gestures) {
    return gestures.map((g) => `${g.name}:${(g.releases || []).map((r) => `${r.step}@${r.at}`).join(',')}:${g.steps.map((s) => s.name).join(',')}`).join('|');
  }

  /** The body form is editing THIS body (so its attacks are this enemy's). */
  const linked = (st = rig.getStatus()) => st.kind === 'enemy' && !!st.id && enemy.linkedId() === st.id;

  /** Points the form at this body's record. `save` = save the body first if it has no record yet. */
  async function ensureLinked(save) {
    const st = rig.getStatus();
    if (st.kind !== 'enemy') return false;
    let id = st.id;
    if (!id) {
      if (!save) return false;
      id = await rig.save();
      if (!id) return false;
    }
    if (enemy.linkedId() !== id) await enemy.linkLibraryEntry(id, st.name);
    return true;
  }

  rig.hooks.gestureAttacks = {
    render(host, gestureName, rerender) {
      const st = rig.getStatus();
      if (st.kind !== 'enemy') return;   // a boss takes its attacks from 👑 Boss Creator
      if (linked(st)) { enemy.renderGestureAttacks(host, gestureName, rerender); return; }
      const box = el('div', 'rigFxBox');
      box.appendChild(el('p', 'rigGroupTitle', '⚔ Ataques que usan este gesto'));
      const go = el('button', 'small', st.id ? 'Ver sus ataques' : '💾 Guardar el enemigo para darle ataques');
      go.title = 'Los ataques son del enemigo guardado en la biblioteca: cada uno se hace con un gesto y lanza algo en cada suelta.';
      go.onclick = async () => { if (await ensureLinked(true)) { rerender(); render(); } };
      box.appendChild(go);
      host.appendChild(box);
    },
    shots: (gestureName) => (linked() ? enemy.gestureShots(gestureName) : null),
  };

  // ============================================================ steps: locks and state

  function usable(st) { return !!st.name && st.hasArt && st.openRequired === 0; }

  function whyNotUsable(st) {
    if (!st.hasArt) return 'sin piezas';
    if (st.openRequired) return `faltan ${st.openRequired} huecos`;
    if (!st.name) return 'falta el nombre';
    return '';
  }

  function locked(id, st = rig.getStatus()) {
    return (id === 'effects' || id === 'behaviour' || id === 'export') && !usable(st);
  }

  function render() {
    const st = rig.getStatus();
    panel.dataset.mode = mode;
    panel.dataset.kind = st.kind;

    if (mode === 'focus') {
      // No steps: the gestures on the right, and under them the form's attacks (once it is on this enemy).
      const form = linked(st);
      nodes.rigMain.hidden = false;
      nodes.enemyForm.hidden = !form;
      nodes.enemyPreview.hidden = !form;
      focusInfo.textContent = !st.id
        ? (st.parts ? 'El enemigo en pantalla no está guardado: guárdalo desde Enemy Studio para darle ataques.' : 'Elige arriba el enemigo.')
        : st.kind === 'boss' ? `"${st.name}" es un jefe: aquí sus gestos; sus ataques, en 👑 Boss Creator.`
          : `"${st.name}": ${st.gestures} gesto(s). Abre uno a la derecha: su línea de tiempo aparece bajo el lienzo.`;
      return;
    }

    if (locked(active, st)) active = 'body';
    panel.dataset.step = active;

    const text = {
      body: usable(st) ? `${st.parts} piezas · ${st.gestures} gestos` : whyNotUsable(st),
      template: st.template || '',
      effects: st.gestureFx ? `${st.gestureFx} en gestos` : effects.length ? `${effects.length} en biblioteca` : '',
      behaviour: `${st.kind === 'boss' ? 'jefe' : st.behaviour ? 'configurado' : 'por defecto'}${st.stateSprites ? ` · ${st.stateSprites} sprites` : ''}`,
      export: st.id ? 'guardado' : 'sin guardar',
    };
    const done = { body: usable(st), template: !!st.template, effects: st.gestureFx > 0, behaviour: st.behaviour, export: !!st.id };
    // The hint of step 3 names the gesture open in the inspector: keep it current.
    if (active === 'effects' && effectsGesture !== st.openGesture) { effectsGesture = st.openGesture; refreshEffects(); }

    STEPS.forEach(({ id }) => {
      const s = stepEls[id];
      const isLocked = locked(id, st);
      s.box.className = `studioStep${id === active ? ' open' : ''}${isLocked ? ' locked' : ''}${done[id] && !isLocked ? ' done' : ''}`;
      s.head.title = isLocked ? `Primero el cuerpo (paso 1): ${whyNotUsable(st)}.` : '';
      s.state.textContent = isLocked ? '🔒' : text[id];
      s.body.hidden = id !== active;
    });

    startHint.hidden = st.parts > 0;

    // Right column: the behaviour form in step 4 (enemies, once linked to this body), else the inspector.
    const form = active === 'behaviour' && st.kind === 'enemy' && !!st.id && enemy.linkedId() === st.id;
    nodes.enemyForm.hidden = !form;
    nodes.rigMain.hidden = form;
    nodes.enemyPreview.hidden = !form;
    behaviourInfo.textContent = st.kind === 'boss'
      ? 'Un jefe toma sus ataques y fases de 👑 Boss Creator: enlázalo aquí abajo.'
      : form
        ? 'El formulario está a la derecha. Se guarda solo al salir de este paso, al guardar y al exportar.'
        : behaviourNote || 'Abriendo el comportamiento…';
  }

  // ============================================================ step 3: effects of the library

  async function refreshEffects() {
    effects = [...(await listEntries('projectile')), ...(await listEntries('fx'))];
    effectsBox.innerHTML = '';
    const st = rig.getStatus();
    effectsBox.appendChild(el('p', 'hint', st.openGesture
      ? `Gesto abierto: "${st.openGesture}". Pulsa ＋ para colgarlo en el instante donde está el cursor de su línea de tiempo (bajo el lienzo), o arrástralo a la línea de tiempo y suéltalo en el instante que quieras; luego colócalo en el lienzo.`
      : 'Abre un gesto a la derecha (Gestos): su línea de tiempo aparece bajo el lienzo. Después pulsa ＋ aquí, o arrastra el efecto a la línea de tiempo.'));
    const list = el('div', 'studioFxList');
    effects.forEach((entry) => {
      const row = el('div', 'studioFxRow');
      if (entry.thumbnail) { const img = el('img'); img.src = entry.thumbnail; img.alt = ''; row.appendChild(img); }
      else row.appendChild(el('span', 'studioFxEmpty'));
      row.appendChild(el('span', null, entry.enemyName));
      row.appendChild(el('span', 'kind', kindOf(entry.kind).label));
      row.draggable = true;
      row.addEventListener('dragstart', (e) => e.dataTransfer.setData('text/rig-fx', entry.id));
      const add = el('button', 'small', '＋');
      add.title = 'Colgarlo del gesto abierto, en el instante donde está el cursor de su línea de tiempo.';
      add.onclick = () => { rig.addFxToOpenGesture(entry.id); };
      row.appendChild(add);
      list.appendChild(row);
    });
    if (!effects.length) list.appendChild(el('p', 'hint', 'Todavía no hay ninguno.'));
    effectsBox.appendChild(list);
    const lab = el('button', 'small', '✨ Crear uno en VFX Lab');
    lab.onclick = () => openTab('vfx');
    effectsBox.appendChild(lab);
    render();
  }

  // ============================================================ step 4: the behaviour form on this body

  /** The form, if it is editing THIS body, saved onto its record (no download). */
  async function flushBehaviour(force = false) {
    if (!mounted && !force) return;   // on the old tabs the form is only saved by its own exports, as before
    const id = rig.getStatus().id;
    if (id && enemy.linkedId() === id) await enemy.persistLinked();
  }

  /** Saves the body (the form edits a library record) and points the form at it. */
  async function enterBehaviour() {
    const st = rig.getStatus();
    if (st.kind !== 'enemy') { render(); return; }
    behaviourNote = '';
    const id = await rig.save();   // afterSave → flushBehaviour: a form already on this body is kept
    if (!id) { behaviourNote = 'No se pudo guardar el cuerpo en la biblioteca: revisa el nombre en el paso 1.'; render(); return; }
    if (enemy.linkedId() !== id) await enemy.linkLibraryEntry(id, st.name);
    else { enemy.setEnemyName(st.name); await enemy.refreshArtLibrary(); }
    render();
  }

  async function open(id) {
    if (locked(id) || id === active) return;
    const leftBehaviour = active === 'behaviour';
    if (leftBehaviour) await flushBehaviour();
    active = id;
    if (leftBehaviour) await rig.refreshLibraries();   // the composer re-reads the behaviour just saved
    render();
    if (id === 'behaviour') await enterBehaviour();
    else if (id === 'effects') await refreshEffects();
  }

  // ============================================================ glue with the composer

  rig.hooks.beforeExport = () => flushBehaviour();
  rig.hooks.afterSave = () => flushBehaviour();
  rig.hooks.onChange = () => {
    if (!mounted) return;
    // A release added / moved / removed, a gesture renamed: the attacks' slots follow.
    const signature = signatureOf(rig.getGestures());
    if (signature !== gestureSignature) {
      gestureSignature = signature;
      if (linked()) enemy.gesturesChanged();
    }
    render();
  };

  /** `as` = 'full' (🧬 Enemy Studio, the steps) or 'focus' (🎬 Gestos y ataques). */
  async function show(as = 'full') {
    // From the behaviour step straight to the other door: what was typed there is saved first.
    if (mounted && as !== mode && active === 'behaviour') await flushBehaviour();
    mode = as;
    mount();
    placeHosts();
    rig.setFocusMode(mode === 'focus');
    if (mode === 'focus') await refreshPicker();
    rig.onShow();
    await ensureLinked(false);   // an enemy opened from the library: its attacks, beside its gestures
    render();
    if (mode === 'focus') return;
    if (active === 'behaviour') await enterBehaviour();
    else if (active === 'effects') await refreshEffects();
  }

  async function hide() {
    if (!mounted) return;
    // Panels go back at once (the tab being shown needs them now); the form is saved meanwhile —
    // whichever step is open: attacks are also edited from a gesture's card.
    unmount();
    await flushBehaviour(true);
  }

  /** Biblioteca ▸ "Editar en Enemy Studio" (a part-based enemy: open it) / a body template (a new enemy from it). */
  async function linkLibraryEntry(id) {
    const entry = await getEnemy(id);
    if (active === 'behaviour') await flushBehaviour();
    if (entry?.kind === 'rig-template') await rig.useTemplateRecord(id);
    else await rig.linkLibraryEntry(id);
    active = 'body';
    await ensureLinked(false);
    gestureSignature = signatureOf(rig.getGestures());
    rig.refreshInspector();
    render();
  }

  return { show, hide, open, linkLibraryEntry, isMounted: () => mounted };
}
