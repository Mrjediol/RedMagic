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
//
// THE UNITY BRIDGE (docs/plan-unity-web-bridge.md): with the Unity editor open the page connects by itself
// (unity-bridge.js ▸ connect). The focused tab then lists the project's part-based prefabs and opens one
// FROM the project, with "💾 Guardar en Unity" (gestures and attacks); Enemy Studio's export step saves the
// WHOLE enemy into Unity (a new prefab, or the existing one) — no .zip to export and import. When Unity
// opens the page (`?bridge=…`) it goes straight to that enemy. Without Unity, only the .zip buttons.

import { listEntries, getEnemy, saveEnemy, updateEnemy } from './enemy-library.js';
import { kindOf } from './entry-kinds.js';
import { bridge, connect, onBridgeChange, listEnemies, fetchEnemy, fetchFile, fetchDataURL, fetchSounds, saveGestures, saveRig, BridgeError } from './unity-bridge.js';
import { readRigDocument } from './rig-export.js';
import { decodeAudio, playBuffer } from './vfx-stage.js';

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
  const focusSelection = el('div');
  focusEl.appendChild(focusSelection);
  const fullSelection = el('div');
  stepsEl.appendChild(fullSelection);
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
    (focus ? focusSelection : fullSelection).appendChild(rig.selectionPanel);
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
    setTelegraph: (gestureName, seconds) => enemy.setGestureTelegraph(gestureName, seconds),
  };

  // ============================================================ the Unity bridge (opened from Unity)
  //
  // docs/plan-unity-web-bridge.md. Unity opens this tab with an enemy of the PROJECT: what the prefab
  // has (gestures, state sprites, the deck with its attacks' real numbers, the sound of each gesture) is
  // what is edited, and "Guardar en Unity" writes it back — no export / import. The body is drawn from
  // the library record of that name when the enemy was made here, else read from Unity (read-only).

  const unity = {
    id: null,          // the library record the enemy on screen was loaded into
    prefab: '', name: '', kind: 'enemy',
    revision: '',      // what Unity had when it was read: sent back on save (409 if it changed since)
    busy: '',          // 'Leyendo de Unity…' / 'Guardando en Unity…'
    result: null,      // { ok, summary, warnings[], errors[] } | { error }
    conflict: null,    // { message } — "Cambió en Unity": reload or overwrite
    notes: [],         // what the readers had to say when it was opened
    clips: null,       // the project's audio clips (asset paths): what a gesture's sound can be
    buffers: new Map(),// clip path -> Promise<AudioBuffer | null>
    prefabs: null,     // the project's part-based prefabs [{ prefab, name, kind }]: what can be opened
    exists: null,      // a whole save that found a prefab of that name already there: { message, prefab }
  };

  /** Unity came or went: what it offers (its prefabs, its clips) is read again, and the panels redrawn. */
  async function refreshUnity() {
    if (bridge.active) {
      [unity.prefabs, unity.clips] = await Promise.all([listEnemies().catch(() => null), fetchSounds().catch(() => null)]);
    }
    rig.refreshInspector();
    render();
  }
  onBridgeChange(refreshUnity);
  if (bridge.linked) refreshUnity(); else connect();

  /** "Abrir un prefab de Unity…": the project's part-based prefabs, enemies then bosses. */
  function prefabPicker(current) {
    const pick = document.createElement('select');
    pick.dataset.nodirty = '1';
    pick.appendChild(Object.assign(el('option', null, unity.prefabs?.length ? '🔌 Abrir un prefab de Unity…' : '(Unity no tiene enemigos por partes)'), { value: '' }));
    ['enemy', 'boss'].forEach((kind) => {
      const list = (unity.prefabs || []).filter((p) => p.kind === kind);
      if (!list.length) return;
      const og = document.createElement('optgroup');
      og.label = kind === 'boss' ? 'Jefes' : 'Enemigos';
      list.forEach((p) => og.appendChild(Object.assign(el('option', null, p.name), { value: p.prefab, title: p.prefab })));
      pick.appendChild(og);
    });
    pick.value = current && unity.prefabs?.some((p) => p.prefab === current) ? current : '';
    pick.disabled = !!unity.busy;
    pick.title = 'Lee el prefab guardado en Unity (gestos, ataques, sonidos) para editarlo aquí y guardarlo de vuelta.';
    pick.onchange = () => { if (pick.value && pick.value !== current) openFromUnity(pick.value, {}); };
    return pick;
  }

  /** Not connected: why, and a way to try again. */
  function connectRow(host) {
    const row = el('div', 'unityBridge');
    row.appendChild(el('p', 'hint', '🔌 Unity no está conectado. Con el editor de Unity abierto en el proyecto se conecta solo (y "Guardar en Unity" sustituye al .zip).'));
    const again = el('button', 'small', '↻ Conectar con Unity');
    again.onclick = () => connect({ force: true });
    row.appendChild(again);
    host.appendChild(row);
  }

  /**
   * Enemy Studio ▸ Exportar ▸ "💾 Guardar en Unity": the WHOLE enemy (body included) into the project — a
   * new prefab, or the one it already is. `force` = the user confirmed replacing what Unity has.
   */
  async function saveWholeToUnity(force = false) {
    const st = rig.getStatus();
    if (unity.busy || !st.name) return;
    unity.busy = 'Guardando en Unity…';
    unity.result = null;
    unity.conflict = null;
    unity.exists = null;
    rig.refreshInspector();
    try {
      const blob = await rig.buildRigZip();   // saves the behaviour form first (hooks.beforeExport)
      if (!blob) return;
      // The prefab this record already is in Unity (opened from there or saved before): written in place.
      const known = st.id && unity.id === st.id ? unity : null;
      const res = await saveRig(blob, { prefab: known?.prefab || '', revision: known?.revision || '', force });
      unity.result = res;
      if (res.ok) {
        const id = await rig.save();
        Object.assign(unity, { id: id || st.id, prefab: res.prefab, name: st.name, kind: st.kind, revision: res.revision, notes: [] });
        unity.prefabs = await listEnemies().catch(() => unity.prefabs);
      }
      rig.setStatus(res.ok ? `${res.created ? 'Creado' : 'Actualizado'} en Unity: ${res.prefab}` : 'Unity no pudo aplicarlo: mira el informe.');
    } catch (e) {
      if (e instanceof BridgeError && e.status === 409 && e.reason === 'exists') unity.exists = { message: e.message, prefab: e.data?.prefab || '' };
      else if (e instanceof BridgeError && e.status === 409 && e.reason !== 'playmode') unity.conflict = { message: e.message };
      else unity.result = { error: e.message };
    } finally {
      unity.busy = '';
      rig.refreshInspector();
    }
  }

  /** Unity's answer to the last save: ✓ / ✗ and the importer's report. */
  function resultBlock(box) {
    const r = unity.result;
    if (r?.error) box.appendChild(el('p', 'efSummaryError', `✗ ${r.error}`));
    else if (r) {
      box.appendChild(el('p', r.ok ? 'efSummaryOk' : 'efSummaryError', r.ok ? `✓ Guardado en Unity${r.prefab ? `: ${r.prefab}` : ''}` : '✗ Unity no pudo aplicarlo'));
      box.appendChild(el('pre', 'unityReport', r.summary || ''));
    }
  }

  /** Enemy Studio's export step: "💾 Guardar en Unity" above the .zip buttons. */
  rig.hooks.exportExtras = (host) => {
    if (!bridge.active) { connectRow(host); return; }
    const st = rig.getStatus();
    const box = el('div', 'unityBridge');
    const known = st.id && unity.id === st.id;
    box.appendChild(el('p', 'unityBanner', `🔌 Unity${bridge.project ? ` · ${bridge.project}` : ''}`));
    if (st.unityBody) {
      box.appendChild(el('p', 'hint', 'Este cuerpo se leyó de Unity: aquí sólo se guardan sus gestos y ataques (🎬 Gestos y ataques ▸ Guardar en Unity).'));
      host.appendChild(box);
      return;
    }
    const save = el('button', 'primary', unity.busy || '💾 Guardar en Unity');
    save.style.width = '100%';
    save.disabled = !!unity.busy || !st.name || !st.parts;
    save.title = known
      ? `Actualiza ${unity.prefab}: cuerpo, gestos, sonidos, ataques. Lo afinado a mano en el Inspector se conserva.`
      : 'Crea el prefab en Unity (o lo actualiza si ya hay uno con este nombre: te lo pregunta). Sin exportar ni importar el .zip.';
    save.onclick = () => saveWholeToUnity(false);
    box.appendChild(save);
    box.appendChild(el('p', 'hint', known ? `En Unity: ${unity.prefab}` : `Irá a Assets/Prefabs/Enemies/Procedural/${st.name || '<nombre>'}.prefab`));

    if (unity.exists) {
      const c = el('div', 'unityConflict');
      c.appendChild(el('p', 'efSummaryError', `⚠ ${unity.exists.message}`));
      c.appendChild(el('p', 'hint', 'Actualizarlo reescribe su cuerpo, gestos y ataques con los de aquí; lo afinado a mano en su Inspector (vida, velocidad…) se conserva.'));
      const over = el('button', 'small danger', 'Actualizarlo');
      over.onclick = () => saveWholeToUnity(true);
      const cancel = el('button', 'small', 'Cancelar');
      cancel.onclick = () => { unity.exists = null; rig.refreshInspector(); };
      c.append(over, cancel);
      box.appendChild(c);
    }
    if (unity.conflict) {
      const c = el('div', 'unityConflict');
      c.appendChild(el('p', 'efSummaryError', '⚠ Cambió en Unity'));
      c.appendChild(el('p', 'hint', `${unity.conflict.message} No se ha escrito nada.`));
      const over = el('button', 'small danger', 'Sobrescribir');
      over.title = 'Lo de aquí pisa lo que tenga ahora el prefab.';
      over.onclick = () => saveWholeToUnity(true);
      box.append(c);
      c.appendChild(over);
    }
    resultBlock(box);
    host.appendChild(box);
  };

  /** The enemy on screen is the one Unity sent. */
  const onUnityEnemy = () => !!unity.id && rig.getStatus().id === unity.id;

  /** Reads the enemy from Unity and puts it on screen. `select` = { gesture, attack } (a deep link). */
  async function openFromUnity(prefab = bridge.prefab, select = { gesture: bridge.gesture, attack: bridge.attack }) {
    if (!bridge.active || !prefab) return;
    await showing;
    unity.busy = 'Leyendo de Unity…';
    unity.result = null;
    unity.conflict = null;
    rig.refreshInspector();
    try {
      const data = await fetchEnemy(prefab);
      await flushBehaviour(true);       // whatever was open before is saved first
      await rig.refreshLibraries();     // the library's effects: a Unity prefab built from one gets its preview

      // The body: the library's when this enemy was made in the web (its original canvases), else Unity's.
      const records = (await listEntries('rig')).filter((e) => e.draft && e.enemyName === data.name);
      let record = records.find((e) => !e.draft.unityBody) || null;
      const notes = [...(data.warnings || [])];
      if (!record) {
        const read = await readRigDocument(data.rigConfig, {
          sprites: data.sprites, fetchDataURL, entriesById: rig.getFxEntries(), artLibrary: data.artLibrary,
        });
        read.state.unityPrefab = data.prefab;
        notes.push(...read.notes);
        const previous = records.find((e) => e.draft.unityBody);
        record = previous
          ? await updateEnemy(previous.id, { draft: read.state })
          : await saveEnemy({ kind: 'rig', sprite: null, enemyName: data.name, draft: read.state });
      }
      // The behaviour is Unity's: its deck with every attack's numbers as they are in the assets.
      if (data.enemyConfig) await updateEnemy(record.id, { enemyConfig: data.enemyConfig });

      await rig.linkLibraryEntry(record.id);
      // A body from the library keeps its parts; its gestures are the prefab's (Unity owns what it has).
      if (!record.draft.unityBody) notes.push(...rig.applyUnityGestures(data.rigConfig, data.artLibrary));
      if (data.kind === 'enemy') await enemy.linkLibraryEntry(record.id, data.name);

      Object.assign(unity, { id: record.id, prefab: data.prefab, name: data.name, kind: data.kind, revision: data.revision, notes });
      // Each gesture's own sound, as the prefab has it (its clips, volume, pitch and instant); and what it can be changed to.
      rig.applyUnitySounds(data.sounds || []);
      unity.clips = await fetchSounds().catch(() => null);
      (data.sounds || []).forEach((s) => (s.clips || []).forEach(clipBuffer));
      gestureSignature = signatureOf(rig.getGestures());
      await rig.save();   // the library copy (thumbnail included) is now what Unity has
      await refreshPicker();

      // Deep link: a card of the deck opens its gesture too; a gesture, just itself.
      const card = select?.attack >= 0 ? enemy.focusAttack(select.attack) : null;
      const gesture = select?.gesture || card?.gesture || '';
      if (gesture && !rig.openGestureNamed(gesture)) notes.push(`El gesto "${gesture}" no está en el prefab.`);
      rig.setStatus(`Abierto "${data.name}" desde Unity.`);
    } catch (e) {
      unity.result = { error: e.message };
    } finally {
      unity.busy = '';
      rig.refreshInspector();
      render();
    }
  }

  /** Writes gestures + attacks into the prefab. `force` = overwrite what changed in Unity meanwhile. */
  async function saveToUnity(force = false) {
    if (unity.busy || !onUnityEnemy()) return;
    unity.busy = 'Guardando en Unity…';
    unity.result = null;
    unity.conflict = null;
    rig.refreshInspector();
    try {
      const blob = await rig.buildGesturesZip();   // saves the form first (hooks.beforeExport)
      if (!blob) return;
      const res = await saveGestures(blob, { prefab: unity.prefab, revision: unity.revision, force });
      if (res.revision) unity.revision = res.revision;
      unity.result = res;
      if (res.ok) await rig.save();
      rig.setStatus(res.ok ? `Guardado en Unity: ${unity.prefab}` : 'Unity no pudo aplicarlo: mira el informe.');
    } catch (e) {
      // 409 = Unity refused to write: it changed there (two choices), or it is in Play Mode (just a message).
      if (e instanceof BridgeError && e.status === 409 && e.reason !== 'playmode') unity.conflict = { message: e.message };
      else unity.result = { error: e.message };
    } finally {
      unity.busy = '';
      rig.refreshInspector();
    }
  }

  /** A project clip, fetched and decoded once (null = the browser cannot play it: that gesture stays silent here). */
  function clipBuffer(path) {
    if (!unity.buffers.has(path)) {
      unity.buffers.set(path, fetchFile(path).then((blob) => blob.arrayBuffer()).then(decodeAudio).catch(() => null));
    }
    return unity.buffers.get(path);
  }

  // The project's clips can be picked and heard only with Unity on the other end.
  rig.hooks.soundClips = () => (bridge.active ? unity.clips : null);
  rig.hooks.playClip = (path, o) => {
    if (!bridge.active || !path) return;
    clipBuffer(path).then((buffer) => { if (buffer) playBuffer(buffer, o); });
  };

  const soundCount = () => rig.getGestures().filter((g) => (g.sounds || []).some((a) => a.clips?.length)).length;

  /** Above the save / export buttons of the focused tab: the banner, "Guardar en Unity", Unity's answer. */
  rig.hooks.focusExtras = (host) => {
    if (!bridge.active) { connectRow(host); return; }
    const box = el('div', 'unityBridge');
    if (!onUnityEnemy()) {
      box.appendChild(el('p', 'unityBanner', `🔌 Unity${bridge.project ? ` · ${bridge.project}` : ''}`));
      // The library enemy on screen is in Unity too: open THAT one (its gestures and attacks as the prefab has them).
      const same = unity.prefabs?.find((p) => p.name === rig.getStatus().name);
      if (same) {
        const take = el('button', 'primary', `🔌 Abrir "${same.name}" de Unity`);
        take.style.width = '100%';
        take.title = `${same.prefab}: sus gestos y ataques tal como están en el prefab. Lo no guardado aquí se pierde.`;
        take.onclick = () => openFromUnity(same.prefab, {});
        box.appendChild(take);
      }
      box.appendChild(prefabPicker(''));
      box.appendChild(el('p', 'hint', 'Ábrelo de aquí para guardarlo de vuelta en Unity ("💾 Guardar en Unity"). Uno elegido arriba, de la biblioteca, sólo se exporta en .zip.'));
      if (unity.busy) box.appendChild(el('p', 'hint', unity.busy));
      else if (unity.result?.error) box.appendChild(el('p', 'efSummaryError', `✗ ${unity.result.error}`));
      host.appendChild(box);
      return;
    }

    box.appendChild(el('p', 'unityBanner', `🔌 Unity · ${unity.name}`));
    box.appendChild(prefabPicker(unity.prefab));
    const st = rig.getStatus();
    box.appendChild(el('p', 'hint', `${unity.prefab}. ${st.unityBody ? 'El cuerpo se ha leído de Unity (sólo para verlo y posarlo).' : 'El cuerpo es el de tu biblioteca; los gestos'
      + (unity.kind === 'enemy' ? ' y los ataques' : '') + ' son los del prefab.'}${soundCount() ? ` ${soundCount()} gesto(s) con sonido: suenan al probarlos, y se editan en sus pistas 🔊 de la línea de tiempo.` : ''}`));

    const save = el('button', 'primary', unity.busy || '💾 Guardar en Unity');
    save.style.width = '100%';
    save.disabled = !!unity.busy;
    save.title = 'Escribe en el prefab sus gestos (con efectos, sonido y sprites por estado)' + (unity.kind === 'enemy' ? ', sus ataques y sus defensas' : '') + '. No toca el cuerpo ni lo demás afinado en el Inspector.';
    save.onclick = () => saveToUnity(false);
    const reload = el('button', 'small', '↻ Recargar desde Unity');
    reload.disabled = !!unity.busy;
    reload.title = 'Vuelve a leer el prefab guardado: lo que no hayas guardado aquí se pierde.';
    reload.onclick = () => openFromUnity(unity.prefab, {});
    box.append(save, reload);

    if (unity.conflict) {
      const c = el('div', 'unityConflict');
      c.appendChild(el('p', 'efSummaryError', '⚠ Cambió en Unity'));
      c.appendChild(el('p', 'hint', `${unity.conflict.message} No se ha escrito nada.`));
      const take = el('button', 'small', '↻ Recargar desde Unity');
      take.title = 'Te quedas con lo de Unity: lo cambiado aquí se pierde.';
      take.onclick = () => openFromUnity(unity.prefab, {});
      const over = el('button', 'small danger', 'Sobrescribir');
      over.title = 'Lo de aquí pisa los gestos y ataques que tenga ahora el prefab.';
      over.onclick = () => saveToUnity(true);
      c.append(take, over);
      box.appendChild(c);
    }

    const r = unity.result;
    if (r?.error) box.appendChild(el('p', 'efSummaryError', `✗ ${r.error}`));
    else if (r) {
      box.appendChild(el('p', r.ok ? 'efSummaryOk' : 'efSummaryError', r.ok ? '✓ Guardado en Unity' : '✗ Unity no pudo aplicarlo'));
      const pre = el('pre', 'unityReport', r.summary || '');
      box.appendChild(pre);
    }
    if (unity.notes.length) {
      const list = el('ul', 'hint');
      unity.notes.forEach((n) => list.appendChild(el('li', null, n)));
      box.appendChild(list);
    }
    host.appendChild(box);
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
      focusInfo.textContent = unity.busy ? unity.busy : !st.id
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

  let showing = Promise.resolve();   // the last show(): openFromUnity waits for its panels

  /** `as` = 'full' (🧬 Enemy Studio, the steps) or 'focus' (🎬 Gestos y ataques). */
  function show(as = 'full') {
    showing = showNow(as).catch((e) => console.warn('[enemy-studio]', e));
    return showing;
  }

  async function showNow(as) {
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

  return { show, hide, open, linkLibraryEntry, openFromUnity, isMounted: () => mounted };
}
