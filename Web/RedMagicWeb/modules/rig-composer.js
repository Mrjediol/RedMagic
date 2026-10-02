// modules/rig-composer.js
// -----------------------------------------------------------------------------
// The rig composer — the BODY EDITOR of 🧬 Enemy Studio: assemble an enemy or boss from separate part
// PNGs and export the rig-config .zip Unity's Tools ▸ Web ▸ Rig ▸ Importar rig reads. It has no tab
// of its own any more (the 🦴 Rig Composer tab was retired): enemy-studio.js lays its hosts out.
//
//   left   — document (name, kind, scale), the parts list (draw order + parenting by drag)
//   centre — canvas with tools: move / rotate / scale / pivot / centre / point / barrel / muzzle / grip
//   right  — the selected part (transform, roles, sway, state sprites), rig timings, GESTURES,
//            collider, links to an Enemy Creator / Boss Creator entry, validation and export
//
// GESTURES (inspector ▸ Gestos): the named timelines an attack telegraphs with. The open gesture's
// TIMELINE sits under the canvas (gesture-timeline.js: steps, releases, effects, a playhead that stops
// it at any instant); this file is its host — it owns the canvas side of it:
//  - "✋ Posar este paso" turns the canvas into a pose editor: move / rotate / scale then write that
//    part's OFFSET in the step's pose instead of the part itself (drag on empty space = the whole body);
//  - the playhead decides what the canvas shows (the body, its effects, where the attack leaves from),
//    with RigDriverCode's timing. Data and formulas: rig-gestures.js, rig-fx.js;
//  - the selected release (a red target) and effect (a cross) are dragged on the canvas, or placed
//    with "⌖ Colocar".
//
// "Nuevo desde plantilla" starts the document from a body shape (rig-templates.js): its slots show
// up as empty parts to drop art on (parts list row, canvas placeholder, or a bulk drop by file name).
//
// The panels are several HOSTS rather than two columns, so 🧬 Enemy Studio (enemy-studio.js) can lay
// the same single composer out as workflow steps: `sidebar` (document + parts), `templateHost`
// (templates: new from one, save as one), `inspector` (the selected part, timings, gestures,
// collider), `statesHost` (the state-sprite board: which sprite each part shows asleep / alert /
// attacking / hurt / dead / in each gesture), `linksHost` (behaviour link) and `exportHost`. There is ONE composer and one document
// whichever tab shows it.
//
// Geometry and part operations: rig-model.js. Drawing and picking: rig-canvas-view.js. Export:
// rig-export.js. Preview formulas copy RigDriverCode.cs (sway, hover, lean, hurt).

import { createDefaultRigState, createPart, RIG_FIELD_GROUPS, createDefaultRigFields, adoptRigState } from './rig-defaults.js';
import * as M from './rig-model.js';
import { RigCanvasView } from './rig-canvas-view.js';
import { buildRigExport, buildRigBundle, readRigGestures } from './rig-export.js';
import { loadValidator, validateConfig } from './config-schema.js';
import { saveEnemy, updateEnemy, getEnemy, deleteEnemy, listEntries, buildThumbnail } from './enemy-library.js';
import { downloadBlob } from './export-manifest.js';
import * as F from './enemy-form-fields.js';
import { RIG_TEMPLATES, templateById, instantiateTemplate, openSlots, matchSlot, fillSlot, fitLengths, suggestedEnemyConfig,
  templateFromState, templateFromRecord, importGestures } from './rig-templates.js';
import * as G from './rig-gestures.js';
import * as FX from './rig-fx.js';
import { createGestureTimeline } from './gesture-timeline.js';

const TOOLS = [
  { id: 'move', label: '✋ Mover', key: 'v', hint: 'Arrastra una pieza. Clic en vacío = deseleccionar.' },
  { id: 'rotate', label: '⟳ Rotar', key: 'r', hint: 'Arrastra: gira la pieza seleccionada alrededor de su pivote.' },
  { id: 'scale', label: '⤢ Escalar', key: 's', hint: 'Arrastra alejándote/acercándote del pivote.' },
  { id: 'pivot', label: '✚ Pivote', key: 'p', hint: 'Clic: la articulación de la pieza (gira y se escala desde aquí). La imagen no se mueve.' },
  { id: 'center', label: '● Centro', key: 'c', hint: 'Clic: centro visible = punto de contacto cuando un ataque lanza la pieza.' },
  { id: 'point', label: '➜ Frente al lanzar', key: 'a', hint: 'Sólo piezas que un jefe lanza (extremidades, orbitales): arrastra hacia dónde mira su frente en el dibujo (los nudillos de un puño). Sale volando con ese lado por delante. NO es la boca del arma: eso es ◉ Boca.' },
  { id: 'muzzle', label: '◉ Boca', key: 'm', hint: 'Clic sobre la pieza que apunta: la punta del arma, de donde sale el proyectil. El cañón (pivote → boca) se calcula solo.' },
  { id: 'barrel', label: '➜ Cañón', key: 'b', hint: 'Normalmente no hace falta: al poner la Boca se calcula solo (pivote → boca). Arrastra sólo para corregirlo: esa línea es la que se alinea con el jugador al apuntar.' },
  { id: 'grip', label: '✊ Agarre', key: 'g', hint: 'Clic sobre una extremidad: dónde sostiene objetos (jefes).' },
];

const ROLE_LABELS = { aim: 'apunta', stretch: 'se estira', rider: 'pasajero', limb: 'extremidad', orbiter: 'orbital', body: 'cuerpo' };

const clone = (o) => JSON.parse(JSON.stringify(o));
const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

export function initRigComposer({ sidebar, templateHost, toolbar, canvas, canvasWrap, timelineHost, statusEl, inspector, statesHost, linksHost, exportHost }) {
  // Set by whoever embeds the composer (Enemy Studio): beforeExport / afterSave(id) / onChange.
  const hooks = { beforeExport: null, afterSave: null, onChange: null };
  let userTemplates = [];        // library records of kind 'rig-template', as templates
  let state = createDefaultRigState();
  let selectedUid = null;
  let tool = 'move';
  let currentId = null;          // library record id this document was saved as / opened from
  let validate = null;
  let behaviour = null;          // the EnemyConfig saved on this rig's record (from Enemy Creator)
  let bossEntries = [];

  // preview (RigDriverCode's formulas)
  const preview = { playing: false, clock: 0, lean: 0, speed: 0, hurt: -1, spriteState: '', stage: 0, last: 0 };

  // gestures: which one is open, whether one of its steps' poses is being edited on the canvas, and
  // the playhead of its timeline. UI state only — the gestures themselves are in `state`.
  let gestureIndex = -1;
  let posing = null;             // { index, step } while a step's pose is being edited
  const head = { t: 0, playing: false, lead: null };   // the open gesture's playhead; lead = a warning only for this play
  let previewTelegraph = 0.6;    // seconds of warning the preview launches a gesture with (the attack's 'telegraph')
  let placing = null;            // { kind: 'fx' | 'release', index }: the next canvas click places it
  let fxEntries = new Map();     // library effects (kind fx / projectile, with frames) by id

  const POSE_TOOLS = ['move', 'rotate', 'scale'];

  function openGesture() { return state.gestures?.[gestureIndex] || null; }
  function lead() { return head.lead ?? previewTelegraph; }

  // The timeline editor of the open gesture (the dock under the canvas + the selected thing's fields).
  const timeline = createGestureTimeline({
    gesture: openGesture,
    lead,
    setLead: (v) => { previewTelegraph = v; },
    time: () => head.t,
    playing: () => head.playing,
    togglePlay,
    seek: (t) => { head.playing = false; head.t = Math.max(0, t); timeline.updatePlayhead(); requestRender(); },
    scrub: (t) => {
      const wasPosing = !!posing;
      if (wasPosing) stopPosing(true);
      head.playing = false;
      head.t = Math.max(0, t);
      if (wasPosing) renderInspector(); else timeline.updatePlayhead();
      requestRender();
    },
    posingStep: () => (posing && posing.index === gestureIndex ? posing.step : -1),
    pose: (step) => (step < 0 ? stopPosing() : startPosing(gestureIndex, step)),
    renderPose: (box, step) => renderPosePanel(box, openGesture(), step),
    placing: () => placing,
    place: (kind, index) => {
      placing = placing?.kind === kind && placing.index === index ? null : { kind, index };
      renderInspector();
      if (placing) setStatus(kind === 'fx' ? 'Haz clic donde va el efecto (se guarda relativo a su ancla). Esc = cancelar.' : 'Haz clic en el punto del que sale el ataque. Esc = cancelar.');
    },
    parts: () => state.parts.filter((p) => p.image || p.rigPart).map((p) => ({ uid: p.uid, name: p.name })),
    hasMuzzle: () => !!(state.roles.aim && state.roles.muzzle),
    // The weapon tip when there is one (the usual place); else the selected part; else the body.
    defaultAnchor: () => {
      const sel = selectedPart();
      const anchor = state.roles.aim && state.roles.muzzle ? 'muzzle' : sel?.rigPart ? 'part' : 'body';
      return { anchor, part: anchor === 'part' ? sel.uid : null };
    },
    fxEntries: () => fxEntries,
    changed: () => changed(),
    refresh: () => { renderInspector(); requestRender(); if (hooks.onChange) hooks.onChange(); },
    redraw: () => { requestRender(); refreshExport(); },
    status: (text) => setStatus(text),
  });

  /**
   * The gesture layered over the body right now — the pose being edited, or the instant the playhead
   * is on: `{ gesture, weights[], shake, settling, moment }` — or null (the body at rest).
   */
  function gestureLayer() {
    const gesture = openGesture();
    if (!gesture) return null;
    if (posing && gesture.steps[posing.step]) {
      return { gesture, weights: gesture.steps.map((_, i) => (i === posing.step ? 1 : 0)), shake: 0, settling: false, moment: null };
    }
    if (head.playing || head.t > 0) return layerAt(gesture, head.t);
    return null;
  }

  /** The body `t` seconds into a gesture (RigDriverCode.GestureRoutine). */
  function layerAt(gesture, t) {
    const at = G.gestureAt(gesture, t, lead());
    return { gesture, weights: at.weights, shake: at.shake ? Math.sin(t * 80) * at.shake : 0, settling: at.settling, moment: at.moment };
  }

  /** What the canvas shows right now (poses + Visual matrix), or null for the plain rest pose. */
  function currentAnim() {
    return preview.playing || preview.hurt >= 0 || gestureLayer() ? previewPoses() : null;
  }

  loadValidator('rig').then((v) => { validate = v; refreshExport(); })
    .catch((e) => setStatus(`No se pudo cargar el validador: ${e.message}`));

  const view = new RigCanvasView(canvas, canvasWrap, { onChange: () => requestRender() });

  // ============================================================ render scheduling

  let renderQueued = false;
  function requestRender() {
    if (renderQueued) return;
    renderQueued = true;
    requestAnimationFrame(() => { renderQueued = false; draw(); });
  }

  /** The sprite state on screen: the gesture the playhead is in, the hurt preview, else the one picked. */
  function shownSpriteState(layer) {
    if (layer && layer.moment !== null) return M.gestureState(layer.gesture.name);
    if (preview.hurt >= 0) return 'hurt';
    return preview.spriteState;
  }

  function draw() {
    const layer = gestureLayer();
    const anim = currentAnim();
    view.render(state, {
      selectedUid,
      poses: anim?.poses,
      visualMatrix: anim?.visual,
      spriteState: shownSpriteState(layer),
      // Where the gesture is, in steps (0–1 the first… the last stretch is the way back), or null.
      gestureMoment: layer ? layer.moment : null,
      stage: preview.stage,
      ghostRest: !!posing,
      fx: fxOverlay(anim),
      marks: releaseMarks(anim),
      requestRender,
    });
  }

  function setStatus(text) { statusEl.textContent = text; }

  // ============================================================ gesture effects on the canvas

  const fxFramesOf = (f) => FX.fxFrames(fxEntries.get(f.fx), f.loop);

  /**
   * Visual-space matrix of an effect's anchor (its local space): a part, the muzzle on the aim part,
   * the whole body, or the ground under it. `anim` = the poses on screen (null = rest).
   */
  function fxAnchorMatrix(f, anim) {
    const visual = anim?.visual || M.IDENTITY;
    const partMatrix = (part) => M.multiply(visual, M.worldMatrix(state, part, anim?.poses));
    if (f.anchor === 'part') {
      const part = M.byUid(state, f.part);
      if (part) return partMatrix(part);
    } else if (f.anchor === 'muzzle') {
      const aim = M.byUid(state, state.roles.aim);
      if (aim && state.roles.muzzle) return M.multiply(partMatrix(aim), M.trs(M.pixelToLocal(state, aim, state.roles.muzzle), 0, { x: 1, y: 1 }));
      if (aim) return partMatrix(aim);
    } else if (f.anchor === 'feet') {
      const c = state.collider;
      const half = c.type === 'circle' ? (c.radius || 0.5) : c.size.y / 2;
      return M.trs({ x: c.offset.x, y: c.offset.y - half }, 0, { x: 1, y: 1 });
    }
    return visual;
  }

  /** Degrees the action points to for an effect ('direction' facing), with the body facing right. */
  function fxDirection(f, anim) {
    const aim = M.byUid(state, state.roles.aim);
    if (f.anchor !== 'muzzle' || !aim || !state.roles.muzzle) return 0;
    // At the muzzle: where the weapon points, pivot → muzzle.
    const m = M.multiply(anim?.visual || M.IDENTITY, M.worldMatrix(state, aim, anim?.poses));
    const from = M.apply(m, { x: 0, y: 0 }), to = M.apply(m, M.pixelToLocal(state, aim, state.roles.muzzle));
    return (Math.atan2(to.y - from.y, to.x - from.x) * 180) / Math.PI;
  }

  /**
   * Frame pixels (centred, y down) → Visual space, for an effect sitting on its anchor — RigDriver.FxPose:
   * always AT its anchor's point; turned with the anchor, towards the action, or upright.
   */
  function fxMatrix(f, anchorMatrix, anim) {
    const k = (f.scale > 0 ? f.scale : 1) / state.pixelsPerUnit / FX.fxDensity(fxEntries.get(f.fx));
    const offset = f.offset || { x: 0, y: 0 };
    const facing = f.facing || 'direction';
    if (facing === 'anchor' && f.anchor !== 'feet') return M.multiply(anchorMatrix, M.trs(offset, f.angle || 0, { x: k, y: -k }));
    const angle = (f.angle || 0) + (facing === 'direction' ? fxDirection(f, anim) : 0);
    return M.trs(M.apply(anchorMatrix, offset), angle, { x: k, y: -k });
  }

  /** What the canvas draws of the open gesture's effects: the ones alive at the playhead, and the one selected. */
  function fxOverlay(anim) {
    const gesture = openGesture();
    const out = [];
    if (!gesture) return out;
    const chosen = timeline.selection?.kind === 'fx' ? timeline.selection.index : -1;
    const shown = new Set();
    if (!posing && (head.playing || head.t > 0)) {
      const tl = G.timeline(gesture, lead());
      FX.activeFx(gesture, head.t, lead(), fxFramesOf, tl).forEach(({ fx, index, frame }) => {
        // An effect that does not follow stays where it came out: its anchor as the body was at that instant.
        const at = fx.followsPart === false ? previewPoses(layerAt(gesture, G.timeOf(tl, fx.step, fx.at || 0))) : anim;
        shown.add(index);
        out.push({ sheet: fxEntries.get(fx.fx).sprite.sourceDataURL, box: fxFramesOf(fx).boxes[frame], matrix: fxMatrix(fx, fxAnchorMatrix(fx, at), at),
          behind: fx.layer === 'behind', marked: index === chosen && !head.playing });
      });
    }
    const f = gesture.fx?.[chosen];
    if (f && !shown.has(chosen) && !head.playing) {   // the one being edited, even outside its moment
      const frames = fxFramesOf(f);
      out.push({ sheet: frames ? fxEntries.get(f.fx).sprite.sourceDataURL : null, box: frames?.boxes[0], matrix: fxMatrix(f, fxAnchorMatrix(f, anim), anim),
        alpha: 0.85, marked: true, behind: f.layer === 'behind' });
    }
    return out;
  }

  /** Visual-space matrix of the point a release leaves from: its part, or the muzzle (→ the aim part → the body). */
  function releaseMatrix(r, anim) {
    return fxAnchorMatrix(r.point ? { anchor: 'part', part: r.point } : { anchor: 'muzzle' }, anim);
  }

  /** Where the open gesture's attack leaves from: a target per release, a burst as each one fires. */
  function releaseMarks(anim) {
    const gesture = openGesture();
    if (!gesture) return [];
    const tl = G.timeline(gesture, lead());
    const chosen = timeline.selection?.kind === 'release' ? timeline.selection.index : -1;
    const out = [];
    tl.releases.forEach((r, n) => {
      const point = M.apply(releaseMatrix(r.release, anim), r.release.offset);
      const age = head.t - r.time;
      if (head.playing) { if (age >= 0 && age < 0.25) out.push({ point, fired: age / 0.25 }); }
      else out.push({ point, label: tl.releases.length > 1 ? `suelta ${n + 1}` : 'suelta', selected: r.index >= 0 && r.index === chosen });
    });
    return out;
  }

  /** The thing selected on the timeline that sits on the canvas (a release, an effect): its anchor and its point. */
  function canvasHandle(anim) {
    const gesture = openGesture();
    const sel = timeline.selection;
    if (!gesture || !sel || (sel.kind !== 'fx' && sel.kind !== 'release')) return null;
    const item = sel.kind === 'fx' ? gesture.fx[sel.index] : gesture.releases[sel.index];
    const matrix = sel.kind === 'fx' ? fxAnchorMatrix(item, anim) : releaseMatrix(item, anim);
    return { item, matrix, point: M.apply(matrix, item.offset || { x: 0, y: 0 }) };
  }

  const r3 = (v) => Math.round(v * 1000) / 1000;

  /** "⌖ Colocar": the clicked point becomes the offset of the effect / release on its anchor. */
  function placeAt(pt) {
    const { kind, index } = placing;
    placing = null;
    const gesture = openGesture();
    const item = kind === 'fx' ? gesture?.fx?.[index] : gesture?.releases?.[index];
    if (!item) { renderInspector(); return; }
    const anim = currentAnim();
    const local = M.apply(M.invert(kind === 'fx' ? fxAnchorMatrix(item, anim) : releaseMatrix(item, anim)), pt);
    item.offset = { x: r3(local.x), y: r3(local.y) };
    changed();
    setStatus(kind === 'fx' ? 'Efecto colocado. "▶ Probar" lo enseña en su momento.' : 'Punto de salida colocado: de ahí sale el ataque en esa suelta.');
  }

  // ============================================================ preview animation

  function previewPoses(layer = gestureLayer()) {
    const rig = state.rig;
    const poses = new Map();
    state.parts.forEach((part) => {
      if (!part.rigPart) return;
      const s = Math.sin(preview.clock * part.sway.speed + part.sway.phase);
      poses.set(part.uid, {
        position: { x: part.position.x + part.sway.move.x * s, y: part.position.y + part.sway.move.y * s },
        rotation: part.rotation + part.sway.rotate * s + preview.lean * part.leanFollow,
        scale: { x: part.scale.x * (1 + part.sway.scale.x * s), y: part.scale.y * (1 + part.sway.scale.y * s) },
      });
    });

    let hurtTilt = 0, squash = 0;
    if (preview.hurt >= 0) {
      const k = preview.hurt / rig.hurtDuration;
      if (k < 1) {
        const envelope = 1 - k;
        hurtTilt = rig.hurtTilt * envelope * Math.sin(k * Math.PI * 3);
        squash = rig.hurtSquash * envelope * Math.cos(k * Math.PI * 2);
      }
    }

    const bob = { x: rig.hoverBob.x * Math.sin(preview.clock * rig.hoverSpeed), y: rig.hoverBob.y * Math.sin(preview.clock * rig.hoverSpeed) };
    const body = { position: bob, rotation: preview.lean + hurtTilt, scale: { x: 1 + squash, y: 1 - squash } };

    // The gesture goes on top of everything else, like in the driver (additive layers).
    if (layer) {
      G.applySteps(layer.gesture, layer.weights, poses, body, layer.settling);
      body.rotation += layer.shake;
    }

    return { poses, visual: M.trs(body.position, body.rotation, body.scale) };
  }

  function tick(now) {
    const dt = preview.last ? Math.min(0.1, (now - preview.last) / 1000) : 0;
    preview.last = now;
    const rig = state.rig;
    if (preview.playing) preview.clock += dt;
    const leanTarget = -Math.max(-1, Math.min(1, preview.speed)) * rig.leanDegrees;
    preview.lean += (leanTarget - preview.lean) * (1 - Math.exp(-rig.leanResponse * dt));
    if (preview.hurt >= 0) {
      preview.hurt += dt;
      if (preview.hurt >= rig.hurtDuration) preview.hurt = -1;
    }
    if (head.playing) {
      const gesture = openGesture();
      head.t += dt;
      if (!gesture || head.t >= G.gestureDuration(gesture, lead())) {   // done: the body back at rest, the playhead at the start
        const borrowed = head.lead !== null;
        stopHead();
        if (borrowed) renderTimeline();   // it played with another warning (an idle gesture): the track goes back to its own
      }
      timeline.updatePlayhead();
    }
    draw();
    if (preview.playing || preview.hurt >= 0 || head.playing || Math.abs(preview.lean) > 0.01) requestAnimationFrame(tick);
    else { preview.running = false; preview.last = 0; preview.lean = 0; draw(); }
  }

  function startTicking() {
    if (preview.running) return;
    preview.running = true;
    preview.last = 0;
    requestAnimationFrame(tick);
  }

  // ============================================================ toolbar

  function renderToolbar() {
    toolbar.innerHTML = '';
    const tools = el('div', 'rigTools');
    TOOLS.forEach((t) => {
      const b = el('button', `small${tool === t.id ? ' active' : ''}`, t.label);
      b.title = `${t.hint} (tecla ${t.key.toUpperCase()})`;
      b.onclick = () => { tool = t.id; renderToolbar(); setStatus(t.hint); };
      tools.appendChild(b);
    });
    undoBtn = el('button', 'small', '↶ Deshacer');
    undoBtn.title = 'Ctrl+Z';
    undoBtn.onclick = undo;
    redoBtn = el('button', 'small', '↷ Rehacer');
    redoBtn.title = 'Ctrl+Y';
    redoBtn.onclick = redo;
    tools.append(undoBtn, redoBtn);
    renderUndoButtons();
    const centre = el('button', 'small', '◎ Origen al centro');
    centre.title = 'Mueve el cuerpo entero para que su centro quede en el origen (la raíz del prefab): desde ahí se miden rangos y detección.';
    centre.onclick = () => { if (centerOrigin()) { changed(); setStatus('Origen (raíz del prefab) en el centro del cuerpo.'); } };
    tools.appendChild(centre);
    const frame = el('button', 'small', '⌖ Encuadrar');
    frame.onclick = () => view.frame(state);
    tools.appendChild(frame);
    toolbar.appendChild(tools);

    const pv = el('div', 'rigPreviewBar');
    const play = el('button', `small${preview.playing ? ' active' : ''}`, preview.playing ? '⏸ Balanceo' : '▶ Balanceo');
    play.title = 'Vista previa del balanceo en reposo (mismas fórmulas que RigDriverCode).';
    play.onclick = () => { preview.playing = !preview.playing; renderToolbar(); startTicking(); };
    pv.appendChild(play);

    const hurt = el('button', 'small', '💥 Golpe');
    hurt.title = 'Vista previa del golpe recibido (hurtTilt / hurtSquash / hurtDuration).';
    hurt.onclick = () => { preview.hurt = 0; startTicking(); };
    pv.appendChild(hurt);

    pv.appendChild(el('span', 'hint', 'Inclinación:'));
    const lean = document.createElement('input');
    lean.type = 'range'; lean.min = '-1'; lean.max = '1'; lean.step = '0.05'; lean.value = String(preview.speed);
    lean.title = 'Simula desplazarse (−1 izquierda … 1 derecha): inclinación del cuerpo y leanFollow de las piezas.';
    lean.oninput = () => { preview.speed = parseFloat(lean.value); startTicking(); };
    lean.ondblclick = () => { lean.value = '0'; preview.speed = 0; startTicking(); };
    pv.appendChild(lean);

    const st = document.createElement('select');
    spriteStateOptions().forEach(([v, l]) => {
      const o = el('option', null, l); o.value = v; st.appendChild(o);
    });
    st.value = preview.spriteState;
    st.title = 'Qué sprite por estado enseñar (el tablero "Sprites por estado" los asigna).';
    st.onchange = () => { preview.spriteState = st.value; requestRender(); renderInspector(); };
    pv.appendChild(st);

    const maxStages = Math.max(0, ...state.parts.map((p) => (p.variants?.stages || []).length));
    if (maxStages > 0) {
      const sg = document.createElement('select');
      for (let i = 0; i <= maxStages; i++) { const o = el('option', null, `etapa ${i}`); o.value = String(i); sg.appendChild(o); }
      sg.value = String(preview.stage);
      sg.onchange = () => { preview.stage = parseInt(sg.value, 10); requestRender(); };
      pv.appendChild(sg);
    }
    toolbar.appendChild(pv);
  }

  /** Every sprite state of this document: the fixed ones plus one per gesture. */
  function spriteStateOptions() {
    return [['', 'sprite base'], ...M.SPRITE_STATES.map((s) => [s.id, s.label.toLowerCase()]),
      ...(state.gestures || []).map((g) => [M.gestureState(g.name), `gesto: ${g.name}`])];
  }

  document.addEventListener('keydown', (e) => {
    if (canvas.closest('.tabPanel')?.hidden) return;
    if ((e.target instanceof Element && e.target.closest('input, textarea, select'))) return;
    const t = TOOLS.find((x) => x.key === e.key.toLowerCase());
    if (e.ctrlKey || e.metaKey) return;
    if (t) { tool = t.id; renderToolbar(); setStatus(t.hint); }
    if (e.key === 'Escape' && placing) { placing = null; renderInspector(); setStatus('Colocar: cancelado.'); return; }
    if (e.key === 'Escape' && posing) { stopPosing(); return; }
    if (e.key === 'Delete' || e.key === 'Supr') {
      // Posing: Supr takes the part out of the pose, never deletes the part.
      if (posing) clearPose(poseTarget());
      else if (selectedUid) removePart(M.byUid(state, selectedUid));
    }
  });

  // ============================================================ canvas interaction

  let drag = null;

  function selectedPart() { return M.byUid(state, selectedUid); }

  /** Visual-space point → original pixels of `part` (rest pose). */
  function pixelOn(part, visualPt) {
    const local = M.apply(M.invert(M.worldMatrix(state, part)), visualPt);
    const px = M.localToPixel(state, part, local);
    return { x: Math.round(px.x * 10) / 10, y: Math.round(px.y * 10) / 10 };
  }

  /** aimArtAngle = direction pivot → muzzle in the arm's unrotated art (local units, y up). */
  function barrelFromMuzzle(aim) {
    const local = M.pixelToLocal(state, aim, state.roles.muzzle);
    return Math.round((Math.atan2(local.y, local.x) * 180) / Math.PI * 10) / 10;
  }

  function localDirectionAngle(part, visualPt) {
    const world = M.worldMatrix(state, part);
    const pivot = M.apply(world, { x: 0, y: 0 });
    const d = M.applyVector(M.invert(world), { x: visualPt.x - pivot.x, y: visualPt.y - pivot.y });
    return Math.round((Math.atan2(d.y, d.x) * 180) / Math.PI * 10) / 10;
  }

  /** Posing: what a drag edits — the selected part, or the whole body when nothing is selected. */
  function poseTarget() { return selectedUid || G.BODY; }

  /**
   * A press on the canvas while a pose is being edited: the same three tools, but they write the
   * OFFSET of that part in the pose (rig-gestures.js) and leave the part itself alone.
   */
  function beginPoseDrag(e, pt) {
    const gesture = state.gestures?.[posing.index];
    if (!gesture) { stopPosing(); return; }
    if (!POSE_TOOLS.includes(tool)) { setStatus('Posando un gesto sólo valen Mover, Rotar y Escalar. "■ Salir de posar" para el resto de herramientas.'); return; }
    head.playing = false;

    const anim = currentAnim();
    const hit = view.partAt(state, e.clientX, e.clientY, { poses: anim?.poses, visualMatrix: anim?.visual });
    if (tool === 'move' || !selectedUid) select(hit ? hit.uid : null);

    const part = selectedPart();
    if (part && !part.rigPart) { setStatus(`"${part.name}" no es parte del rig (inspector ▸ "Parte del rig"): un gesto no puede moverla.`); return; }

    const pose = G.poseOf(gesture, posing.step, poseTarget(), true);
    if (!pose) { stopPosing(); return; }
    const visual = anim?.visual || M.IDENTITY;
    // Root space → the space the offset is measured in (the part's parent; the root for the body).
    const parent = part ? M.byUid(state, part.parent) : null;
    const frame = part ? M.multiply(visual, parent ? M.worldMatrix(state, parent, anim?.poses) : M.IDENTITY) : M.IDENTITY;
    const pivot = part ? M.apply(M.multiply(visual, M.worldMatrix(state, part, anim?.poses)), { x: 0, y: 0 }) : M.apply(visual, { x: 0, y: 0 });

    if (tool === 'move') {
      drag = { kind: 'pose-move', pose, start: pt, move: { ...pose.move }, inv: M.invert(frame) };
    } else if (tool === 'rotate') {
      drag = { kind: 'pose-rotate', pose, pivot, a0: Math.atan2(pt.y - pivot.y, pt.x - pivot.x), rot: pose.rotate,
        mirror: Math.sign(frame[0] * frame[3] - frame[1] * frame[2]) || 1 };
    } else {
      drag = { kind: 'pose-scale', pose, pivot, d0: Math.max(1e-3, Math.hypot(pt.x - pivot.x, pt.y - pivot.y)), scale: { ...pose.scale } };
    }
    setStatus(`Posando "${stepLabel(gesture, posing.step)}" de "${gesture.name}": ${part ? part.name : 'el cuerpo entero'}.`);
  }

  canvas.addEventListener('mousedown', (e) => {
    if (e.button !== 0) return;
    const pt = view.screenToVisual(e.clientX, e.clientY);
    if (placing) { placeAt(pt); return; }
    // The selected release (its target) or effect (its cross): dragging it moves it on its anchor.
    const handle = canvasHandle(currentAnim());
    if (handle && Math.hypot(pt.x - handle.point.x, pt.y - handle.point.y) * view.zoom < 14) {
      head.playing = false;
      drag = { kind: 'handle', item: handle.item, inv: M.invert(handle.matrix) };
      return;
    }
    if (posing) { beginPoseDrag(e, pt); return; }
    const hit = view.partAt(state, e.clientX, e.clientY);
    let part = selectedPart();

    if (tool === 'move') {
      // The handle of an empty node first (a small target, usually under its children's art).
      const target = view.nodeAt(state, e.clientX, e.clientY) || hit || view.slotAt(state, e.clientX, e.clientY);
      if (!target) { select(null); return; }
      select(target.uid);
      const parent = M.byUid(state, target.parent);
      drag = { kind: 'move', part: target, start: pt, pos: { ...target.position }, inv: parent ? M.invert(M.worldMatrix(state, parent)) : M.IDENTITY };
      return;
    }

    if (!part && hit) { select(hit.uid); part = hit; }
    if (!part) { setStatus('Selecciona una pieza primero.'); return; }

    const world = M.worldMatrix(state, part);
    const pivot = M.apply(world, { x: 0, y: 0 });
    const parent = M.byUid(state, part.parent);
    const pw = parent ? M.worldMatrix(state, parent) : M.IDENTITY;
    const parentDet = Math.sign(pw[0] * pw[3] - pw[1] * pw[2]) || 1;   // padre reflejado = giro al revés

    switch (tool) {
      case 'rotate':
        drag = { kind: 'rotate', part, pivot, a0: Math.atan2(pt.y - pivot.y, pt.x - pivot.x), rot: part.rotation, mirror: parentDet };
        break;
      case 'scale':
        drag = { kind: 'scale', part, pivot, d0: Math.max(1e-3, Math.hypot(pt.x - pivot.x, pt.y - pivot.y)), scale: { ...part.scale } };
        break;
      case 'pivot':
        if (!part.image) { setStatus('Una pieza vacía no tiene imagen: su pivote es su posición.'); return; }
        M.setPivot(state, part, pixelOn(part, pt));
        if (part.uid === state.roles.aim && state.roles.muzzle) state.rig.aimArtAngle = barrelFromMuzzle(part);
        changed();
        break;
      case 'center':
        part.center = pixelOn(part, pt);
        changed();
        break;
      case 'point':
        drag = { kind: 'point', part };
        part.pointAngle = localDirectionAngle(part, pt);
        changed(false);
        break;
      case 'barrel':
        if (state.roles.aim !== part.uid) { setStatus('El cañón es de la pieza que apunta: márcala como "apunta" en el inspector.'); return; }
        drag = { kind: 'barrel', part };
        state.rig.aimArtAngle = localDirectionAngle(part, pt);
        changed(false);
        break;
      case 'muzzle': {
        const aim = M.byUid(state, state.roles.aim);
        if (!aim) { setStatus('Primero marca una pieza como "apunta" (inspector ▸ Roles).'); return; }
        state.roles.muzzle = pixelOn(aim, pt);
        state.rig.aimArtAngle = barrelFromMuzzle(aim);
        changed();
        setStatus(`Boca puesta. Cañón = pivote → boca (${state.rig.aimArtAngle}°): esa línea es la que apunta al jugador.`);
        break;
      }
      case 'grip':
        if (!state.roles.limbs.includes(part.uid)) { setStatus('El agarre es de una extremidad: márcala como "extremidad" en el inspector.'); return; }
        state.roles.grips[part.uid] = pixelOn(part, pt);
        changed();
        break;
      default:
    }
  });

  window.addEventListener('mousemove', (e) => {
    if (!drag) return;
    const pt = view.screenToVisual(e.clientX, e.clientY);
    const p = drag.part;
    if (drag.kind === 'handle') {
      drag.item.offset = M.apply(drag.inv, pt);
    } else if (drag.kind === 'pose-move') {
      const d = M.applyVector(drag.inv, { x: pt.x - drag.start.x, y: pt.y - drag.start.y });
      drag.pose.move = { x: drag.move.x + d.x, y: drag.move.y + d.y };
    } else if (drag.kind === 'pose-rotate') {
      const a = Math.atan2(pt.y - drag.pivot.y, pt.x - drag.pivot.x);
      let rot = drag.rot + (((a - drag.a0) * 180) / Math.PI) * drag.mirror;
      if (e.shiftKey) rot = Math.round(rot / 15) * 15;
      drag.pose.rotate = rot;
    } else if (drag.kind === 'pose-scale') {
      const k = Math.hypot(pt.x - drag.pivot.x, pt.y - drag.pivot.y) / drag.d0;
      drag.pose.scale = { x: (1 + drag.scale.x) * k - 1, y: (1 + drag.scale.y) * k - 1 };
    } else if (drag.kind === 'move') {
      const d = M.applyVector(drag.inv, { x: pt.x - drag.start.x, y: pt.y - drag.start.y });
      p.position = { x: drag.pos.x + d.x, y: drag.pos.y + d.y };
    } else if (drag.kind === 'rotate') {
      const a = Math.atan2(pt.y - drag.pivot.y, pt.x - drag.pivot.x);
      let rot = drag.rot + (((a - drag.a0) * 180) / Math.PI) * drag.mirror;
      if (e.shiftKey) rot = Math.round(rot / 15) * 15;
      p.rotation = Math.round(rot * 10) / 10;
    } else if (drag.kind === 'scale') {
      const k = Math.hypot(pt.x - drag.pivot.x, pt.y - drag.pivot.y) / drag.d0;
      p.scale = { x: Math.round(drag.scale.x * k * 1000) / 1000, y: Math.round(drag.scale.y * k * 1000) / 1000 };
    } else if (drag.kind === 'point') {
      p.pointAngle = localDirectionAngle(p, pt);
    } else if (drag.kind === 'barrel') {
      state.rig.aimArtAngle = localDirectionAngle(p, pt);
    }
    requestRender();
  });

  window.addEventListener('mouseup', () => {
    if (!drag) return;
    if (drag.pose) G.roundPose(drag.pose);
    if (drag.kind === 'handle') drag.item.offset = { x: r3(drag.item.offset.x), y: r3(drag.item.offset.y) };
    drag = null;
    changed();
  });

  // Files dropped on the canvas = new parts.
  canvasWrap.addEventListener('dragover', (e) => { if (e.dataTransfer?.types?.includes('Files')) e.preventDefault(); });
  canvasWrap.addEventListener('drop', (e) => {
    if (!e.dataTransfer?.files?.length) return;
    e.preventDefault();
    const files = [...e.dataTransfer.files];
    const slot = files.length === 1 ? view.slotAt(state, e.clientX, e.clientY) : null;
    if (slot) fillFromFile(slot, files[0]);
    else addFiles(files);
  });

  // ============================================================ document operations

  function select(uid) {
    selectedUid = uid;
    renderSidebar();
    renderInspector();
    requestRender();
  }

  /** After any edit: panels + canvas + export preview. `panels=false` during a drag. */
  function changed(panels = true) {
    if (panels) { renderSidebar(); renderInspector(); }
    requestRender();
    refreshExport();
  }

  async function addFiles(files) {
    const images = files.filter((f) => f.type.startsWith('image/'));
    if (!images.length) return;
    setStatus(`Cargando ${images.length} imagen(es)…`);
    const wasEmpty = !state.parts.some((p) => p.image);
    const filled = [];
    let removed = 0, last = null;
    for (const file of images) {
      const res = await M.importImage(file);
      if (!res) { setStatus(`"${file.name}" no tiene nada visible.`); continue; }
      if (res.removedBg) removed++;
      const slot = matchSlot(state, file.name);   // a template slot this file names
      if (slot) {
        fillSlot(state, slot, res.image);
        filled.push(slot.slot.label);
        last = slot;
        continue;
      }
      const part = createPart({ name: M.uniqueName(state, file.name.replace(/\.[^.]+$/, '')), image: res.image, index: state.parts.length });
      part.position = M.initialPosition(state, res.image, part.pivot);
      state.parts.push(part);
      last = part;
    }
    if (last) selectedUid = last.uid;
    const fitted = fitTemplate();
    if (wasEmpty && !fitted) { if (!state.template) fitCollider(); view.frame(state); }
    changed();
    if (!fitted) setStatus(`${images.length} pieza(s) añadida(s)${filled.length ? ` (en su hueco: ${filled.join(', ')})` : ''}${removed ? `, fondo quitado en ${removed}` : ''}. Los PNG del mismo lienzo quedan ya colocados.`);
  }

  // ============================================================ templates

  /** Starts a new document from a body template: its slots become empty parts to drop art on. */
  function newFromTemplate(id, take = templateTake) {
    const template = findTemplate(id);
    if (!template) return;
    if (state.parts.length && !confirm(`¿Empezar "${template.label}" desde cero? Lo no guardado se pierde.`)) return;
    const ppu = state.pixelsPerUnit;
    state = instantiateTemplate(template, take);
    state.pixelsPerUnit = ppu;
    resetGestureUi();
    currentId = null;
    behaviour = null;
    selectedUid = openSlots(state, true)[0]?.uid || null;
    changed();
    renderToolbar();
    view.frame(state);
    setStatus(`Plantilla "${template.label}": suelta cada PNG sobre su hueco (lista de piezas o lienzo). Todo se puede cambiar después.`);
  }

  /** A built-in template or one saved by the user (library, kind 'rig-template'). */
  function findTemplate(id) {
    return templateById(id) || userTemplates.find((t) => t.id === id) || null;
  }

  const bodyHeight = () => { const b = view.bounds(state); return b ? b.maxY - b.minY : 0; };

  /**
   * "Guardar como plantilla": this body's shape (slots, roles, pivots, sway), its gestures by slot
   * and its behaviour, as a library record of kind 'rig-template'. Same name = that one is replaced.
   */
  async function saveAsTemplate(label) {
    const name = (label || state.name).trim();
    if (!name) { setStatus('Ponle nombre a la plantilla.'); return; }
    if (!state.parts.length) { setStatus('No hay cuerpo que guardar: añade piezas primero.'); return; }
    let behaviourCfg = null;
    if (state.kind === 'enemy') {
      if (hooks.beforeExport) await hooks.beforeExport();
      behaviourCfg = (currentId && (await getEnemy(currentId))?.enemyConfig) || suggestedEnemyConfig(state);
    }
    const template = templateFromState(state, { label: name, bodyHeight: bodyHeight(), behaviour: behaviourCfg });
    const existing = userTemplates.find((t) => t.label === name);
    if (existing && !confirm(`Ya hay una plantilla "${name}". ¿Sustituirla?`)) return;
    const patch = { enemyName: name, thumbnail: await thumbnail(), config: null, draft: template };
    try {
      const record = existing ? await updateEnemy(existing.recordId, patch) : await saveEnemy({ kind: 'rig-template', sprite: null, ...patch });
      await refreshLibraries();
      templateChoice = templateFromRecord(record).id;
      renderSidebar();
      setStatus(`Plantilla "${name}" guardada: ${template.slots.filter((x) => !x.node).length} huecos, ${template.gestures.length} gestos${template.behaviour ? ', con comportamiento' : ''}. Está en "Nuevo desde plantilla" y en 📚 Biblioteca.`);
    } catch (e) { setStatus(`No se pudo guardar la plantilla: ${e.message}`); }
  }

  /** The chosen template's gestures onto the body already on screen (matched by slot → role → name). */
  function importTemplateGestures(id) {
    const template = findTemplate(id);
    if (!template?.gestures?.length) { setStatus('Esa plantilla no tiene gestos.'); return; }
    const res = importGestures(state, template, bodyHeight());
    resetGestureUi();
    changed();
    setStatus(`Gestos de "${template.label}": ${res.added} nuevos, ${res.replaced} sustituidos${res.dropped.length ? `. Sin pieza equivalente aquí (se omiten sus poses): ${res.dropped.join(', ')}` : ''}.`);
  }

  async function deleteUserTemplate(template) {
    if (!template?.recordId || !confirm(`¿Borrar la plantilla "${template.label}"? Los enemigos ya hechos con ella no cambian.`)) return;
    await deleteEnemy(template.recordId);
    templateChoice = RIG_TEMPLATES[0].id;
    await refreshLibraries();
    setStatus(`Plantilla "${template.label}" borrada.`);
  }

  /** One image into a part: a template slot gets its guesses (pivot, boca, agarre…), any other part just the art. */
  async function fillFromFile(part, file) {
    const res = await M.importImage(file);
    if (!res) { setStatus(`"${file.name}" no tiene nada visible.`); return; }
    if (M.isOpenSlot(part)) {
      fillSlot(state, part, res.image);
      selectedUid = part.uid;
      const fitted = fitTemplate();
      changed();
      if (!fitted) setStatus(`"${part.slot.label}" puesto${res.removedBg ? ' (fondo quitado)' : ''}: pivote${part.uid === state.roles.aim ? ' y boca' : ''} estimados, corrígelos con sus herramientas si hace falta.`);
      return;
    }
    part.image = res.image;
    if (!part.pivot || part.pivot.x === 0) part.pivot = { x: res.image.cropX + res.image.w / 2, y: res.image.cropY + res.image.h / 2 };
    changed();
  }

  /**
   * Once the last required slot is filled: the template's lengths (sway, hover, recoil, gestures)
   * are rescaled to the real size of the art, and the collider wraps it. Runs once per document.
   */
  function fitTemplate(force = false) {
    if (!state.template || (state.template.fitted && !force) || openSlots(state, true).length) return false;
    const b = view.bounds(state);
    if (!b) return false;
    const k = fitLengths(state, b.maxY - b.minY);
    if (!force) { centerOrigin(); fitCollider(); view.frame(state); }
    setStatus(`Plantilla completa: amplitudes (balanceo, flotar, retroceso, gestos) ajustadas ×${Math.round(k * 100) / 100} al tamaño del dibujo${force ? '' : ' y collider ajustado'}.`);
    return true;
  }

  function addEmptyPart() {
    const part = createPart({ name: M.uniqueName(state, 'Pivote'), index: state.parts.length });
    part.rigPart = false;
    const sel = selectedPart();
    if (sel) part.parent = sel.uid;
    state.parts.push(part);
    selectedUid = part.uid;
    changed();
    setStatus('Pieza vacía: un pivote para colgar otras (p. ej. el cuello de una torreta).');
  }

  function removePart(part) {
    if (!part) return;
    M.childrenOf(state, part.uid).forEach((c) => M.reparent(state, c, part.parent));
    state.parts = state.parts.filter((p) => p !== part);
    const r = state.roles;
    if (r.aim === part.uid) { r.aim = null; r.muzzle = null; }
    if (r.stretch === part.uid) r.stretch = null;
    r.riders = r.riders.filter((u) => u !== part.uid);
    r.limbs = r.limbs.filter((u) => u !== part.uid);
    r.orbiters = r.orbiters.filter((u) => u !== part.uid);
    delete r.grips[part.uid];
    G.dropPartFromGestures(state, part.uid);
    if (state.bossBody.bodyRenderer === part.uid) state.bossBody.bodyRenderer = null;
    if (selectedUid === part.uid) selectedUid = null;
    changed();
  }

  /** No motion of its own: no sway, no scale pulse, no lean follow, and out of every gesture pose. Returns the poses removed. */
  function makeStill(part) {
    part.sway.move = { x: 0, y: 0 };
    part.sway.rotate = 0;
    part.sway.scale = { x: 0, y: 0 };
    part.leanFollow = 0;
    let removed = 0;
    (state.gestures || []).forEach((g) => g.steps.forEach((_, i) => { removed += G.removePose(g, i, part.uid); }));
    return removed;
  }

  function moveInOrder(part, delta) {
    const i = state.parts.indexOf(part);
    const j = i + delta;
    if (j < 0 || j >= state.parts.length) return;
    [state.parts[i], state.parts[j]] = [state.parts[j], state.parts[i]];
    changed();
  }

  /**
   * Moves the whole body so its centre sits on the origin (= the prefab root). The root of an enemy
   * or boss is the centre of its body, not its feet: ranges and detection are measured from it;
   * standing on the ground is the collider's job (enemy) or the boss's feet point (root.feet).
   */
  function centerOrigin() {
    const b = view.bounds(state);
    if (!b) return false;
    const c = { x: (b.minX + b.maxX) / 2, y: (b.minY + b.maxY) / 2 };
    const r2 = (v) => Math.round(v * 100) / 100;
    state.parts.forEach((p) => { if (!p.parent) p.position = { x: p.position.x - c.x, y: p.position.y - c.y }; });
    state.collider.offset = { x: r2(state.collider.offset.x - c.x), y: r2(state.collider.offset.y - c.y) };
    if (typeof state.feetY === 'number') state.feetY = r2(state.feetY - c.y);
    return true;
  }

  /** Collider = the box around every drawn part (rest pose). */
  function fitCollider() {
    const b = view.bounds(state);
    if (!b) return;
    state.collider.size = { x: Math.round((b.maxX - b.minX) * 100) / 100, y: Math.round((b.maxY - b.minY) * 100) / 100 };
    state.collider.offset = { x: Math.round(((b.minX + b.maxX) / 2) * 100) / 100, y: Math.round(((b.minY + b.maxY) / 2) * 100) / 100 };
  }

  // ============================================================ sidebar (document + parts list)

  function renderSidebar() {
    sidebar.innerHTML = '';

    const doc = el('div', 'rigBlock');
    doc.appendChild(F.textField({ label: 'Nombre (prefab)', value: state.name, placeholder: 'ej. TorretaHielo',
      onChange: (v) => { state.name = v; refreshExport(); } }).row);
    doc.appendChild(F.enumField({ label: 'Tipo', value: state.kind,
      options: [{ value: 'enemy', label: 'Enemigo' }, { value: 'boss', label: 'Jefe' }],
      onChange: (v) => { state.kind = v; changed(); } }).row);
    doc.appendChild(F.numberField({ label: 'Escala en juego', value: state.rootScale, min: 0.01,
      hint: 'Escala de la raíz del prefab: 0.5 = la mitad del tamaño del dibujo.',
      onChange: (v) => { state.rootScale = v > 0 ? v : 1; refreshExport(); } }).row);
    doc.appendChild(F.numberField({ label: 'Píxeles por unidad', value: state.pixelsPerUnit, min: 1,
      hint: 'De los PNG. Cambiarlo cambia el tamaño de todo; mejor fíjalo antes de colocar.',
      onChange: (v) => { state.pixelsPerUnit = v > 0 ? v : 100; requestRender(); refreshExport(); } }).row);
    sidebar.appendChild(doc);

    const add = el('div', 'rigBlock');
    const input = document.createElement('input');
    input.type = 'file'; input.accept = 'image/*'; input.multiple = true; input.hidden = true;
    input.onchange = () => { addFiles([...input.files]); input.value = ''; };
    const addBtn = el('button', 'primary', '＋ Añadir piezas (PNG)…');
    addBtn.style.width = '100%';
    addBtn.onclick = () => input.click();
    const emptyBtn = el('button', 'small', '＋ Pieza vacía (pivote)');
    emptyBtn.onclick = addEmptyPart;
    add.append(input, addBtn, emptyBtn);
    add.appendChild(el('p', 'hint', 'También puedes arrastrar los PNG al lienzo. El fondo liso se quita solo y cada pieza se recorta.'));
    sidebar.appendChild(add);

    sidebar.appendChild(el('h2', null, 'Piezas'));
    sidebar.appendChild(el('p', 'hint', 'Arriba = delante. Arrastra una fila sobre otra para colgarla de ella; sobre "Visual" para soltarla.'));
    const list = el('div', 'rigPartList');

    const rootRow = el('div', 'rigPartRow rigRootRow', 'Visual (raíz)');
    wireDrop(rootRow, null);
    list.appendChild(rootRow);

    const depth = (p) => { let d = 0; for (let q = M.byUid(state, p.parent); q; q = M.byUid(state, q.parent)) d++; return d; };
    [...state.parts].reverse().forEach((part) => {
      const open = M.isOpenSlot(part);
      const row = el('div', `rigPartRow${part.uid === selectedUid ? ' selected' : ''}${open ? ' rigSlotOpen' : ''}${open && part.slot.optional ? ' optional' : ''}`);
      row.draggable = true;
      row.style.paddingLeft = `${8 + depth(part) * 14}px`;
      const name = el('span', 'rigPartName', open
        ? `${part.parent ? '↳ ' : ''}◇ ${part.slot.label}${part.slot.optional ? ' (opcional)' : ''}`
        : `${part.parent ? '↳ ' : ''}${part.name}${part.image ? '' : ' ◌'}`);
      if (part.slot) row.title = `${part.slot.label}: ${part.slot.hint}`;
      row.appendChild(name);
      const badges = roleBadges(part);
      if (badges.length) row.appendChild(el('span', 'rigPartRoles', badges.join(' · ')));
      const btns = el('span', 'rigPartBtns');
      const up = el('button', 'small', '↑'); up.title = 'Más delante'; up.onclick = (e) => { e.stopPropagation(); moveInOrder(part, +1); };
      const down = el('button', 'small', '↓'); down.title = 'Más detrás'; down.onclick = (e) => { e.stopPropagation(); moveInOrder(part, -1); };
      btns.append(up, down);
      row.appendChild(btns);
      row.onclick = () => select(part.uid);
      row.addEventListener('dragstart', (e) => { e.dataTransfer.setData('text/rig-part', part.uid); });
      wireDrop(row, part.uid);
      // A PNG dropped on the row goes into this part (a template slot: with its pivot guess).
      row.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('Files')) { e.preventDefault(); row.classList.add('dropTarget'); } });
      row.addEventListener('drop', (e) => {
        const file = [...(e.dataTransfer.files || [])].find((f) => f.type.startsWith('image/'));
        if (!file) return;
        e.preventDefault();
        row.classList.remove('dropTarget');
        fillFromFile(part, file);
      });
      list.appendChild(row);
      if (open) {
        const hint = el('div', 'rigSlotHint', `${part.slot.hint} Suelta aquí su PNG.`);
        hint.style.paddingLeft = row.style.paddingLeft;
        list.appendChild(hint);
      }
    });
    sidebar.appendChild(list);
    renderTemplateBlock();
  }

  let templateChoice = RIG_TEMPLATES[0].id;
  const templateTake = { gestures: true, behaviour: true };   // what "Nuevo desde plantilla" brings along
  let templateName = '';                                      // "Guardar como plantilla": empty = the rig's name

  /** Templates (own host): the one in use, "Nuevo desde plantilla" (built-in + saved), "Guardar como plantilla". */
  function renderTemplateBlock() {
    templateHost.innerHTML = '';
    const block = el('div', 'rigBlock rigTemplateBlock');
    if (state.template) {
      const label = state.template.label || findTemplate(state.template.id)?.label || 'plantilla';
      const slots = state.parts.filter((p) => p.slot);
      const open = openSlots(state), required = openSlots(state, true);
      block.appendChild(el('p', required.length ? 'rigTemplateInfo' : 'efSummaryOk',
        `Plantilla: ${label} — ${slots.length - open.length}/${slots.length} huecos${required.length ? `. Faltan: ${required.map((p) => p.slot.label).join(', ')}` : ''}`));
      const refit = el('button', 'small', '⤢ Reajustar amplitudes al tamaño');
      refit.title = 'Si has cambiado mucho el tamaño del cuerpo: reescala balanceos, flotar, retroceso y gestos a su altura actual.';
      refit.onclick = () => { if (fitTemplate(true)) changed(); else setStatus('Rellena antes los huecos que faltan.'); };
      block.appendChild(refit);
    }

    if (!findTemplate(templateChoice)) templateChoice = RIG_TEMPLATES[0].id;
    const kindLabel = (t) => (t.kind === 'boss' ? 'jefe' : 'enemigo');
    // The gallery: your saved templates first (with their thumbnail), then the built-in shapes.
    block.appendChild(el('p', 'rigGroupTitle', 'Nuevo desde plantilla'));
    const gallery = el('div', 'rigTplGallery');
    [...userTemplates, ...RIG_TEMPLATES].forEach((t) => {
      const card = el('button', `rigTplCard${t.id === templateChoice ? ' selected' : ''}`);
      card.dataset.template = t.id;
      if (t.thumbnail) { const img = el('img'); img.src = t.thumbnail; img.alt = ''; card.appendChild(img); }
      else card.appendChild(el('span', 'rigTplIcon', t.kind === 'boss' ? '👑' : t.user ? '★' : '🧬'));
      card.appendChild(el('span', 'rigTplName', `${t.user ? '★ ' : ''}${t.label}`));
      card.appendChild(el('span', 'rigTplMeta', `${kindLabel(t)} · ${t.slots.filter((x) => !x.node).length} piezas · ${(t.gestures || []).length} gestos`));
      card.onclick = () => { templateChoice = t.id; renderSidebar(); };
      gallery.appendChild(card);
    });
    block.appendChild(gallery);
    const chosen = findTemplate(templateChoice);
    if (chosen.shape) block.appendChild(el('p', 'hint', chosen.shape));
    if (chosen.fits) block.appendChild(el('p', 'hint', `Sirve para: ${chosen.fits}`));
    if (chosen.user) block.appendChild(el('p', 'hint', `Tuya: ${chosen.slots.filter((x) => !x.node).length} huecos · ${chosen.gestures.length} gestos${chosen.behaviour ? ` · ${chosen.behaviour.summary}` : ''}.`));

    const hasBehaviour = chosen.kind === 'enemy' && !!(chosen.behaviour?.tuning || chosen.behaviour?.config);
    if (chosen.gestures?.length) block.appendChild(F.boolField({ label: `Con sus gestos (${chosen.gestures.length})`, value: templateTake.gestures,
      onChange: (v) => { templateTake.gestures = v; } }).row);
    if (hasBehaviour) block.appendChild(F.boolField({ label: 'Con su comportamiento', value: templateTake.behaviour,
      hint: chosen.behaviour.summary || '', onChange: (v) => { templateTake.behaviour = v; } }).row);

    const go = el('button', 'small primary', `＋ Nuevo: ${chosen.label}`);
    go.onclick = () => newFromTemplate(templateChoice);
    block.appendChild(go);

    if (chosen.gestures?.length || chosen.user) {
      const more = el('details', 'efFold');
      more.appendChild(el('summary', null, 'Más'));
      if (chosen.gestures?.length) {
        const bring = el('button', 'small', '⇣ Traer sólo sus gestos a este cuerpo');
        bring.title = 'No toca las piezas: copia los gestos de la plantilla emparejando cada pieza por su hueco, su rol (la que apunta, extremidades…) o su nombre. Un gesto con el mismo nombre se sustituye.';
        bring.disabled = !state.parts.length;
        bring.onclick = () => importTemplateGestures(templateChoice);
        more.appendChild(bring);
      }
      if (chosen.user) {
        const del = el('button', 'small danger', '🗑 Borrar esta plantilla');
        del.onclick = () => deleteUserTemplate(chosen);
        more.appendChild(del);
      }
      block.appendChild(more);
    }
    templateHost.appendChild(block);

    const save = el('div', 'rigBlock rigTemplateBlock');
    save.appendChild(F.textField({ label: 'Guardar como plantilla', value: templateName, placeholder: state.name.trim() || 'nombre de la plantilla',
      hint: 'Guarda la forma del cuerpo (piezas, roles, pivotes, balanceo), sus gestos y su comportamiento. El arte no: otro enemigo pone el suyo.',
      onChange: (v) => { templateName = v; } }).row);
    const saveBtn = el('button', 'small', '💾 Guardar como plantilla');
    saveBtn.disabled = !state.parts.length;
    saveBtn.onclick = () => saveAsTemplate(templateName);
    save.appendChild(saveBtn);
    templateHost.appendChild(save);
  }

  function wireDrop(row, parentUid) {
    row.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('text/rig-part')) { e.preventDefault(); row.classList.add('dropTarget'); } });
    row.addEventListener('dragleave', () => row.classList.remove('dropTarget'));
    row.addEventListener('drop', (e) => {
      row.classList.remove('dropTarget');
      const uid = e.dataTransfer.getData('text/rig-part');
      const part = M.byUid(state, uid);
      if (!part || uid === parentUid) return;
      e.preventDefault();
      if (!M.reparent(state, part, parentUid)) { setStatus('No se puede colgar una pieza de su propio hijo.'); return; }
      changed();
    });
  }

  function roleBadges(part) {
    const r = state.roles, out = [];
    if (r.aim === part.uid) out.push(ROLE_LABELS.aim);
    if (r.stretch === part.uid) out.push(ROLE_LABELS.stretch);
    if (r.riders.includes(part.uid)) out.push(ROLE_LABELS.rider);
    if (r.limbs.includes(part.uid)) out.push(ROLE_LABELS.limb);
    if (r.orbiters.includes(part.uid)) out.push(ROLE_LABELS.orbiter);
    if (state.bossBody.bodyRenderer === part.uid) out.push(ROLE_LABELS.body);
    if (!part.rigPart) out.push('fija');
    return out;
  }

  // ============================================================ inspector

  function section(title, open = true, host = inspector) {
    const d = el('details', 'rigSection');
    d.open = open;
    d.appendChild(el('summary', null, title));
    host.appendChild(d);
    return d;
  }

  const openSections = new Map();
  function rememberedSection(key, title, def, host = inspector) {
    const d = section(title, openSections.has(key) ? openSections.get(key) : def, host);
    d.addEventListener('toggle', () => openSections.set(key, d.open));
    return d;
  }

  function renderInspector() {
    // The hosts do not scroll themselves: the column they sit in does (it differs per tab).
    const scroller = inspector.closest('.scrollHost') || inspector;
    const scrollTop = scroller.scrollTop;
    inspector.innerHTML = '';
    statesHost.innerHTML = '';
    linksHost.innerHTML = '';
    exportHost.innerHTML = '';
    const part = selectedPart();
    if (part) renderPartInspector(part);
    else inspector.appendChild(el('p', 'hint', 'Selecciona una pieza (clic en el lienzo o en la lista).'));
    renderRigSection();
    renderGesturesSection();
    renderBodySection();
    renderStatesBoard();
    renderLinksSection();
    renderExportSection();
    scroller.scrollTop = scrollTop;
    renderTimeline();
  }

  /** The open gesture's timeline, docked under the canvas (hidden while none is open). */
  function renderTimeline() {
    const hidden = !openGesture();
    if (timelineHost.hidden !== hidden) { timelineHost.hidden = hidden; requestRender(); }   // the canvas takes / gives the room
    timeline.renderDock(timelineHost);
  }

  function renderPartInspector(part) {
    const s = rememberedSection('part', `Pieza: ${part.name}`, true);

    if (part.slot) {
      const open = M.isOpenSlot(part);
      const extra = [part.slot.states && 'sprites dormido / despierto / atacando', part.slot.stage && 'una etapa para la fase 2'].filter(Boolean);
      s.appendChild(el('p', open ? 'rigTemplateInfo' : 'hint',
        `${open ? '◇ Hueco de la plantilla — ' : 'Plantilla — '}${part.slot.label}: ${part.slot.hint}${extra.length && !open ? ` Admite además: ${extra.join(', ')} (abajo, "Sprites por estado / etapa").` : ''}`));
    }

    const nameField = F.textField({ label: 'Nombre', value: part.name, onChange: () => {} });
    nameField.input.addEventListener('change', () => { part.name = M.uniqueName(state, nameField.input.value, part); changed(); });
    s.appendChild(nameField.row);

    const parentOptions = [{ value: '', label: 'Visual (raíz)' }, ...state.parts
      .filter((p) => p !== part && !M.isDescendant(state, p.uid, part.uid))
      .map((p) => ({ value: p.uid, label: M.pathOf(state, p) }))];
    s.appendChild(F.enumField({ label: 'Cuelga de', value: part.parent || '', options: parentOptions,
      hint: 'Se queda donde está en pantalla; sólo cambia de qué pieza depende.',
      onChange: (v) => { M.reparent(state, part, v || null); changed(); } }).row);

    // Fine arrow steps: a whole unit per click is far too coarse to place a part.
    const fine = (row, step = '0.05') => { row.querySelectorAll('input[type=number]').forEach((i) => { i.step = step; }); return row; };
    s.appendChild(fine(F.vector2Field({ label: 'Posición (u, en su padre)', value: part.position,
      onChange: (v) => { part.position = v; requestRender(); refreshExport(); } }).row));
    s.appendChild(F.numberField({ label: 'Giro (°)', value: part.rotation,
      onChange: (v) => { part.rotation = v; requestRender(); refreshExport(); } }).row);
    s.appendChild(fine(F.vector2Field({ label: 'Escala', value: part.scale,
      onChange: (v) => { part.scale = v; requestRender(); refreshExport(); } }).row, '0.01'));
    s.appendChild(F.boolField({ label: 'Parte del rig (se balancea / sale despedida)', value: part.rigPart,
      hint: 'Apagado = sólo existe en la jerarquía (un pivote vacío, una pieza fija).',
      onChange: (v) => { part.rigPart = v; changed(); } }).row);

    const acts = el('div', 'rigActions');
    const mk = (label, title, fn, cls = 'small') => { const b = el('button', cls, label); b.title = title; b.onclick = fn; acts.appendChild(b); return b; };
    const replaceInput = document.createElement('input');
    replaceInput.type = 'file'; replaceInput.accept = 'image/*'; replaceInput.hidden = true;
    replaceInput.onchange = () => { if (replaceInput.files[0]) fillFromFile(part, replaceInput.files[0]); };
    acts.appendChild(replaceInput);
    mk(part.image ? 'Cambiar imagen' : 'Poner imagen', 'Otra imagen para esta pieza (mismo pivote en píxeles del archivo).', () => replaceInput.click());
    if (part.image) {
      mk('Quitar fondo', 'Fuerza quitar el color de fondo (si el automático no lo hizo).', async () => {
        const res = await M.importImage(part.image.dataURL, { removeBg: true });
        if (res) {
          const o = part.image;
          res.image.cropX += o.cropX; res.image.cropY += o.cropY; res.image.origW = o.origW; res.image.origH = o.origH;
          part.image = res.image; changed();
        }
      });
      mk('✂ Dividir en piezas', 'La imagen tiene varias piezas separadas: una pieza por cada una, colocadas donde están.', async () => {
        const parts = await M.splitPart(state, part);
        if (!parts.length) { setStatus('Sólo hay una pieza en esa imagen.'); return; }
        const i = state.parts.indexOf(part);
        removePart(part);                  // sus hijas pasan a su padre, sin moverse
        state.parts.splice(i, 0, ...parts);
        selectedUid = parts[0].uid;
        changed();
        setStatus(`Dividida en ${parts.length} piezas.`);
      });
    }
    mk('🗑 Borrar', 'Borra la pieza (sus hijas pasan a su padre).', () => removePart(part), 'small danger');
    s.appendChild(acts);

    // --- roles
    const roles = rememberedSection('roles', 'Roles', true);
    const r = state.roles;
    const roleBox = (label, checked, hint, onChange) => roles.appendChild(F.boolField({ label, value: checked, hint, onChange }).row);
    roleBox('Apunta al objetivo (brazo-arma)', r.aim === part.uid, 'Sólo una. Luego: herramienta Boca y Cañón.', (v) => {
      r.aim = v ? part.uid : (r.aim === part.uid ? null : r.aim);
      if (!v && r.aim === null) r.muzzle = null;
      changed();
    });
    roleBox('Se estira (cuello que se pliega al dormir)', r.stretch === part.uid, 'Sólo una. Se escala en Y desde su pivote.', (v) => {
      r.stretch = v ? part.uid : (r.stretch === part.uid ? null : r.stretch); changed();
    });
    roleBox('Pasajero (viaja en la punta de la que se estira)', r.riders.includes(part.uid), 'Debe colgar del mismo padre que la que se estira.', (v) => {
      r.riders = v ? [...new Set([...r.riders, part.uid])] : r.riders.filter((u) => u !== part.uid); changed();
    });
    if (state.kind === 'boss') {
      roleBox('Extremidad (los ataques de partes la lanzan)', r.limbs.includes(part.uid), 'Orden: la primera marcada = la izquierda.', (v) => {
        r.limbs = v ? [...new Set([...r.limbs, part.uid])] : r.limbs.filter((u) => u !== part.uid);
        if (!v) delete r.grips[part.uid];
        changed();
      });
      roleBox('Orbital (pieza pequeña que se lanza en grupo)', r.orbiters.includes(part.uid), '', (v) => {
        r.orbiters = v ? [...new Set([...r.orbiters, part.uid])] : r.orbiters.filter((u) => u !== part.uid); changed();
      });
      roleBox('Cuerpo (el aura de aviso copia este sprite)', state.bossBody.bodyRenderer === part.uid, 'Normalmente el torso.', (v) => {
        state.bossBody.bodyRenderer = v ? part.uid : null; changed();
      });
    }

    // --- rig part data
    if (part.rigPart) {
      const moves = part.sway.move.x || part.sway.move.y || part.sway.rotate || part.sway.scale.x || part.sway.scale.y || part.leanFollow;
    const d = rememberedSection('sway', `Balanceo y comportamiento${moves ? '' : ' (quieta)'}`, false);
      d.appendChild(F.vector2Field({ label: 'Vaivén (u)', value: part.sway.move, onChange: (v) => { part.sway.move = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Giro del vaivén (°)', value: part.sway.rotate, onChange: (v) => { part.sway.rotate = v; refreshExport(); } }).row);
      d.appendChild(F.vector2Field({ label: 'Pulso de escala', value: part.sway.scale, hint: 'Una llama o un núcleo que late (0 = nada).', onChange: (v) => { part.sway.scale = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Velocidad (rad/s)', value: part.sway.speed, min: 0, onChange: (v) => { part.sway.speed = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Desfase (rad)', value: part.sway.phase, onChange: (v) => { part.sway.phase = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Sigue la inclinación', value: part.leanFollow, hint: 'Positivo = se queda atrás (péndulo); negativo = tiende a vertical.', onChange: (v) => { part.leanFollow = v; refreshExport(); } }).row);
      if (state.roles.limbs.includes(part.uid) || state.roles.orbiters.includes(part.uid))
        d.appendChild(F.numberField({ label: 'Frente al lanzar (°)', value: part.pointAngle, hint: 'Herramienta ➜ Frente al lanzar: sale volando con este lado por delante. 0 = derecha, −90 = abajo.', onChange: (v) => { part.pointAngle = v; requestRender(); refreshExport(); } }).row);
      d.appendChild(F.boolField({ label: 'Sale despedida al morir', value: part.breaksOnDeath, onChange: (v) => { part.breaksOnDeath = v; refreshExport(); } }).row);
      const still = el('button', 'small', '⏹ Dejar quieta (sin movimiento propio)');
      still.title = 'Pone a cero su vaivén, giro, pulso de escala y "sigue la inclinación", y la saca de las poses de todos los gestos: se mueve sólo con la pieza de la que cuelga. Para una pieza de plantilla que en tu enemigo no es una llama ni una cola.';
      still.onclick = () => { const n = makeStill(part); changed(); setStatus(`"${part.name}" queda quieta${n ? ` (quitada de ${n} pose(s) de gestos)` : ''}. Ctrl+Z lo deshace.`); };
      d.appendChild(still);
    }

    // --- state sprites
    if (part.image) renderVariants(part);
  }

  function renderVariants(part) {
    const d = rememberedSection('variants', 'Sprites por estado / etapa', false);
    d.appendChild(el('p', 'hint', 'Mismo tamaño de archivo que la pieza = se alinean solos. Vacío = ese estado no cambia el sprite.'));
    const v = part.variants;
    const slot = (label, get, set) => {
      const row = el('div', 'rigVariantRow');
      row.appendChild(el('span', 'rigVariantLabel', label));
      const img = get();
      if (img) { const t = document.createElement('img'); t.src = img.dataURL; t.className = 'rigVariantThumb'; row.appendChild(t); }
      const input = document.createElement('input');
      input.type = 'file'; input.accept = 'image/*'; input.hidden = true;
      input.onchange = async () => { const f = input.files[0]; if (!f) return; const res = await M.importImage(f); if (res) { set(res.image); changed(); } };
      const pick = el('button', 'small', img ? 'Cambiar' : 'Elegir…');
      pick.onclick = () => input.click();
      row.append(input, pick);
      if (img) { const clr = el('button', 'small', '✕'); clr.onclick = () => { set(null); changed(); }; row.appendChild(clr); }
      row.addEventListener('dragover', (e) => e.preventDefault());
      row.addEventListener('drop', async (e) => { e.preventDefault(); const f = e.dataTransfer.files?.[0]; if (!f) return; const res = await M.importImage(f); if (res) { set(res.image); changed(); } });
      d.appendChild(row);
    };
    M.SPRITE_STATES.forEach((s) => slot(s.label, () => M.variantOf(part, s.id), (x) => M.setVariant(part, s.id, x)));
    (state.gestures || []).forEach((g) => slot(`Gesto: ${g.name}`, () => M.variantOf(part, M.gestureState(g.name)), (x) => M.setVariant(part, M.gestureState(g.name), x)));
    v.stages.forEach((_, i) => slot(`Etapa ${i + 1} (fase ${i + 2})`, () => v.stages[i], (x) => { if (x) v.stages[i] = x; else v.stages.splice(i, 1); }));
    const addStage = el('button', 'small', '＋ Etapa (fase del jefe)');
    addStage.onclick = () => { v.stages.push(null); changed(); };
    d.appendChild(addStage);
  }

  function renderRigSection() {
    const d = rememberedSection('rig', 'Tiempos del rig (ProceduralRig)', false);
    RIG_FIELD_GROUPS.forEach((g) => {
      d.appendChild(el('h3', 'rigGroupTitle', g.title));
      g.fields.forEach(([key, type, , label]) => {
        const val = state.rig[key];
        const set = (x) => { state.rig[key] = x; requestRender(); refreshExport(); };
        if (type === 'vector2') d.appendChild(F.vector2Field({ label, value: val, onChange: set }).row);
        else if (type === 'string') d.appendChild(F.textField({ label, value: val, onChange: set }).row);
        else if (type === 'color') d.appendChild(F.colorField({ label, value: val, onChange: set }).row);
        else if (type === 'layers') d.appendChild(F.textField({ label: `${label} (separadas por coma)`, value: val.join(', '), onChange: (x) => set(x.split(',').map((s) => s.trim()).filter(Boolean)) }).row);
        else d.appendChild(F.numberField({ label, value: val, step: type === 'int' ? '1' : 'any', onChange: set }).row);
      });
    });
    const reset = el('button', 'small', 'Valores por defecto');
    reset.onclick = () => { state.rig = createDefaultRigFields(); changed(); };
    d.appendChild(reset);
  }

  // ============================================================ gestures (timeline host + pose editor)

  const stepLabel = (gesture, step) => gesture.steps[step]?.name || `Paso ${step + 1}`;

  /** The playhead back at the start, the body at rest. */
  function stopHead() {
    head.playing = false;
    head.t = 0;
    head.lead = null;
  }

  /** Which gesture is open (-1 = none): nothing of the previous one stays selected, posing or playing. */
  function openGestureAt(index) {
    if (posing) stopPosing(true);
    gestureIndex = index;
    stopHead();
    placing = null;
    timeline.reset();
  }

  /** `quiet` = whoever calls re-renders. */
  function stopPosing(quiet = false) {
    if (!posing) return;
    const gesture = state.gestures?.[posing.index];
    if (gesture) G.pruneGesture(gesture);
    posing = null;
    if (quiet) return;
    changed();
    setStatus('Fuera del modo pose: las herramientas vuelven a mover las piezas.');
  }

  function startPosing(index, step) {
    const gesture = state.gestures?.[index];
    if (!gesture?.steps[step]) return;
    if (posing) G.pruneGesture(gesture);
    head.playing = false;
    posing = { index, step };
    gestureIndex = index;
    if (!POSE_TOOLS.includes(tool)) { tool = 'move'; renderToolbar(); }
    changed();
    setStatus(`Posando "${stepLabel(gesture, step)}" de "${gesture.name}": arrastra una pieza (Mover / Rotar / Escalar). En vacío = el cuerpo entero. Esc = salir.`);
  }

  /** "▶ Probar" of the dock: plays from the playhead (from the start if it is at the end), or pauses. */
  function togglePlay() {
    const gesture = openGesture();
    if (!gesture) return;
    if (head.playing) { head.playing = false; timeline.updatePlayhead(); requestRender(); return; }
    const wasPosing = !!posing;
    if (wasPosing) stopPosing(true);
    if (head.t >= G.gestureDuration(gesture, lead()) - 1e-3) head.t = 0;
    head.playing = true;
    if (wasPosing) renderInspector(); else timeline.updatePlayhead();
    startTicking();
  }

  /** Opens a gesture and plays it from the start. `withLead` = a warning only for this play (an idle gesture's). */
  function playGesture(index, withLead = null) {
    if (!state.gestures?.[index]) return;
    if (gestureIndex !== index) openGestureAt(index);
    else if (posing) stopPosing(true);
    head.t = 0;
    head.lead = withLead;
    head.playing = true;
    renderInspector();
    startTicking();
  }

  /** Takes `target` (part uid or the body) out of the pose being edited. */
  function clearPose(target) {
    const gesture = posing && state.gestures?.[posing.index];
    if (!gesture) return;
    G.removePose(gesture, posing.step, target);
    changed();
  }

  const targetName = (target) => (target === G.BODY ? 'Cuerpo entero' : M.byUid(state, target)?.name || '(pieza borrada)');

  /** Gestures of a rig-config.json (what Unity's Tools ▸ Web ▸ Rig exports) onto this body, by part path. */
  async function importGestureFile(file) {
    let doc;
    try { doc = JSON.parse(await file.text()); } catch (e) { setStatus(`"${file.name}" no es un JSON válido.`); return; }
    const res = readRigGestures(doc, state, { entriesById: fxEntries });
    if (!res.gestures.length) { setStatus(`"${file.name}" no trae gestos (¿es un rig-config.json?).`); return; }
    const list = state.gestures || (state.gestures = []);
    let added = 0, replaced = 0;
    res.gestures.forEach((g) => {
      const i = list.findIndex((x) => x.name === g.name);
      if (i >= 0) { list[i] = g; replaced++; } else { list.push(g); added++; }
    });
    if (res.idle) state.idle = { ...state.idle, ...res.idle };
    resetGestureUi();
    changed();
    setStatus(`Gestos de "${file.name}": ${added} nuevos, ${replaced} sustituidos.${res.notes.length ? ` ${res.notes.join(' ')}` : ''}`);
  }

  function renderGesturesSection() {
    const list = state.gestures || [];
    if (posing && !list[posing.index]?.steps[posing.step]) posing = null;
    if (gestureIndex >= list.length) openGestureAt(list.length - 1);

    const d = rememberedSection('gestures', `Gestos (${list.length})`, true);
    d.appendChild(el('p', 'hint', 'Un gesto es el aviso de un ataque, como línea de tiempo: los PASOS que recorre el cuerpo (cada uno una pose y un tiempo), las ◆ SUELTAS (cuándo y de dónde sale el ataque) y sus ✨ efectos. '
      + 'Cada ataque elige el suyo por nombre (Enemy Creator ▸ Ataques, Boss Creator). Al abrir uno, su línea de tiempo aparece bajo el lienzo.'));

    if (state.gestures === null) {
      d.appendChild(el('p', 'hint', 'Este documento no exporta gestos: Unity conserva los que tenga el prefab. Al crear uno aquí, los de aquí sustituyen a los del prefab.'));
    }

    list.forEach((gesture, index) => {
      const open = index === gestureIndex;
      const card = el('div', `rigGesture${open ? ' open' : ''}${posing?.index === index ? ' posing' : ''}`);
      const row = el('div', 'rigGestureHead');
      const title = el('span', 'rigGestureName', `${open ? '▾' : '▸'} ${gesture.name}`);
      title.onclick = () => { openGestureAt(open ? -1 : index); renderInspector(); requestRender(); if (hooks.onChange) hooks.onChange(); };
      row.appendChild(title);
      row.appendChild(el('span', 'hint', `${gesture.steps.length} paso${gesture.steps.length === 1 ? '' : 's'} · ◆ ${G.effectiveReleases(gesture).length}${gesture.fx.length ? ` · ✨ ${gesture.fx.length}` : ''}${state.idle.gestures.includes(gesture.name) ? ' · 💤' : ''}`));
      const play = el('button', 'small', '▶');
      play.dataset.action = 'gesturePlay';              // Space plays the open gesture's dock (app.js ▸ playActions); else the first of these
      play.dataset.nodirty = '1';
      play.title = 'Abre el gesto y lo reproduce con los tiempos del juego: sus pasos y la vuelta al reposo.';
      play.onclick = () => playGesture(index);
      row.appendChild(play);
      card.appendChild(row);

      if (open) renderGestureCard(card, gesture, index);
      // An effect dragged from Enemy Studio ▸ Efectos lands on the gesture it is dropped on (on the timeline: at that instant).
      card.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('text/rig-fx')) { e.preventDefault(); card.classList.add('dropTarget'); } });
      card.addEventListener('dragleave', () => card.classList.remove('dropTarget'));
      card.addEventListener('drop', (e) => {
        const id = e.dataTransfer.getData('text/rig-fx');
        if (!id) return;
        e.preventDefault();
        if (gestureIndex !== index) openGestureAt(index);
        if (timeline.addFx(id)) setStatus(`Efecto añadido a "${gesture.name}". Muévelo por la línea de tiempo y colócalo en el lienzo.`);
      });
      d.appendChild(card);
    });

    const acts = el('div', 'rigActions');
    const add = el('button', 'small primary', '＋ Nuevo gesto');
    add.onclick = () => {
      if (!state.gestures) state.gestures = [];
      state.gestures.push(G.createGesture(state));
      openGestureAt(state.gestures.length - 1);
      timeline.select('step', 0);
      changed();
    };
    acts.appendChild(add);

    const bring = el('button', 'small', '⬆ Traer de un rig-config.json');
    bring.title = 'Los gestos de un rig-config.json exportado por Unity (Tools ▸ Web ▸ Rig) o por esta web, sobre este cuerpo: las piezas se buscan por su nombre y de quién cuelgan. Un gesto con el mismo nombre se sustituye.';
    const file = document.createElement('input');
    file.type = 'file'; file.accept = '.json,application/json'; file.hidden = true;
    file.onchange = () => { if (file.files[0]) importGestureFile(file.files[0]); file.value = ''; };
    bring.onclick = () => file.click();
    acts.append(bring, file);

    if (state.gestures) {
      const drop = el('button', 'small danger', 'No exportar gestos (conservar los de Unity)');
      drop.title = 'Borra los gestos de este documento: al importar, Unity se queda con los que ya tenga el prefab.';
      drop.onclick = () => {
        if (list.length && !confirm('¿Borrar los gestos de este documento? (Ctrl+Z los recupera)')) return;
        resetGestureUi();
        state.gestures = null;
        changed();
      };
      acts.appendChild(drop);
    }
    d.appendChild(acts);

    if (state.idle.gestures.some((n) => list.some((g) => g.name === n))) {
      const idle = el('details', 'efFold');
      idle.open = openSections.get('idle') ?? true;
      idle.addEventListener('toggle', () => openSections.set('idle', idle.open));
      idle.appendChild(el('summary', null, `💤 En reposo (${state.idle.gestures.filter((n) => list.some((g) => g.name === n)).join(', ')})`));
      const grid = el('div', 'rigGestureGrid');
      grid.appendChild(F.numberField({ label: 'Cada (s)', value: state.idle.interval, min: 0.2, step: '0.5',
        hint: 'Más o menos: varía un ±25%.', onChange: (v) => { state.idle.interval = Math.max(0.2, v); refreshExport(); } }).row);
      grid.appendChild(F.numberField({ label: 'Probabilidad (0–1)', value: state.idle.chance, min: 0, step: '0.1',
        hint: 'De hacerlo en cada intento.', onChange: (v) => { state.idle.chance = Math.min(1, Math.max(0, v)); refreshExport(); } }).row);
      grid.appendChild(F.numberField({ label: 'Aviso (s)', value: state.idle.windup, min: 0.05, step: '0.1',
        hint: 'Lo que duran sus pasos marcados "dura el aviso" (en un ataque es el aviso del ataque).', onChange: (v) => { state.idle.windup = Math.max(0.05, v); refreshExport(); } }).row);
      idle.appendChild(grid);
      const tryIdle = el('button', 'small', '▶ Probar uno');
      tryIdle.dataset.nodirty = '1';
      tryIdle.title = 'Reproduce uno de los gestos de reposo al azar, con ese aviso.';
      tryIdle.onclick = () => {
        const names = state.idle.gestures.filter((n) => list.some((g) => g.name === n));
        playGesture(list.findIndex((g) => g.name === names[Math.floor(Math.random() * names.length)]), state.idle.windup);
      };
      idle.appendChild(tryIdle);
      d.appendChild(idle);
    }
  }

  function renderGestureCard(card, gesture, index) {
    const nameField = F.textField({ label: 'Nombre', value: gesture.name,
      hint: 'Es lo que escribe el ataque en su campo "gesto". Cambiarlo no actualiza los ataques que ya lo usaban.', onChange: () => {} });
    nameField.input.addEventListener('change', () => {
      const old = gesture.name;
      gesture.name = G.uniqueGestureName(state, nameField.input.value, gesture);
      if (state.rig.moveGesture === old) state.rig.moveGesture = gesture.name;
      M.renameGestureSprites(state, old, gesture.name);
      state.idle.gestures = state.idle.gestures.map((n) => (n === old ? gesture.name : n));
      if (preview.spriteState === M.gestureState(old)) preview.spriteState = M.gestureState(gesture.name);
      changed();
    });
    card.appendChild(nameField.row);

    // What is selected on the timeline (under the canvas): a step and its pose, a release, an effect.
    timeline.renderFields(card);
    renderGestureSprites(card, gesture);

    card.appendChild(F.boolField({ label: '💤 Gesto de reposo (lo hace solo de vez en cuando)', value: state.idle.gestures.includes(gesture.name),
      hint: 'Sin objetivo, quieto y despierto. No avisa de nada ni lanza ningún ataque (sus sueltas no cuentan): sólo da vida. Cada cuánto, abajo.',
      onChange: (v) => {
        state.idle.gestures = v ? [...new Set([...state.idle.gestures, gesture.name])] : state.idle.gestures.filter((n) => n !== gesture.name);
        changed();
      } }).row);

    if (state.rig.moveGesture === gesture.name) card.appendChild(el('p', 'hint', 'Es el gesto al desplazarse (Tiempos del rig ▸ moveGesture): sólo usa la pose de su primer paso, mantenida mientras se mueve.'));

    const more = el('div', 'rigActions');
    const dup = el('button', 'small', '⧉ Duplicar');
    dup.onclick = () => { state.gestures.splice(index + 1, 0, G.duplicateGesture(state, gesture)); openGestureAt(index + 1); changed(); };
    const del = el('button', 'small danger', '🗑 Borrar gesto');
    del.onclick = () => {
      M.renameGestureSprites(state, gesture.name, null);
      state.idle.gestures = state.idle.gestures.filter((n) => n !== gesture.name);
      if (preview.spriteState === M.gestureState(gesture.name)) preview.spriteState = '';
      resetGestureUi();
      state.gestures.splice(index, 1);
      changed();
    };
    more.append(dup, del);
    card.appendChild(more);
  }

  /**
   * The sprites this gesture changes (one per part that has one) and WHEN each shows inside it: all
   * of it by default, or a window — "stretch the hand, show the finger, hide it, pull it back".
   */
  function renderGestureSprites(card, gesture) {
    const stateId = M.gestureState(gesture.name);
    const mine = state.parts.filter((p) => p.image && M.variantOf(p, stateId));
    const box = el('div', 'rigFxBox');
    box.appendChild(el('p', 'rigGroupTitle', 'Sprites de este gesto'));
    if (!mine.length) {
      box.appendChild(el('p', 'hint', 'Ninguna pieza cambia de sprite en este gesto. Se asignan en "Sprites por estado" (estado "gesto: ' + gesture.name + '").'));
      card.appendChild(box);
      return;
    }
    const steps = gesture.steps.length;
    const moments = M.gestureMoments(gesture);
    const momentSelect = (value, onChange) => {
      const s = document.createElement('select');
      const options = moments.some(([m]) => Math.abs(m - value) < 1e-3) ? moments : [...moments, [value, `${value}`]];
      options.forEach(([m, label]) => { const o = el('option', null, label); o.value = String(m); s.appendChild(o); });
      s.value = String(options.find(([m]) => Math.abs(m - value) < 1e-3)[0]);
      s.onchange = () => onChange(parseFloat(s.value));
      return s;
    };
    mine.forEach((part) => {
      const w = M.gestureWindow(part, gesture.name, steps);
      const row = el('div', 'rigSpriteWindow');
      const t = document.createElement('img'); t.src = M.variantOf(part, stateId).dataURL; t.className = 'rigVariantThumb own';
      row.appendChild(t);
      const body = el('div', 'rigSpriteWindowBody');
      body.appendChild(el('span', 'rigFxName', part.name));
      const line = el('div', 'rigSpriteWindowLine');
      line.appendChild(el('span', 'hint', 'se ve desde'));
      line.appendChild(momentSelect(w.from, (v) => { M.setGestureWindow(part, gesture.name, { from: v, to: Math.max(v, w.to) }, steps); changed(); }));
      line.appendChild(el('span', 'hint', 'hasta'));
      line.appendChild(momentSelect(w.to, (v) => { M.setGestureWindow(part, gesture.name, { from: Math.min(v, w.from), to: v }, steps); changed(); }));
      body.appendChild(line);
      row.appendChild(body);
      box.appendChild(row);
    });
    box.appendChild(el('p', 'hint', 'Fuera de ese tramo la pieza lleva su sprite normal. El cursor de la línea de tiempo lo enseña tal cual. Si luego añades o quitas pasos, revisa estos tramos: van por número de paso.'));
    card.appendChild(box);
  }

  /** The pose being edited: what is in it, and the numbers of the part (or body) a drag would edit. */
  function renderPosePanel(card, gesture, step) {
    const box = el('div', 'rigPosePanel');
    const target = poseTarget();
    const part = selectedPart();
    box.appendChild(el('p', 'rigTemplateInfo', `Posando «${stepLabel(gesture, step)}» · ${targetName(target)}`));

    const chips = el('div', 'rigActions');
    const chip = (t) => {
      const b = el('button', `small${t === target ? ' active' : ''}`, targetName(t));
      b.onclick = () => select(t === G.BODY ? null : t);
      chips.appendChild(b);
    };
    const inPose = gesture.steps[step].poses.map((p) => p.part);
    if (!inPose.includes(G.BODY)) chip(G.BODY);
    inPose.forEach(chip);
    box.appendChild(chips);
    box.appendChild(el('p', 'hint', 'En la pose: las piezas de arriba. Clic en una pieza del lienzo (o de la lista) para añadirla; sin pieza seleccionada se posa el cuerpo entero. La silueta tenue es el reposo: cada pose se mide desde él, no desde el paso anterior.'));

    if (part && !part.rigPart) {
      box.appendChild(el('p', 'efSummaryError', `"${part.name}" no es parte del rig: un gesto no puede moverla.`));
      card.appendChild(box);
      return;
    }

    const pose = G.poseOf(gesture, step, target);
    const edit = (fn) => { fn(G.poseOf(gesture, step, target, true)); requestRender(); refreshExport(); };
    const fine = (row, by) => { row.querySelectorAll('input[type=number]').forEach((i) => { i.step = by; }); return row; };
    box.appendChild(fine(F.vector2Field({ label: 'Desplazamiento (u)', value: pose?.move || { x: 0, y: 0 },
      hint: target === G.BODY ? 'X positivo = hacia delante (se refleja al mirar a la izquierda).' : 'En el espacio de su pieza padre.',
      onChange: (v) => edit((p) => { p.move = v; }) }).row, '0.05'));
    box.appendChild(F.numberField({ label: 'Giro (°)', value: pose?.rotate || 0, step: '1',
      hint: 'Una vuelta entera (360) es un giro completo: al volver al reposo no se deshace.',
      onChange: (v) => edit((p) => { p.rotate = v; }) }).row);
    box.appendChild(fine(F.vector2Field({ label: 'Escala (± relativa)', value: pose?.scale || { x: 0, y: 0 },
      hint: '0 = igual; 0.2 = un 20% más grande; −0.2 = un 20% más pequeña.',
      onChange: (v) => edit((p) => { p.scale = v; }) }).row, '0.05'));

    if (pose) {
      const clear = el('button', 'small danger', `Quitar ${targetName(target)} de la pose`);
      clear.title = 'También con la tecla Supr.';
      clear.onclick = () => clearPose(target);
      box.appendChild(clear);
    }
    card.appendChild(box);
  }

  // ============================================================ state sprites board

  /**
   * "Sprites por estado": pick a state (the canvas shows the body in it) and give each part the
   * sprite it wears there. A part left empty keeps what the fallback gives it (rig-model.js ▸
   * stateImage, the same order as Unity's ProceduralRig.RefreshSprites).
   */
  function renderStatesBoard() {
    const withArt = state.parts.filter((p) => p.image);
    const count = (id) => withArt.filter((p) => M.variantOf(p, id)).length;
    const options = spriteStateOptions().filter(([v]) => v);
    const total = options.reduce((n, [v]) => n + count(v), 0);
    const d = rememberedSection('states', `Sprites por estado${total ? ` (${total})` : ''}`, false, statesHost);
    if (!withArt.length) { d.appendChild(el('p', 'hint', 'Añade piezas con imagen primero.')); return; }
    if (!options.some(([v]) => v === preview.spriteState)) preview.spriteState = '';

    const boardState = preview.spriteState || 'alert';
    const pick = document.createElement('select');
    options.forEach(([v, l]) => { const o = el('option', null, `${l}${count(v) ? ` · ${count(v)}` : ''}`); o.value = v; pick.appendChild(o); });
    pick.value = boardState;
    pick.onchange = () => { preview.spriteState = pick.value; renderToolbar(); renderInspector(); requestRender(); };
    d.appendChild(pick);
    const meta = M.SPRITE_STATES.find((s) => s.id === boardState);
    d.appendChild(el('p', 'hint', meta ? meta.hint : 'Mientras dura ese gesto (el ataque que lo usa): "en este ataque uso este brazo". Sin sprite propio, la pieza usa el de "Atacando".'));
    if (preview.spriteState !== boardState) {
      const show = el('button', 'small', '👁 Ver este estado en el lienzo');
      show.onclick = () => { preview.spriteState = boardState; renderToolbar(); renderInspector(); requestRender(); };
      d.appendChild(show);
    }

    [...withArt].reverse().forEach((part) => {
      const row = el('div', `rigVariantRow${part.uid === selectedUid ? ' selected' : ''}`);
      const own = M.variantOf(part, boardState);
      const shown = own || M.stateImage(part, boardState, preview.stage) || part.image;
      const t = document.createElement('img'); t.src = shown.dataURL; t.className = `rigVariantThumb${own ? ' own' : ''}`;
      t.title = own ? 'Sprite propio de este estado.' : 'Sin sprite propio: es lo que se verá (el del estado anterior en la cadena, o el de la pieza).';
      row.appendChild(t);
      const name = el('span', 'rigVariantLabel', part.name);
      name.onclick = () => select(part.uid);
      row.appendChild(name);
      const input = document.createElement('input');
      input.type = 'file'; input.accept = 'image/*'; input.hidden = true;
      const put = async (file) => { const res = file && await M.importImage(file); if (res) { M.setVariant(part, boardState, res.image); changed(); } };
      input.onchange = () => put(input.files[0]);
      const choose = el('button', 'small', own ? 'Cambiar' : 'Elegir…');
      choose.onclick = () => input.click();
      row.append(input, choose);
      if (own) { const clr = el('button', 'small', '✕'); clr.title = 'Quitar: vuelve a lo que le toque por la cadena.'; clr.onclick = () => { M.setVariant(part, boardState, null); changed(); }; row.appendChild(clr); }
      row.addEventListener('dragover', (e) => { if (e.dataTransfer.types.includes('Files')) e.preventDefault(); });
      row.addEventListener('drop', (e) => { const f = e.dataTransfer.files?.[0]; if (f) { e.preventDefault(); put(f); } });
      d.appendChild(row);
    });
    d.appendChild(el('p', 'hint', 'Mismo tamaño de archivo que la pieza = se alinea solo. También puedes soltar el PNG sobre la fila.'));
  }

  function renderBodySection() {
    const d = rememberedSection('body', 'Collider y cadáver', false);
    const c = state.collider;
    d.appendChild(F.enumField({ label: 'Collider', value: c.type, options: [{ value: 'box', label: 'Caja' }, { value: 'capsule', label: 'Cápsula' }, { value: 'circle', label: 'Círculo' }],
      onChange: (v) => { c.type = v; if (v === 'circle' && !c.radius) c.radius = Math.max(c.size.x, c.size.y) / 2; changed(); } }).row);
    if (c.type === 'circle') d.appendChild(F.numberField({ label: 'Radio', value: c.radius || 0.5, onChange: (v) => { c.radius = v; requestRender(); refreshExport(); } }).row);
    else d.appendChild(F.vector2Field({ label: 'Tamaño', value: c.size, onChange: (v) => { c.size = v; requestRender(); refreshExport(); } }).row);
    d.appendChild(F.vector2Field({ label: 'Centro', value: c.offset, onChange: (v) => { c.offset = v; requestRender(); refreshExport(); } }).row);
    const fit = el('button', 'small', 'Ajustar a las piezas');
    fit.onclick = () => { fitCollider(); changed(); };
    d.appendChild(fit);
    d.appendChild(F.numberField({ label: 'Cadáver: espera (s)', value: state.corpse.linger, hint: 'Las piezas necesitan tiempo para caer.', onChange: (v) => { state.corpse.linger = v; refreshExport(); } }).row);
    d.appendChild(F.numberField({ label: 'Cadáver: desvanecer (s)', value: state.corpse.fadeDuration, onChange: (v) => { state.corpse.fadeDuration = v; refreshExport(); } }).row);

    if (state.kind === 'boss') {
      const b = rememberedSection('boss', 'Jefe', true);
      const bb = state.bossBody;
      [['health', 'Vida'], ['arenaHalfWidth', 'Media anchura de la arena'], ['arenaHeight', 'Altura de la arena'], ['activationRadius', 'Radio de activación'], ['contactDamage', 'Daño por contacto']]
        .forEach(([k, l]) => b.appendChild(F.numberField({ label: l, value: bb[k], onChange: (v) => { bb[k] = v; refreshExport(); } }).row));
      b.appendChild(F.numberField({ label: 'Pies: altura (Y)', value: Math.round(M.feetY(state) * 100) / 100, step: '0.05',
        hint: 'La línea naranja: lo que se apoya en el suelo y desde donde los ataques miden alturas (el pivote va en el centro). Por defecto, la base del collider.',
        onChange: (v) => { state.feetY = v; requestRender(); refreshExport(); } }).row);
      const autoFeet = el('button', 'small', 'Pies = base del collider');
      autoFeet.onclick = () => { state.feetY = null; changed(); };
      b.appendChild(autoFeet);
      b.appendChild(F.numberField({ label: 'Radio de contacto de extremidad', value: state.bossLimbs.contactRadius, onChange: (v) => { state.bossLimbs.contactRadius = v; refreshExport(); } }).row);
      b.appendChild(F.numberField({ label: 'Radio de contacto de orbital', value: state.bossLimbs.orbiterRadius, onChange: (v) => { state.bossLimbs.orbiterRadius = v; refreshExport(); } }).row);
      b.appendChild(el('p', 'hint', 'Se escriben al crear el jefe en Unity (o reimportando con "Resetear").'));
    }
  }

  /** A boss takes its attacks and phases from a 👑 Boss Creator entry (an enemy's behaviour is step 4's form). */
  function renderLinksSection() {
    if (state.kind !== 'boss') return;
    const d = rememberedSection('links', 'Ataques y fases (Boss Creator)', true, linksHost);
    const options = [{ value: '', label: '(ninguno: la definición se asigna en Unity)' }, ...bossEntries.map((e) => ({ value: e.id, label: e.enemyName }))];
    const tpl = findTemplate(state.template?.id);
    if (tpl?.behaviour?.summary) d.appendChild(el('p', 'hint', `Plantilla: ${tpl.behaviour.summary}`));
    d.appendChild(F.enumField({ label: 'Jefe de la biblioteca', value: state.bossConfigId || '', options,
      hint: 'Su boss-config (ataques, fases, arte de ataques) viaja en el .zip sin su cuerpo: el cuerpo es este rig.',
      onChange: (v) => { state.bossConfigId = v || null; refreshExport(); } }).row);
    d.appendChild(F.textField({ label: 'O una BossDefinition que ya existe en Unity', value: state.bossBody.definition || '',
      placeholder: 'Assets/Resources/Bosses/Boss_ColosodeHielo2.asset',
      hint: 'Ruta del asset (clic derecho ▸ Copy Path). Misma pelea que otro jefe: al importar hereda también sus avisos (flechas, círculos). Si eliges un jefe de la biblioteca arriba, manda ése.',
      onChange: (v) => { state.bossBody.definition = v; refreshExport(); } }).row);
  }

  let exportSummaryEl = null, exportPreviewEl = null;
  function renderExportSection() {
    const d = rememberedSection('export', 'Exportar', true, exportHost);
    exportSummaryEl = el('div', 'rigExportSummary');
    d.appendChild(exportSummaryEl);
    const save = el('button', 'primary', '💾 Guardar en biblioteca');
    save.style.width = '100%';
    save.onclick = saveToLibrary;
    const zip = el('button', null, '⬇ Exportar .zip para Unity');
    zip.style.width = '100%';
    zip.onclick = exportZip;
    const json = el('button', 'small', '⬇ Sólo rig-config.json');
    json.onclick = () => {
      const { doc } = buildRigExport(state, { entriesById: fxEntries });
      downloadBlob(new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' }), `${fileStem()}.rig-config.json`);
    };
    const reset = el('button', 'small danger', 'Nuevo (vaciar)');
    reset.onclick = () => { if (confirm('¿Empezar un rig nuevo? Lo no guardado se pierde.')) { state = createDefaultRigState(); currentId = null; selectedUid = null; resetGestureUi(); changed(); view.frame(state); } };
    d.append(save, zip, json, reset);
    const pre = el('details', 'rigSection');
    pre.appendChild(el('summary', null, 'rig-config.json'));
    exportPreviewEl = el('pre', 'rigJsonPreview');
    pre.appendChild(exportPreviewEl);
    d.appendChild(pre);
    refreshExport();
  }

  // ============================================================ undo / redo

  const history = [];
  let historyIndex = -1;
  let restoring = false;

  function snapshot() {
    const images = [];
    const json = JSON.stringify({ state, selectedUid }, (k, v) =>
      (v && typeof v === 'object' && typeof v.dataURL === 'string' ? { __img: images.push(v) - 1 } : v));
    return { json, images };
  }

  function recordHistory() {
    if (restoring) return;
    const snap = snapshot();
    if (historyIndex >= 0 && history[historyIndex].json === snap.json) return;
    history.splice(historyIndex + 1);
    history.push(snap);
    if (history.length > 80) history.shift();
    historyIndex = history.length - 1;
    renderUndoButtons();
  }

  function restore(i) {
    if (i < 0 || i >= history.length) return;
    historyIndex = i;
    const snap = history[i];
    const data = JSON.parse(snap.json, (k, v) => (v && typeof v === 'object' && v.__img !== undefined ? snap.images[v.__img] : v));
    restoring = true;
    state = data.state;
    selectedUid = data.selectedUid;
    changed();
    renderToolbar();
    setTimeout(() => { restoring = false; renderUndoButtons(); }, 200);
  }

  const undo = () => restore(historyIndex - 1);
  const redo = () => restore(historyIndex + 1);

  let undoBtn = null, redoBtn = null;
  function renderUndoButtons() {
    if (undoBtn) undoBtn.disabled = historyIndex <= 0;
    if (redoBtn) redoBtn.disabled = historyIndex >= history.length - 1;
  }

  document.addEventListener('keydown', (e) => {
    if (canvas.closest('.tabPanel')?.hidden || (e.target instanceof Element && e.target.closest('input, textarea, select'))) return;
    if (!(e.ctrlKey || e.metaKey)) return;
    const k = e.key.toLowerCase();
    if (k === 'z' && !e.shiftKey) { e.preventDefault(); undo(); }
    else if (k === 'y' || (k === 'z' && e.shiftKey)) { e.preventDefault(); redo(); }
  });

  /** Another document on screen: nothing of the previous one's gestures is open, posing or playing. */
  function resetGestureUi() {
    gestureIndex = -1;
    posing = null;
    stopHead();
    placing = null;
    timeline.reset();
  }

  const fileStem = () => (state.name.trim() || 'rig').replace(/[^A-Za-z0-9_-]/g, '_');

  let exportTimer = 0;
  function refreshExport() {
    clearTimeout(exportTimer);
    exportTimer = setTimeout(() => {
      recordHistory();
      if (hooks.onChange) hooks.onChange();
      if (!exportSummaryEl) return;
      const { doc, problems, notes } = buildRigExport(state, { entriesById: fxEntries });
      const schemaErrors = validate ? validateConfig(validate, doc).errors : [];
      let html = '';
      if (!problems.length && !schemaErrors.length) html += '<p class="efSummaryOk">✓ Listo para exportar (rig-config.schema.json)</p>';
      if (problems.length) html += `<p class="efSummaryError">✗ Falta:</p><ul>${problems.map((p) => `<li>${p}</li>`).join('')}</ul>`;
      if (schemaErrors.length) html += `<p class="efSummaryError">✗ Esquema:</p><ul>${schemaErrors.map((e) => `<li><code>${e.path || '(raíz)'}</code> — ${e.message}</li>`).join('')}</ul>`;
      if (notes.length) html += `<ul class="hint">${notes.map((n) => `<li>${n}</li>`).join('')}</ul>`;
      exportSummaryEl.innerHTML = html;
      if (exportPreviewEl) exportPreviewEl.textContent = JSON.stringify(doc, null, 2);
    }, 150);
  }

  // ============================================================ library + export

  async function thumbnail() {
    const c = document.createElement('canvas');
    c.width = 320; c.height = 320;
    const tmp = new RigCanvasView(c, { clientWidth: 320, clientHeight: 320 }, { dpr: 1 });
    tmp.frame(state);
    // Every part image decoded first (the view loads them lazily on its first render).
    await tmp.preload(state);
    tmp.render(state, { showMarkers: false, showCollider: false });
    return buildThumbnail(c);
  }

  async function saveToLibrary() {
    if (!state.name.trim()) { setStatus('Ponle nombre antes de guardar.'); return null; }
    const { doc } = buildRigExport(state, { entriesById: fxEntries });
    const patch = { enemyName: state.name.trim(), thumbnail: await thumbnail(), config: doc, draft: clone(state) };
    try {
      const record = currentId && (await getEnemy(currentId))
        ? await updateEnemy(currentId, patch)
        : await saveEnemy({ kind: 'rig', sprite: null, ...patch });
      currentId = record.id;
      if (hooks.afterSave) await hooks.afterSave(record.id);
      behaviour = (await getEnemy(record.id))?.enemyConfig || null;
      setStatus(`Guardado "${record.enemyName}" en la biblioteca (${new Date(record.updatedAt).toLocaleTimeString()}).`);
      if (hooks.onChange) hooks.onChange();
      return record.id;
    } catch (e) { setStatus(`No se pudo guardar: ${e.message}`); }
    return null;
  }

  async function exportZip() {
    const { problems } = buildRigExport(state, { entriesById: fxEntries });
    if (problems.length && !confirm(`Hay avisos:\n- ${problems.join('\n- ')}\n\n¿Exportar igualmente?`)) return;
    if (hooks.beforeExport) await hooks.beforeExport();   // the behaviour form open in Enemy Studio, saved first
    const all = await listEntries(null);
    const entriesById = new Map(all.map((e) => [e.id, e]));
    const blob = await buildRigBundle(state, {
      enemyConfig: (currentId && (await getEnemy(currentId))?.enemyConfig) || suggestedEnemyConfig(state),
      bossEntry: entriesById.get(state.bossConfigId) || null,
      entriesById,
    });
    downloadBlob(blob, `${fileStem()}.rig.zip`);
    setStatus('Exportado. En Unity: Tools ▸ Web ▸ Rig ▸ Importar rig (.zip · carpeta)…');
    if (currentId) saveToLibrary();
  }

  async function refreshLibraries() {
    behaviour = currentId ? (await getEnemy(currentId))?.enemyConfig || null : null;
    bossEntries = (await listEntries('boss')).filter((e) => e.config);
    userTemplates = (await listEntries('rig-template')).map(templateFromRecord).filter(Boolean);
    const effects = [...(await listEntries('fx')), ...(await listEntries('projectile'))].filter((e) => FX.fxFrames(e, false));
    fxEntries = new Map(effects.map((e) => [e.id, e]));
    renderSidebar();
    renderInspector();
    if (hooks.onChange) hooks.onChange();
  }

  const adopt = adoptRigState;

  async function linkLibraryEntry(id) {
    const entry = await getEnemy(id);
    if (!entry?.draft) { setStatus('Esa entrada no tiene el documento del cuerpo por partes.'); return; }
    state = adopt(entry.draft);
    currentId = entry.id;
    selectedUid = null;
    resetGestureUi();
    await refreshLibraries();
    changed();
    view.frame(state);
    setStatus(`Abierto "${entry.enemyName}".`);
  }

  renderToolbar();
  renderSidebar();
  renderInspector();
  refreshLibraries();
  view.frame(state);
  setStatus(TOOLS[0].hint);

  /** What Enemy Studio needs to know to lock / tick its steps. */
  function getStatus() {
    return {
      id: currentId,
      name: state.name.trim(),
      kind: state.kind,
      parts: state.parts.length,
      hasArt: state.parts.some((p) => p.image),
      openRequired: openSlots(state, true).length,
      gestures: (state.gestures || []).length,
      gestureFx: (state.gestures || []).reduce((n, g) => n + (g.fx || []).length, 0),
      stateSprites: state.parts.reduce((n, p) => n + ['asleep', 'awake', 'attacking'].filter((k) => p.variants?.[k]).length
        + Object.values(p.variants?.named || {}).filter(Boolean).length + Object.values(p.variants?.gestures || {}).filter(Boolean).length, 0),
      openGesture: state.gestures?.[gestureIndex]?.name || null,
      idleGestures: (state.idle?.gestures || []).length,
      template: state.template ? state.template.label || findTemplate(state.template.id)?.label || null : null,
      behaviour: !!behaviour,
    };
  }

  /** "Nuevo desde plantilla" from outside (Biblioteca ▸ a saved template's "Editar en…"). */
  async function useTemplateRecord(recordId) {
    await refreshLibraries();
    const template = userTemplates.find((t) => t.recordId === recordId);
    if (!template) { setStatus('Esa plantilla ya no existe.'); return; }
    templateChoice = template.id;
    newFromTemplate(template.id);
  }

  /** Enemy Studio ▸ Efectos ▸ "＋ al gesto": onto the open gesture, at the instant its playhead is on. */
  function addFxToOpenGesture(libraryId) {
    const gesture = state.gestures?.[gestureIndex];
    if (!gesture) { setStatus('Abre antes un gesto (inspector ▸ Gestos) para colgarle el efecto.'); return false; }
    if (!timeline.addFx(libraryId)) { setStatus('Ese efecto no tiene frames.'); return false; }
    setStatus(`Efecto añadido a "${gesture.name}" en el instante donde está el cursor de la línea de tiempo. Colócalo con "⌖ Colocar en el lienzo".`);
    return true;
  }

  return {
    linkLibraryEntry, refreshLibraries, hooks, getStatus, useTemplateRecord, addFxToOpenGesture,
    save: saveToLibrary,
    onShow: () => { refreshLibraries(); requestRender(); },
  };
}
