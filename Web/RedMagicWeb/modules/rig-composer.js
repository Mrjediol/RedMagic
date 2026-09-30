// modules/rig-composer.js
// -----------------------------------------------------------------------------
// The 🦴 Rig Composer tab: assemble an enemy or boss from separate part PNGs and export the
// rig-config .zip Unity's Tools ▸ Web ▸ Rig ▸ Importar rig reads. Separate from every classic tab.
//
//   left   — document (name, kind, scale), the parts list (draw order + parenting by drag)
//   centre — canvas with tools: move / rotate / scale / pivot / centre / point / barrel / muzzle / grip
//   right  — the selected part (transform, roles, sway, state sprites), rig timings, collider,
//            links to an Enemy Creator / Boss Creator entry, validation and export
//
// Geometry and part operations: rig-model.js. Drawing and picking: rig-canvas-view.js. Export:
// rig-export.js. Preview formulas copy RigDriverCode.cs (sway, hover, lean, hurt).

import { createDefaultRigState, createPart, RIG_FIELD_GROUPS, createDefaultRigFields } from './rig-defaults.js';
import * as M from './rig-model.js';
import { RigCanvasView } from './rig-canvas-view.js';
import { buildRigExport, buildRigBundle } from './rig-export.js';
import { loadValidator, validateConfig } from './config-schema.js';
import { saveEnemy, updateEnemy, getEnemy, listEntries, buildThumbnail } from './enemy-library.js';
import { downloadBlob } from './export-manifest.js';
import * as F from './enemy-form-fields.js';

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

export function initRigComposer({ sidebar, toolbar, canvas, canvasWrap, statusEl, inspector }) {
  let state = createDefaultRigState();
  let selectedUid = null;
  let tool = 'move';
  let currentId = null;          // library record id this document was saved as / opened from
  let validate = null;
  let enemyEntries = [];
  let bossEntries = [];

  // preview (RigDriverCode's formulas)
  const preview = { playing: false, clock: 0, lean: 0, speed: 0, hurt: -1, spriteState: '', stage: 0, last: 0 };

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

  function draw() {
    const anim = preview.playing || preview.hurt >= 0 ? previewPoses() : null;
    view.render(state, {
      selectedUid,
      poses: anim?.poses,
      visualMatrix: anim?.visual,
      spriteState: preview.spriteState,
      stage: preview.stage,
      requestRender,
    });
  }

  function setStatus(text) { statusEl.textContent = text; }

  // ============================================================ preview animation

  function previewPoses() {
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
    const visual = M.trs(bob, preview.lean + hurtTilt, { x: 1 + squash, y: 1 - squash });
    return { poses, visual };
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
    draw();
    if (preview.playing || preview.hurt >= 0 || Math.abs(preview.lean) > 0.01) requestAnimationFrame(tick);
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
    [['', 'sprite base'], ['asleep', 'dormido'], ['awake', 'despierto'], ['attacking', 'atacando']].forEach(([v, l]) => {
      const o = el('option', null, l); o.value = v; st.appendChild(o);
    });
    st.value = preview.spriteState;
    st.title = 'Qué sprite por estado enseñar.';
    st.onchange = () => { preview.spriteState = st.value; requestRender(); };
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

  document.addEventListener('keydown', (e) => {
    if (canvas.closest('.tabPanel')?.hidden) return;
    if ((e.target instanceof Element && e.target.closest('input, textarea, select'))) return;
    const t = TOOLS.find((x) => x.key === e.key.toLowerCase());
    if (e.ctrlKey || e.metaKey) return;
    if (t) { tool = t.id; renderToolbar(); setStatus(t.hint); }
    if ((e.key === 'Delete' || e.key === 'Supr') && selectedUid) { removePart(M.byUid(state, selectedUid)); }
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

  canvas.addEventListener('mousedown', (e) => {
    if (e.button !== 0) return;
    const pt = view.screenToVisual(e.clientX, e.clientY);
    const hit = view.partAt(state, e.clientX, e.clientY);
    let part = selectedPart();

    if (tool === 'move') {
      if (!hit) { select(null); return; }
      select(hit.uid);
      const parent = M.byUid(state, hit.parent);
      drag = { kind: 'move', part: hit, start: pt, pos: { ...hit.position }, inv: parent ? M.invert(M.worldMatrix(state, parent)) : M.IDENTITY };
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
    if (drag.kind === 'move') {
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
    drag = null;
    changed();
  });

  // Files dropped on the canvas = new parts.
  canvasWrap.addEventListener('dragover', (e) => { if (e.dataTransfer?.types?.includes('Files')) e.preventDefault(); });
  canvasWrap.addEventListener('drop', (e) => {
    if (!e.dataTransfer?.files?.length) return;
    e.preventDefault();
    addFiles([...e.dataTransfer.files]);
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
    let removed = 0, last = null;
    for (const file of images) {
      const res = await M.importImage(file);
      if (!res) { setStatus(`"${file.name}" no tiene nada visible.`); continue; }
      if (res.removedBg) removed++;
      const part = createPart({ name: M.uniqueName(state, file.name.replace(/\.[^.]+$/, '')), image: res.image, index: state.parts.length });
      part.position = M.initialPosition(state, res.image, part.pivot);
      state.parts.push(part);
      last = part;
    }
    if (last) selectedUid = last.uid;
    if (state.parts.length === images.length) { fitCollider(); view.frame(state); }
    changed();
    setStatus(`${images.length} pieza(s) añadida(s)${removed ? `, fondo quitado en ${removed}` : ''}. Los PNG del mismo lienzo quedan ya colocados.`);
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
    if (state.bossBody.bodyRenderer === part.uid) state.bossBody.bodyRenderer = null;
    if (selectedUid === part.uid) selectedUid = null;
    changed();
  }

  function moveInOrder(part, delta) {
    const i = state.parts.indexOf(part);
    const j = i + delta;
    if (j < 0 || j >= state.parts.length) return;
    [state.parts[i], state.parts[j]] = [state.parts[j], state.parts[i]];
    changed();
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
    sidebar.appendChild(el('h1', null, '🦴 Rig Composer'));
    sidebar.appendChild(el('p', 'hint', 'Enemigo o jefe hecho de piezas sueltas. Suelta PNG sobre el lienzo, colócalos y exporta el .zip. En Unity: Tools ▸ Web ▸ Rig ▸ Importar rig.'));

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
      const row = el('div', `rigPartRow${part.uid === selectedUid ? ' selected' : ''}`);
      row.draggable = true;
      row.style.paddingLeft = `${8 + depth(part) * 14}px`;
      const name = el('span', 'rigPartName', `${part.parent ? '↳ ' : ''}${part.name}${part.image ? '' : ' ◌'}`);
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
      list.appendChild(row);
    });
    sidebar.appendChild(list);
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

  function section(title, open = true) {
    const d = el('details', 'rigSection');
    d.open = open;
    d.appendChild(el('summary', null, title));
    inspector.appendChild(d);
    return d;
  }

  const openSections = new Map();
  function rememberedSection(key, title, def) {
    const d = section(title, openSections.has(key) ? openSections.get(key) : def);
    d.addEventListener('toggle', () => openSections.set(key, d.open));
    return d;
  }

  function renderInspector() {
    const scrollTop = inspector.scrollTop;
    inspector.innerHTML = '';
    const part = selectedPart();
    if (part) renderPartInspector(part);
    else inspector.appendChild(el('p', 'hint', 'Selecciona una pieza (clic en el lienzo o en la lista).'));
    renderRigSection();
    renderBodySection();
    renderLinksSection();
    renderExportSection();
    inspector.scrollTop = scrollTop;
  }

  function renderPartInspector(part) {
    const s = rememberedSection('part', `Pieza: ${part.name}`, true);

    const nameField = F.textField({ label: 'Nombre', value: part.name, onChange: () => {} });
    nameField.input.addEventListener('change', () => { part.name = M.uniqueName(state, nameField.input.value, part); changed(); });
    s.appendChild(nameField.row);

    const parentOptions = [{ value: '', label: 'Visual (raíz)' }, ...state.parts
      .filter((p) => p !== part && !M.isDescendant(state, p.uid, part.uid))
      .map((p) => ({ value: p.uid, label: M.pathOf(state, p) }))];
    s.appendChild(F.enumField({ label: 'Cuelga de', value: part.parent || '', options: parentOptions,
      hint: 'Se queda donde está en pantalla; sólo cambia de qué pieza depende.',
      onChange: (v) => { M.reparent(state, part, v || null); changed(); } }).row);

    s.appendChild(F.vector2Field({ label: 'Posición (u, en su padre)', value: part.position,
      onChange: (v) => { part.position = v; requestRender(); refreshExport(); } }).row);
    s.appendChild(F.numberField({ label: 'Giro (°)', value: part.rotation,
      onChange: (v) => { part.rotation = v; requestRender(); refreshExport(); } }).row);
    s.appendChild(F.vector2Field({ label: 'Escala', value: part.scale,
      onChange: (v) => { part.scale = v; requestRender(); refreshExport(); } }).row);
    s.appendChild(F.boolField({ label: 'Parte del rig (se balancea / sale despedida)', value: part.rigPart,
      hint: 'Apagado = sólo existe en la jerarquía (un pivote vacío, una pieza fija).',
      onChange: (v) => { part.rigPart = v; changed(); } }).row);

    const acts = el('div', 'rigActions');
    const mk = (label, title, fn, cls = 'small') => { const b = el('button', cls, label); b.title = title; b.onclick = fn; acts.appendChild(b); return b; };
    const replaceInput = document.createElement('input');
    replaceInput.type = 'file'; replaceInput.accept = 'image/*'; replaceInput.hidden = true;
    replaceInput.onchange = async () => {
      const f = replaceInput.files[0];
      if (!f) return;
      const res = await M.importImage(f);
      if (res) { part.image = res.image; if (!part.pivot || part.pivot.x === 0) part.pivot = { x: res.image.cropX + res.image.w / 2, y: res.image.cropY + res.image.h / 2 }; changed(); }
    };
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
      const d = rememberedSection('sway', 'Balanceo y comportamiento', false);
      d.appendChild(F.vector2Field({ label: 'Vaivén (u)', value: part.sway.move, onChange: (v) => { part.sway.move = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Giro del vaivén (°)', value: part.sway.rotate, onChange: (v) => { part.sway.rotate = v; refreshExport(); } }).row);
      d.appendChild(F.vector2Field({ label: 'Pulso de escala', value: part.sway.scale, hint: 'Una llama o un núcleo que late (0 = nada).', onChange: (v) => { part.sway.scale = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Velocidad (rad/s)', value: part.sway.speed, min: 0, onChange: (v) => { part.sway.speed = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Desfase (rad)', value: part.sway.phase, onChange: (v) => { part.sway.phase = v; refreshExport(); } }).row);
      d.appendChild(F.numberField({ label: 'Sigue la inclinación', value: part.leanFollow, hint: 'Positivo = se queda atrás (péndulo); negativo = tiende a vertical.', onChange: (v) => { part.leanFollow = v; refreshExport(); } }).row);
      if (state.roles.limbs.includes(part.uid) || state.roles.orbiters.includes(part.uid))
        d.appendChild(F.numberField({ label: 'Frente al lanzar (°)', value: part.pointAngle, hint: 'Herramienta ➜ Frente al lanzar: sale volando con este lado por delante. 0 = derecha, −90 = abajo.', onChange: (v) => { part.pointAngle = v; requestRender(); refreshExport(); } }).row);
      d.appendChild(F.boolField({ label: 'Sale despedida al morir', value: part.breaksOnDeath, onChange: (v) => { part.breaksOnDeath = v; refreshExport(); } }).row);
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
    slot('Dormido', () => v.asleep, (x) => { v.asleep = x; });
    slot('Despierto', () => v.awake, (x) => { v.awake = x; });
    slot('Atacando (aviso)', () => v.attacking, (x) => { v.attacking = x; });
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
      b.appendChild(F.numberField({ label: 'Radio de contacto de extremidad', value: state.bossLimbs.contactRadius, onChange: (v) => { state.bossLimbs.contactRadius = v; refreshExport(); } }).row);
      b.appendChild(F.numberField({ label: 'Radio de contacto de orbital', value: state.bossLimbs.orbiterRadius, onChange: (v) => { state.bossLimbs.orbiterRadius = v; refreshExport(); } }).row);
      b.appendChild(el('p', 'hint', 'Se escriben al crear el jefe en Unity (o reimportando con "Resetear").'));
    }
  }

  function renderLinksSection() {
    const d = rememberedSection('links', state.kind === 'boss' ? 'Ataques y fases (Boss Creator)' : 'Números de juego (Enemy Creator)', true);
    if (state.kind === 'boss') {
      const options = [{ value: '', label: '(ninguno: la definición se asigna en Unity)' }, ...bossEntries.map((e) => ({ value: e.id, label: e.enemyName }))];
      d.appendChild(F.enumField({ label: 'Jefe de la biblioteca', value: state.bossConfigId || '', options,
        hint: 'Su boss-config (ataques, fases, arte de ataques) viaja en el .zip sin su cuerpo: el cuerpo es este rig.',
        onChange: (v) => { state.bossConfigId = v || null; refreshExport(); } }).row);
    } else {
      const options = [{ value: '', label: '(ninguno: tuning por defecto)' }, ...enemyEntries.map((e) => ({ value: e.id, label: e.enemyName }))];
      d.appendChild(F.enumField({ label: 'Enemigo de la biblioteca', value: state.enemyConfigId || '', options,
        hint: 'Su tuning (arquetipo, rangos, daño, proyectil) viaja en el .zip como enemy-config.json. Su hoja de sprites no se usa.',
        onChange: (v) => { state.enemyConfigId = v || null; refreshExport(); } }).row);
      if (!enemyEntries.length) d.appendChild(el('p', 'hint', 'Ningún enemigo con config todavía: créalo en Enemy Creator (el arte da igual) y expórtalo una vez.'));
    }
  }

  let exportSummaryEl = null, exportPreviewEl = null;
  function renderExportSection() {
    const d = rememberedSection('export', 'Exportar', true);
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
      const { doc } = buildRigExport(state);
      downloadBlob(new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' }), `${fileStem()}.rig-config.json`);
    };
    const reset = el('button', 'small danger', 'Nuevo (vaciar)');
    reset.onclick = () => { if (confirm('¿Empezar un rig nuevo? Lo no guardado se pierde.')) { state = createDefaultRigState(); currentId = null; selectedUid = null; changed(); view.frame(state); } };
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

  const fileStem = () => (state.name.trim() || 'rig').replace(/[^A-Za-z0-9_-]/g, '_');

  let exportTimer = 0;
  function refreshExport() {
    clearTimeout(exportTimer);
    exportTimer = setTimeout(() => {
      recordHistory();
      if (!exportSummaryEl) return;
      const { doc, problems, notes } = buildRigExport(state);
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
    await new Promise((resolve) => {
      let tries = 0;
      const attempt = () => { tmp.render(state, { showMarkers: false, showCollider: false }); if (++tries > 20) resolve(); else setTimeout(attempt, 30); };
      attempt();
    });
    return buildThumbnail(c);
  }

  async function saveToLibrary() {
    if (!state.name.trim()) { setStatus('Ponle nombre antes de guardar.'); return; }
    const { doc } = buildRigExport(state);
    const patch = { enemyName: state.name.trim(), thumbnail: await thumbnail(), config: doc, draft: clone(state) };
    try {
      const record = currentId && (await getEnemy(currentId))
        ? await updateEnemy(currentId, patch)
        : await saveEnemy({ kind: 'rig', sprite: null, ...patch });
      currentId = record.id;
      setStatus(`Guardado "${record.enemyName}" en la biblioteca (${new Date(record.updatedAt).toLocaleTimeString()}).`);
    } catch (e) { setStatus(`No se pudo guardar: ${e.message}`); }
  }

  async function exportZip() {
    const { problems } = buildRigExport(state);
    if (problems.length && !confirm(`Hay avisos:\n- ${problems.join('\n- ')}\n\n¿Exportar igualmente?`)) return;
    const all = await listEntries(null);
    const entriesById = new Map(all.map((e) => [e.id, e]));
    const blob = await buildRigBundle(state, {
      enemyEntry: entriesById.get(state.enemyConfigId) || null,
      bossEntry: entriesById.get(state.bossConfigId) || null,
      entriesById,
    });
    downloadBlob(blob, `${fileStem()}.rig.zip`);
    setStatus('Exportado. En Unity: Tools ▸ Web ▸ Rig ▸ Importar rig (.zip · carpeta)…');
    if (currentId) saveToLibrary();
  }

  async function refreshLibraries() {
    const enemies = await listEntries('enemy');
    enemyEntries = enemies.filter((e) => e.config);
    bossEntries = (await listEntries('boss')).filter((e) => e.config);
    renderInspector();
  }

  /** A saved composer document back into a valid state (fills anything added since). */
  function adopt(draft) {
    const base = createDefaultRigState();
    const s = { ...base, ...clone(draft) };
    s.roles = { ...base.roles, ...(draft.roles || {}) };
    s.rig = { ...base.rig, ...(draft.rig || {}) };
    s.bossLimbs = { ...base.bossLimbs, ...(draft.bossLimbs || {}) };
    s.bossBody = { ...base.bossBody, ...(draft.bossBody || {}) };
    s.parts = (s.parts || []).map((p) => ({ ...createPart({ name: p.name }), ...p, variants: { asleep: null, awake: null, attacking: null, stages: [], ...(p.variants || {}) } }));
    return s;
  }

  async function linkLibraryEntry(id) {
    const entry = await getEnemy(id);
    if (!entry?.draft) { setStatus('Esa entrada no tiene el documento del Rig Composer.'); return; }
    state = adopt(entry.draft);
    currentId = entry.id;
    selectedUid = null;
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

  return { linkLibraryEntry, refreshLibraries, onShow: () => { refreshLibraries(); requestRender(); } };
}
