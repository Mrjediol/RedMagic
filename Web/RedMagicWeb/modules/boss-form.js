// modules/boss-form.js
// -----------------------------------------------------------------------------
// The Boss Creator tab. Builds a boss end to end — body animations, movement, warnings, attacks
// with their art, phases — and exports what BossBundleImporter.cs / BossConfigImporter.cs read.
//
// WHERE THE LISTS COME FROM
//  - Attack types, movement types, every field, default and art slot: Unity's catalog
//    (modules/boss-catalog.js, written by BossCatalogExporter.cs on every compile). Nothing about a
//    specific attack is hardcoded here, so a new C# archetype shows up with no web change.
//  - Art (body animations, projectiles, effects, warnings): entries of the shared library, made in
//    the Sprites tab. Any empty art slot uses the attack's placeholder in Unity.
//
// RENDERING: each section is rebuilt from state on a STRUCTURAL change (add/remove/reorder/type
// change); scalar edits mutate state in place and only call refresh() (preview + validation) —
// a full rebuild per keystroke would lose focus mid-typing.

import * as F from './enemy-form-fields.js';
import {
  BODY_STATS, createDefaultBossConfig, createAttack, createDefaultPhase, createArtOptions,
  defaultsOf, withDefaults, clone, resetAttackIdCounter, createDefaultBody,
} from './boss-defaults.js';
import { buildBossExport, bodyAnimationNames, framedLanes } from './boss-export.js';
import { buildBossBundle } from './boss-bundle.js';
import { ATTACK_NOTES, ART_KIND_NOTES, attackLabel } from './boss-catalog-notes.js';
import { loadValidator, validateConfig } from './config-schema.js';
import { downloadBlob, cropBox } from './export-manifest.js';
import { listEntries, getEnemy, saveEnemy, updateEnemy, loadImageFromDataURL } from './enemy-library.js';

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

const escapeHtml = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
}[c]));

export function initBossCreator({ formRoot, previewEl, summaryEl, exportBtn, zipBtn, saveBtn, onSaved }) {
  let state = createDefaultBossConfig();
  let catalog = null;
  let validate = null;
  let currentBossId = null;

  /** Every library entry that owns a sheet, by id. Refreshed when the tab is shown. */
  let entries = [];
  let entriesById = new Map();

  let lastExport = null;

  const catalogReady = import('./boss-catalog.js')
    .then((m) => { catalog = m.BOSS_CATALOG; })
    .catch(() => { catalog = null; });

  loadValidator('boss').then((v) => { validate = v; refresh(); }).catch((err) => {
    summaryEl.innerHTML = `<p class="efSummaryError">No se pudo cargar el validador: ${escapeHtml(err.message)}</p>`;
  });

  // ============================================================ layout

  const banner = el('div', 'bcBanner');
  formRoot.appendChild(banner);

  const identitySection = section('Jefe');
  const bodySection = section('Cuerpo y movimiento');
  const attacksSection = section('Ataques');
  const artSection = section('Arte usado');
  const phasesSection = section('Fases');

  function section(title) {
    const s = el('div', 'efSection');
    s.appendChild(el('h2', null, title));
    const content = el('div');
    s.appendChild(content);
    formRoot.appendChild(s);
    return content;
  }

  function renderAll() {
    renderBanner();
    renderIdentity();
    renderBody();
    renderAttacks();
    renderPhases();
    refresh();
  }

  function renderBanner() {
    banner.innerHTML = catalog
      ? ''
      : '<p class="efSummaryError">Falta el catálogo de Unity (<code>modules/boss-catalog.js</code>).</p>'
        + '<p class="efHint">Abre el proyecto en Unity una vez (se genera solo al compilar) o usa '
        + '<em>Tools ▸ Web ▸ Exportar catálogo de jefes</em>, y recarga esta página. Sin él no se '
        + 'pueden elegir tipos de ataque ni movimientos.</p>';
  }

  // ============================================================ identidad

  function renderIdentity() {
    identitySection.innerHTML = '';
    const bar = el('div', 'bcIdBar');
    bar.appendChild(el('span', 'efHint', currentBossId ? 'Editando un jefe guardado: "Guardar" lo actualiza.' : 'Jefe nuevo (aún sin guardar).'));
    const fresh = el('button', 'small', '＋ Nuevo jefe');
    fresh.onclick = () => {
      if (!confirm('¿Empezar un jefe nuevo? Lo que no hayas guardado de este se pierde.')) return;
      currentBossId = null;
      state = createDefaultBossConfig();
      renderAll();
    };
    bar.appendChild(fresh);
    identitySection.appendChild(bar);
    identitySection.appendChild(F.textField({
      label: 'Nombre', value: state.displayName, placeholder: 'ej. Coloso de Hielo',
      hint: 'Sale en la barra de vida. También da nombre a los assets (sin espacios ni tildes).',
      onChange: (v) => { state.displayName = v; refresh(); },
    }).row);
    identitySection.appendChild(F.textField({
      label: 'Epíteto', value: state.title, placeholder: 'ej. Guardián del Glaciar',
      onChange: (v) => { state.title = v; refresh(); },
    }).row);
    identitySection.appendChild(F.textField({
      label: 'Descripción (nota de diseño)', value: state.description,
      onChange: (v) => { state.description = v; refresh(); },
    }).row);
  }

  // ============================================================ cuerpo

  const sourceCache = new Map(); // libraryId -> Promise<canvas>

  function sourceOf(entry) {
    if (!sourceCache.has(entry.id)) {
      sourceCache.set(entry.id, loadImageFromDataURL(entry.sprite.sourceDataURL).then((img) => {
        const c = document.createElement('canvas');
        c.width = entry.sprite.width; c.height = entry.sprite.height;
        c.getContext('2d').drawImage(img, 0, 0);
        return c;
      }));
    }
    return sourceCache.get(entry.id);
  }

  function entryOptions(select, value, preferredKinds, emptyLabel) {
    select.innerHTML = '';
    const none = el('option', null, emptyLabel);
    none.value = '';
    select.appendChild(none);
    const order = [...preferredKinds, ...['enemy', 'projectile', 'fx'].filter((k) => !preferredKinds.includes(k))];
    const names = { enemy: 'Enemigos', projectile: 'Proyectiles', fx: 'VFX' };
    order.forEach((kind) => {
      const list = entries.filter((e) => e.kind === kind);
      if (!list.length) return;
      const group = document.createElement('optgroup');
      group.label = names[kind] || kind;
      list.forEach((e) => {
        const o = el('option', null, `${e.enemyName} (${framedLanes(e).map((l) => l.name).join(', ') || 'sin frames'})`);
        o.value = e.id;
        group.appendChild(o);
      });
      select.appendChild(group);
    });
    select.value = entriesById.has(value) ? value : '';
  }

  function renderBody() {
    bodySection.innerHTML = '';
    const body = state.body;

    const pickRow = el('div', 'efRow');
    pickRow.appendChild(el('label', null, 'Animaciones del cuerpo (entrada de la biblioteca)'));
    const pick = document.createElement('select');
    entryOptions(pick, body.libraryId, ['enemy'], '(sin cuerpo — sólo ataques y fases)');
    pick.addEventListener('change', () => {
      body.libraryId = pick.value;
      body.entryName = entriesById.get(pick.value)?.enemyName || '';
      renderBody(); renderAttacks(); refresh();
    });
    pickRow.appendChild(pick);
    pickRow.appendChild(el('p', 'efHint',
      'Una hoja de la pestaña Sprites con una animación por gesto (Idle obligatoria; Walk si se mueve; '
      + 'una por ataque: GroundSlam, Punch…). Nombres sin "_". Pulsa un frame para marcarlo como el '
      + 'momento en que sale el golpe.'));
    bodySection.appendChild(pickRow);

    const entry = entriesById.get(body.libraryId);
    if (body.libraryId && !entry) {
      bodySection.appendChild(el('p', 'efSummaryError', 'La entrada elegida ya no está en la biblioteca.'));
    }

    if (entry) {
      const lanes = framedLanes(entry);
      const list = el('div', 'bcAnimList');
      lanes.forEach((lane) => {
        const o = body.anims[lane.name] || (body.anims[lane.name] = {});
        const row = el('div', 'bcAnimRow');

        const head = el('div', 'bcAnimHead');
        head.appendChild(el('span', 'bcAnimName', lane.name));
        head.appendChild(el('span', 'efHint', `${lane.frameBoxIndices.length} frames`));

        const fps = document.createElement('input');
        fps.type = 'number'; fps.min = 1; fps.step = 1; fps.value = o.fps ?? lane.fps ?? 10; fps.title = 'fps';
        fps.className = 'bcTiny';
        fps.addEventListener('input', () => { o.fps = Math.max(1, parseFloat(fps.value) || 10); refresh(); });
        head.appendChild(el('span', 'efHint', 'fps'));
        head.appendChild(fps);

        const loopWrap = el('label', 'bcInline');
        const loop = document.createElement('input');
        loop.type = 'checkbox'; loop.checked = o.loop ?? !!lane.loop;
        loop.addEventListener('change', () => { o.loop = loop.checked; refresh(); });
        loopWrap.appendChild(loop);
        loopWrap.appendChild(document.createTextNode(' bucle'));
        head.appendChild(loopWrap);

        const release = el('span', 'bcRelease');
        const setReleaseLabel = () => {
          release.textContent = Number.isInteger(o.releaseFrame) && o.releaseFrame >= 0
            ? `golpe en frame ${o.releaseFrame + 1}` : 'sin golpe';
        };
        setReleaseLabel();
        head.appendChild(release);
        row.appendChild(head);

        const strip = el('div', 'bcFrames');
        row.appendChild(strip);
        sourceOf(entry).then((source) => {
          lane.frameBoxIndices.forEach((boxIdx, i) => {
            const box = entry.sprite.boxes[boxIdx];
            if (!box) return;
            const c = cropBox(source, box);
            const thumb = el('div', 'bcFrame');
            thumb.style.backgroundImage = `url(${c.toDataURL()})`;
            thumb.title = `Frame ${i + 1} — clic: el golpe sale aquí`;
            thumb.appendChild(el('span', null, String(i + 1)));
            if (o.releaseFrame === i) thumb.classList.add('release');
            thumb.addEventListener('click', () => {
              o.releaseFrame = o.releaseFrame === i ? -1 : i;
              strip.querySelectorAll('.bcFrame').forEach((t, j) => t.classList.toggle('release', o.releaseFrame === j));
              setReleaseLabel();
              refresh();
            });
            strip.appendChild(thumb);
          });
        });

        list.appendChild(row);
      });
      bodySection.appendChild(list);

      // --- derived clips
      const derivedBox = el('details', 'bcDetails');
      derivedBox.open = body.derived.length > 0;
      derivedBox.appendChild(el('summary', null, `Clips derivados (${body.derived.length}) — un tramo en bucle de otra animación, p. ej. el viaje de una embestida`));
      body.derived.forEach((d, i) => {
        const card = el('div', 'bcSubCard');
        const grid = el('div', 'bcGrid');
        grid.appendChild(F.textField({ label: 'Nombre', value: d.name, onChange: (v) => { d.name = v; refreshGestures(); refresh(); } }).row);
        grid.appendChild(F.enumField({
          label: 'Sale de', value: d.from,
          options: [{ value: '', label: '—' }, ...lanes.map((l) => ({ value: l.name, label: l.name }))],
          onChange: (v) => { d.from = v; refresh(); },
        }).row);
        grid.appendChild(F.numberField({ label: 'Primer frame (0 = el 1º)', value: d.first, min: 0, step: 1, onChange: (v) => { d.first = Math.round(v); refresh(); } }).row);
        grid.appendChild(F.numberField({ label: 'Nº de frames', value: d.count, min: 1, step: 1, onChange: (v) => { d.count = Math.round(v); refresh(); } }).row);
        grid.appendChild(F.numberField({ label: 'fps', value: d.fps, min: 1, onChange: (v) => { d.fps = v; refresh(); } }).row);
        grid.appendChild(F.boolField({ label: 'bucle', value: d.loop, onChange: (v) => { d.loop = v; refresh(); } }).row);
        card.appendChild(grid);
        const del = el('button', 'small danger', '✕ quitar');
        del.onclick = () => { body.derived.splice(i, 1); renderBody(); renderAttacks(); refresh(); };
        card.appendChild(del);
        derivedBox.appendChild(card);
      });
      const addDerived = el('button', 'small', '＋ Clip derivado');
      addDerived.onclick = () => {
        body.derived.push({ name: '', from: lanes[0]?.name || '', first: 0, count: 2, fps: 8, loop: true });
        renderBody(); refresh();
      };
      derivedBox.appendChild(addDerived);
      bodySection.appendChild(derivedBox);
    }

    // --- stats
    const statsBox = el('details', 'bcDetails');
    statsBox.appendChild(el('summary', null, 'Tamaño y stats del prefab'));
    statsBox.appendChild(el('p', 'efHint', 'Sólo se aplican la PRIMERA vez que se crea el prefab. Después se afinan en '
      + 'Unity y reimportar no los pisa (sí actualiza animaciones, ataques y fases).'));
    const grid = el('div', 'bcGrid');
    BODY_STATS.forEach(([key, label, , hint]) => {
      grid.appendChild(F.numberField({ label, value: body[key], min: 0, hint, onChange: (v) => { body[key] = v; refresh(); } }).row);
    });
    statsBox.appendChild(grid);
    bodySection.appendChild(statsBox);

    // --- movement
    const moveBox = el('div', 'bcSubCard');
    moveBox.appendChild(el('h3', 'bcH3', 'Movimiento entre ataques'));
    const movements = catalog?.movements || [];
    moveBox.appendChild(F.enumField({
      label: 'Tipo', value: body.movement?.type || '',
      options: [{ value: '', label: '(quieto)' }, ...movements.map((m) => ({ value: m.type, label: m.label }))],
      hint: 'Los mismos tipos que usará cualquier enemigo. Anima "Walk" mientras se mueve (si existe).',
      onChange: (v) => {
        const m = movements.find((x) => x.type === v);
        body.movement = v ? { type: v, params: m ? defaultsOf(m.params) : {} } : null;
        renderBody(); refresh();
      },
    }).row);
    const m = movements.find((x) => x.type === body.movement?.type);
    if (m) {
      body.movement.params = withDefaults(m.params, body.movement.params || {});
      const fields = el('div', 'bcParams');
      renderFields(fields, m.params, body.movement.params, { where: 'movimiento' });
      moveBox.appendChild(fields);
    }
    bodySection.appendChild(moveBox);

    // --- warnings
    const warnBox = el('div', 'bcSubCard');
    warnBox.appendChild(el('h3', 'bcH3', 'Avisos (los usa cualquier ataque)'));
    warnBox.appendChild(artPicker({ artKind: 'warning', artLabel: 'Aviso círculo', placeholder: 'barra de color' },
      body.warnCircle, (v) => { body.warnCircle = v; refresh(); }));
    warnBox.appendChild(artPicker({ artKind: 'warning', artLabel: 'Aviso flecha', placeholder: 'caja de color' },
      body.warnArrow, (v) => { body.warnArrow = v; refresh(); }));
    bodySection.appendChild(warnBox);
  }

  // ============================================================ campos genéricos (desde el catálogo)

  let gestureSelects = [];

  function gestureNames() { return bodyAnimationNames(state, entriesById); }

  function refreshGestures() {
    const names = gestureNames();
    gestureSelects = gestureSelects.filter((s) => s.select.isConnected);
    gestureSelects.forEach(({ select, get }) => fillGesture(select, names, get()));
  }

  function fillGesture(select, names, value) {
    select.innerHTML = '';
    const opts = [''].concat(names);
    if (value && !names.includes(value)) opts.push(value);
    opts.forEach((n) => {
      const empty = names.length ? '(ninguno)' : '(sin animaciones: elige el cuerpo arriba)';
      const o = el('option', null, n === '' ? empty : (names.includes(n) ? n : `${n} (no existe)`));
      o.value = n;
      select.appendChild(o);
    });
    select.value = value || '';
  }

  function gestureField(label, value, hint, onChange) {
    const row = el('div', 'efRow');
    row.appendChild(el('label', null, label));
    const select = document.createElement('select');
    let current = value;
    fillGesture(select, gestureNames(), current);
    select.addEventListener('change', () => { current = select.value; onChange(current); });
    gestureSelects.push({ select, get: () => current });
    row.appendChild(select);
    if (hint) row.appendChild(el('p', 'efHint', hint));
    return row;
  }

  const looksLikeGesture = (f) => f.type === 'string' && /(gesture|state)$/i.test(f.name);

  /** Renders a catalog field list into `container`, editing `values` in place. */
  function renderFields(container, fields, values, ctx, prefix = '') {
    fields.forEach((f) => {
      if (f.header) container.appendChild(el('h4', 'bcH4', f.header));
      const hint = f.tooltip || '';
      const set = (v) => { values[f.name] = v; if (ctx.edited) ctx.edited[`${prefix}${f.name}`] = true; refresh(); };

      switch (f.type) {
        case 'float':
        case 'int':
          container.appendChild(F.numberField({
            label: f.label, value: values[f.name], min: f.min, max: f.max, step: f.type === 'int' ? 1 : 'any', hint,
            onChange: (v) => set(f.type === 'int' ? Math.round(v) : v),
          }).row);
          break;
        case 'bool':
          container.appendChild(F.boolField({ label: f.label, value: values[f.name], hint, onChange: set }).row);
          break;
        case 'string':
          container.appendChild(looksLikeGesture(f)
            ? gestureField(`${f.label} (animación)`, values[f.name], hint, set)
            : F.textField({ label: f.label, value: values[f.name], hint, onChange: set }).row);
          break;
        case 'vector2':
          container.appendChild(F.vector2Field({ label: f.label, value: values[f.name] || { x: 0, y: 0 }, hint, onChange: set }).row);
          break;
        case 'color':
          container.appendChild(F.colorField({ label: f.label, value: values[f.name], hint, onChange: set }).row);
          break;
        case 'enum':
          container.appendChild(F.enumField({
            label: f.label, value: values[f.name], hint,
            options: f.options.map((o) => ({ value: o, label: o })), onChange: set,
          }).row);
          break;
        case 'floatList':
        case 'intList':
          container.appendChild(F.textField({
            label: `${f.label} (lista separada por comas)`, value: (values[f.name] || []).join(', '), hint,
            onChange: (v) => set(v.split(',').map((x) => parseFloat(x.trim())).filter(Number.isFinite)
              .map((n) => (f.type === 'intList' ? Math.round(n) : n))),
          }).row);
          break;
        case 'art':
          container.appendChild(artPicker(f, values[f.name], set));
          break;
        case 'object': {
          const box = el('div', 'bcObject');
          box.appendChild(el('div', 'bcObjectTitle', f.artLabel ? `${f.label} — ${f.artLabel}` : f.label));
          if (hint) box.appendChild(el('p', 'efHint', hint));
          values[f.name] = values[f.name] || {};
          renderFields(box, f.fields, values[f.name], ctx, `${prefix}${f.name}.`);
          container.appendChild(box);
          break;
        }
        default:
          container.appendChild(el('p', 'efHint', `${f.label}: se asigna en Unity (${f.unityType || 'asset'}).`));
      }
    });
  }

  /** Library picker for one art slot. Empty = the attack's own placeholder in Unity. */
  function artPicker(f, value, onChange) {
    const kindNote = ART_KIND_NOTES[f.artKind] || ART_KIND_NOTES.fx;
    const row = el('div', 'efRow bcArtRow');
    row.appendChild(el('label', null, `🎨 ${f.artLabel || f.label} · ${kindNote.label}`));

    const line = el('div', 'bcArtLine');
    const thumb = el('div', 'bcArtThumb');
    const select = document.createElement('select');
    entryOptions(select, value, kindNote.kinds, `(por defecto: ${f.placeholder || 'placeholder'})`);
    const paint = () => {
      const e = entriesById.get(select.value);
      thumb.style.backgroundImage = e?.thumbnail ? `url(${e.thumbnail})` : '';
      thumb.classList.toggle('empty', !e);
    };
    paint();
    select.addEventListener('change', () => { paint(); onChange(select.value); });
    line.appendChild(thumb);
    line.appendChild(select);
    row.appendChild(line);
    row.appendChild(el('p', 'efHint', `Necesita: ${kindNote.needs}`));
    return row;
  }

  // ============================================================ ataques

  function renderAttacks() {
    attacksSection.innerHTML = '';
    gestureSelects = gestureSelects.filter((s) => s.select.isConnected);

    attacksSection.appendChild(el('p', 'efHint',
      'Cada ataque es un tipo genérico (sirve para cualquier jefe o enemigo) con su gesto, sus números y '
      + 'su arte. Todo lo que dejes vacío funciona con un placeholder, para probar antes de tener arte.'));

    const list = el('div', 'bcList');
    state.attacks.forEach((attack, index) => list.appendChild(attackCard(attack, index)));
    attacksSection.appendChild(list);

    const add = el('div', 'bcAddRow');
    const typeSelect = typeSelector('');
    add.appendChild(el('span', 'efHint', 'Nuevo:'));
    const addBtn = el('button', 'small primary', '＋ Añadir ataque');
    addBtn.disabled = !catalog;
    addBtn.onclick = () => {
      const a = createAttack(catalog, typeSelect.value, state.attacks.map((x) => x.id));
      state.attacks.push(a);
      renderAttacks(); renderPhases(); refresh();
    };
    add.appendChild(typeSelect);
    add.appendChild(addBtn);
    attacksSection.appendChild(add);
  }

  function typeSelector(value) {
    const select = document.createElement('select');
    const groups = new Map();
    (catalog?.attacks || []).forEach((a) => {
      const g = ATTACK_NOTES[a.type]?.group || 'Otros';
      if (!groups.has(g)) groups.set(g, []);
      groups.get(g).push(a);
    });
    groups.forEach((list, g) => {
      const og = document.createElement('optgroup');
      og.label = g;
      list.forEach((a) => {
        const o = el('option', null, `${attackLabel(a.type, a)} — ${a.type}`);
        o.value = a.type;
        og.appendChild(o);
      });
      select.appendChild(og);
    });
    if (value && ![...select.options].some((o) => o.value === value)) {
      const o = el('option', null, `${value} (no está en el catálogo)`);
      o.value = value;
      select.appendChild(o);
    }
    if (value) select.value = value;
    return select;
  }

  function artSlotsOf(fields, prefix = '') {
    const out = [];
    (fields || []).forEach((f) => {
      if (f.type === 'art') out.push({ label: f.artLabel || f.label, kind: f.artKind, placeholder: f.placeholder });
      if (f.type === 'object') out.push(...artSlotsOf(f.fields, `${prefix}${f.name}.`));
    });
    return out;
  }

  function attackCard(attack, index) {
    const typeEntry = catalog?.attacks.find((a) => a.type === attack.type);
    const card = el('div', 'bcCard');

    const head = el('div', 'bcCardHead');
    const title = el('span', 'bcCardTitle bcClickable', `${attack.open ? '▾' : '▸'} ${attack.id} · ${attackLabel(attack.type, typeEntry)}`);
    title.onclick = () => { attack.open = !attack.open; renderAttacks(); };
    head.appendChild(title);

    const btns = el('div', 'bcBtns');
    const mk = (txt, tip, fn, cls = 'small') => { const b = el('button', cls, txt); b.title = tip; b.onclick = fn; btns.appendChild(b); return b; };
    mk('↑', 'Subir', () => { if (index > 0) { [state.attacks[index - 1], state.attacks[index]] = [state.attacks[index], state.attacks[index - 1]]; renderAttacks(); renderPhases(); refresh(); } }).disabled = index === 0;
    mk('↓', 'Bajar', () => { if (index < state.attacks.length - 1) { [state.attacks[index + 1], state.attacks[index]] = [state.attacks[index], state.attacks[index + 1]]; renderAttacks(); renderPhases(); refresh(); } }).disabled = index === state.attacks.length - 1;
    mk('⧉', 'Duplicar', () => {
      const copy = clone(attack);
      let n = 2; while (state.attacks.some((a) => a.id === `${attack.id}${n}`)) n++;
      copy.id = `${attack.id}${n}`;
      state.attacks.splice(index + 1, 0, copy);
      renderAttacks(); renderPhases(); refresh();
    });
    mk('✕', 'Eliminar (y quitarlo de las fases)', () => {
      state.attacks.splice(index, 1);
      state.phases.forEach((p) => {
        p.attacks = p.attacks.filter((id) => id !== attack.id);
        if (p.openingAttack === attack.id) p.openingAttack = '';
      });
      renderAttacks(); renderPhases(); refresh();
    }, 'small danger');
    head.appendChild(btns);
    card.appendChild(head);

    const slots = artSlotsOf(typeEntry?.params);
    const summary = el('p', 'efHint');
    const gestureTxt = attack.common.gesture ? `gesto ${attack.common.gesture}` : 'sin gesto';
    summary.textContent = `${ATTACK_NOTES[attack.type]?.note || ''} · ${gestureTxt} · daño ${attack.common.damage ?? '?'}`
      + (slots.length ? ` · arte: ${slots.map((s) => s.label).join(', ')}` : '');
    card.appendChild(summary);

    if (!attack.open) return card;

    const grid = el('div', 'bcGrid');
    const idField = F.textField({
      label: 'Id (lo usan las fases)', value: attack.id,
      hint: 'Letras, números, - y _. Se aplica al salir del campo.',
      onChange: () => {},
    });
    idField.input.addEventListener('change', () => {
      const v = idField.input.value.trim();
      const old = attack.id;
      if (v === old) return;
      if (!/^[A-Za-z][A-Za-z0-9_-]*$/.test(v) || state.attacks.some((a) => a !== attack && a.id === v)) {
        idField.setError(!/^[A-Za-z][A-Za-z0-9_-]*$/.test(v) ? 'Id no válido.' : 'Ya hay un ataque con ese id.');
        idField.input.value = old;
        return;
      }
      idField.setError(null);
      attack.id = v;
      state.phases.forEach((p) => {
        p.attacks = p.attacks.map((id) => (id === old ? v : id));
        if (p.openingAttack === old) p.openingAttack = v;
      });
      renderAttacks(); renderPhases(); refresh();
    });
    grid.appendChild(idField.row);

    const typeRow = el('div', 'efRow');
    typeRow.appendChild(el('label', null, 'Tipo de ataque'));
    const ts = typeSelector(attack.type);
    ts.disabled = !catalog;
    ts.addEventListener('change', () => {
      const entry = catalog.attacks.find((a) => a.type === ts.value);
      attack.type = ts.value;
      attack.params = defaultsOf(entry?.params || []);
      // Params belong to the old type; base-field edits still apply.
      Object.keys(attack.edited || {}).forEach((k) => { if (!(k.split('.')[0] in attack.common)) delete attack.edited[k]; });
      renderAttacks(); refresh();
    });
    typeRow.appendChild(ts);
    grid.appendChild(typeRow);

    grid.appendChild(gestureField('Gesto (animación del cuerpo)', attack.common.gesture,
      'La animación que hace de aviso. El golpe sale en su frame marcado.',
      (v) => { attack.common.gesture = v; refresh(); }));
    card.appendChild(grid);

    // Base fields: the common ones in view, the rest folded.
    const common = (catalog?.common || []).filter((f) => f.name !== 'gesture');
    attack.common = withDefaults(common, attack.common);
    const main = ['displayName', 'telegraph', 'recovery', 'damage', 'minPhase', 'cooldownInAttacks'];
    const mainGrid = el('div', 'bcGrid');
    attack.edited = attack.edited || {};
    renderFields(mainGrid, common.filter((f) => main.includes(f.name)).map((f) => ({ ...f, header: undefined })), attack.common, { edited: attack.edited });
    card.appendChild(mainGrid);

    const more = el('details', 'bcDetails');
    more.appendChild(el('summary', null, 'Más (peso, castigo, temblor, sonido…)'));
    const moreGrid = el('div', 'bcGrid');
    renderFields(moreGrid, common.filter((f) => !main.includes(f.name)).map((f) => ({ ...f, header: undefined })), attack.common, { edited: attack.edited });
    more.appendChild(moreGrid);
    card.appendChild(more);

    if (typeEntry) {
      attack.params = withDefaults(typeEntry.params, attack.params || {});
      const params = el('details', 'bcDetails');
      params.open = true;
      params.appendChild(el('summary', null, `Parámetros de ${attackLabel(attack.type, typeEntry)}`));
      const box = el('div', 'bcParams');
      renderFields(box, typeEntry.params, attack.params, { where: attack.id, edited: attack.edited });
      params.appendChild(box);
      card.appendChild(params);
    } else if (catalog) {
      card.appendChild(el('p', 'efSummaryError', `"${attack.type}" no está en el catálogo de Unity.`));
    }

    return card;
  }

  // ============================================================ arte usado

  let artSignature = '';

  function renderArt(exported) {
    const sig = exported.art.map((a) => a.key).join('|') + `#${entries.length}`;
    if (sig === artSignature) return;
    artSignature = sig;

    artSection.innerHTML = '';
    if (exported.art.length === 0) {
      artSection.appendChild(el('p', 'efHint', 'Ningún hueco de arte usa la biblioteca todavía: todo saldrá con placeholders.'));
      return;
    }
    artSection.appendChild(el('p', 'efHint', 'Cada arte de la biblioteca que usan los ataques se convierte en un prefab. '
      + 'El tamaño de proyectiles y objetos lo manda el "size" del ataque; el de aquí es el de partida.'));
    exported.art.forEach(({ id, key, kind, libraryId }) => {
      const entry = entriesById.get(libraryId);
      const opts = state.art[key] || (state.art[key] = createArtOptions(kind));
      const card = el('div', 'bcSubCard');
      const head = el('div', 'bcArtLine');
      const thumb = el('div', 'bcArtThumb');
      if (entry?.thumbnail) thumb.style.backgroundImage = `url(${entry.thumbnail})`;
      head.appendChild(thumb);
      head.appendChild(el('span', 'bcAnimName', `${id} — ${ART_KIND_NOTES[kind]?.label || kind} · ${framedLanes(entry).map((l) => l.name).join(', ')}`));
      card.appendChild(head);
      const grid = el('div', 'bcGrid');
      if (kind !== 'warning') grid.appendChild(F.numberField({ label: 'Ancho en el mundo (u)', value: opts.worldWidth, min: 0.05, onChange: (v) => { opts.worldWidth = v; refresh(); } }).row);
      if (kind === 'projectile') {
        grid.appendChild(F.numberField({ label: 'Collider (× dibujo)', value: opts.colliderScale, min: 0.05, onChange: (v) => { opts.colliderScale = v; refresh(); } }).row);
        grid.appendChild(F.boolField({ label: 'Gira hacia donde vuela', value: opts.faceTravelDirection, onChange: (v) => { opts.faceTravelDirection = v; refresh(); } }).row);
      }
      if (kind === 'warning') grid.appendChild(F.boolField({ label: 'Teñir con el color de la fase', value: opts.tint, onChange: (v) => { opts.tint = v; refresh(); } }).row);
      card.appendChild(grid);
      artSection.appendChild(card);
    });
  }

  // ============================================================ fases

  function renderPhases() {
    phasesSection.innerHTML = '';
    phasesSection.appendChild(el('p', 'efHint', 'De más vida a menos. Cada fase reparte de su mazo; la 1ª empieza siempre en 1.'));

    const list = el('div', 'bcList');
    state.phases.forEach((phase, index) => {
      const card = el('div', 'bcCard');
      const head = el('div', 'bcCardHead');
      const title = el('span', 'bcCardTitle', `${index + 1}. ${phase.displayName}`);
      head.appendChild(title);
      if (index > 0) {
        const del = el('button', 'small danger', '✕');
        del.onclick = () => { state.phases.splice(index, 1); renderPhases(); refresh(); };
        head.appendChild(del);
      }
      card.appendChild(head);

      const grid = el('div', 'bcGrid');
      grid.appendChild(F.textField({ label: 'Nombre', value: phase.displayName, onChange: (v) => { phase.displayName = v; title.textContent = `${index + 1}. ${v}`; refresh(); } }).row);
      const health = F.numberField({ label: 'Empieza por debajo de (vida 0-1)', value: index === 0 ? 1 : phase.startsAtHealth, min: 0, max: 1, onChange: (v) => { phase.startsAtHealth = v; refresh(); } });
      if (index === 0) health.input.disabled = true;
      grid.appendChild(health.row);
      grid.appendChild(F.vector2Field({ label: 'Pausa entre ataques (mín, máx s)', value: phase.pauseBetweenAttacks, hint: 'x = mínimo, y = máximo. Se mueve durante la pausa.', onChange: (v) => { phase.pauseBetweenAttacks = v; refresh(); } }).row);
      [
        ['damageScale', 'Daño hecho ×', 0.01],
        ['damageTakenMultiplier', 'Daño recibido ×', 0.01],
        ['speedScale', 'Ritmo ×', 0.1],
        ['transitionSeconds', 'Transición (s)', 0],
        ['transitionShake', 'Temblor de transición', 0],
        ['frenzyBelowHealth', 'Frenesí por debajo de (vida 0-1)', 0],
        ['frenzySpeedScale', 'Ritmo en frenesí ×', 1],
      ].forEach(([key, label, min]) => {
        grid.appendChild(F.numberField({ label, value: phase[key], min, onChange: (v) => { phase[key] = v; refresh(); } }).row);
      });
      grid.appendChild(F.colorField({ label: 'Color de la fase', value: phase.accent, hint: 'Tiñe los placeholders.', onChange: (v) => { phase.accent = v; refresh(); } }).row);
      grid.appendChild(F.enumField({
        label: 'Ataque de apertura', value: phase.openingAttack || '',
        options: [{ value: '', label: '(ninguno)' }, ...state.attacks.map((a) => ({ value: a.id, label: a.id }))],
        hint: 'Lo hace nada más empezar la fase.',
        onChange: (v) => { phase.openingAttack = v; refresh(); },
      }).row);
      card.appendChild(grid);

      const deckRow = el('div', 'efRow');
      deckRow.appendChild(el('label', null, 'Mazo'));
      const deck = el('div', 'bcDeck');
      if (state.attacks.length === 0) deck.appendChild(el('p', 'efHint', 'Añade ataques arriba.'));
      state.attacks.forEach((attack) => {
        const wrap = el('label', 'efCheckboxRow');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.checked = phase.attacks.includes(attack.id);
        cb.addEventListener('change', () => {
          phase.attacks = cb.checked ? [...phase.attacks, attack.id] : phase.attacks.filter((id) => id !== attack.id);
          refresh();
        });
        wrap.appendChild(cb);
        const minPhase = attack.common?.minPhase ?? 1;
        wrap.appendChild(document.createTextNode(` ${attack.id} · ${attackLabel(attack.type, catalog?.attacks.find((a) => a.type === attack.type))}`
          + (minPhase > index + 1 ? `  (fase mínima ${minPhase}: aquí no sale)` : '')));
        deck.appendChild(wrap);
      });
      deckRow.appendChild(deck);
      card.appendChild(deckRow);
      list.appendChild(card);
    });
    phasesSection.appendChild(list);

    const add = el('button', 'small', '＋ Añadir fase');
    add.onclick = () => { state.phases.push(createDefaultPhase(state.phases.length)); renderPhases(); refresh(); };
    phasesSection.appendChild(add);
  }

  // ============================================================ preview + validación

  function refresh() {
    const exported = buildBossExport(state, catalog, entriesById);
    lastExport = exported;
    previewEl.textContent = JSON.stringify(exported.doc, null, 2);
    renderArt(exported);

    let schemaErrors = [];
    if (validate) {
      const { valid, errors } = validateConfig(validate, exported.doc);
      if (!valid) schemaErrors = errors;
    }

    const blocking = exported.problems.length + schemaErrors.length;
    exportBtn.disabled = blocking > 0;
    if (zipBtn) zipBtn.disabled = blocking > 0;
    if (saveBtn) saveBtn.disabled = !state.displayName.trim();

    let html = blocking === 0
      ? '<p class="efSummaryOk">✓ Listo para exportar</p>'
      : `<p class="efSummaryError">✗ ${blocking} cosa${blocking > 1 ? 's' : ''} que arreglar:</p>`;
    if (exported.problems.length) html += `<ul>${exported.problems.map((p) => `<li>${escapeHtml(p)}</li>`).join('')}</ul>`;
    if (schemaErrors.length) html += `<ul>${schemaErrors.map((e) => `<li><code>${escapeHtml(e.path || '(raíz)')}</code> — ${escapeHtml(e.message)}</li>`).join('')}</ul>`;
    if (exported.notes.length) html += `<p class="efHint">Avisos:</p><ul class="bcNotes">${exported.notes.map((n) => `<li>${escapeHtml(n)}</li>`).join('')}</ul>`;
    html += '<p class="efHint">Unity: <em>Tools ▸ Web ▸ Import Config… ▸ Importar jefe completo (.zip)</em>. '
      + 'El .json suelto sirve para reimportar sólo números cuando los frames ya están en Unity.</p>';
    summaryEl.innerHTML = html;
  }

  // ============================================================ exportar / guardar

  const fileBase = () => (state.displayName.trim() || 'Boss').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^A-Za-z0-9]/g, '');

  exportBtn.addEventListener('click', () => {
    const { doc } = buildBossExport(state, catalog, entriesById);
    downloadBlob(new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' }), `${fileBase()}.boss-config.json`);
    persistIfSaved(doc);
  });

  if (zipBtn) {
    zipBtn.addEventListener('click', async () => {
      zipBtn.disabled = true;
      const old = zipBtn.textContent;
      zipBtn.textContent = '⏳ Empaquetando…';
      try {
        const { doc } = buildBossExport(state, catalog, entriesById);
        const blob = await buildBossBundle(doc, entriesById);
        downloadBlob(blob, `Boss_${fileBase()}.zip`);
        persistIfSaved(doc);
      } catch (e) {
        summaryEl.insertAdjacentHTML('afterbegin', `<p class="efSummaryError">No se pudo crear el zip: ${escapeHtml(e.message)}</p>`);
      } finally {
        zipBtn.textContent = old;
        refresh();
      }
    });
  }

  async function persistIfSaved(doc) {
    if (!currentBossId) return;
    try { if (!(await getEnemy(currentBossId))) { currentBossId = null; return; } await updateEnemy(currentBossId, { config: doc, draft: clone(state) }); } catch { /* la descarga ya salió */ }
  }

  if (saveBtn) {
    saveBtn.addEventListener('click', async () => {
      const { doc } = buildBossExport(state, catalog, entriesById);
      const thumbnail = entriesById.get(state.body.libraryId)?.thumbnail || null;
      const patch = { enemyName: state.displayName.trim() || 'Jefe', thumbnail, config: doc, draft: clone(state) };
      let record;
      try {
        record = currentBossId && await getEnemy(currentBossId)
          ? await updateEnemy(currentBossId, patch)
          : await saveEnemy({ kind: 'boss', sprite: null, ...patch });
      } catch (e) {
        summaryEl.insertAdjacentHTML('afterbegin', `<p class="efSummaryError">No se pudo guardar: ${escapeHtml(e.message)}</p>`);
        return;
      }
      currentBossId = record.id;
      summaryEl.insertAdjacentHTML('afterbegin', `<p class="efSummaryOk">✓ "${escapeHtml(record.enemyName)}" guardado (${new Date(record.updatedAt).toLocaleTimeString()}).</p>`);
      renderIdentity();
      if (onSaved) onSaved(record);
    });
  }

  // ============================================================ reabrir

  /** A saved draft back into a valid state (fills anything added since it was saved). */
  function adoptDraft(draft) {
    const base = createDefaultBossConfig();
    const s = { ...base, ...clone(draft) };
    s.body = { ...createDefaultBody(), ...(s.body || {}) };
    s.art = s.art || {};
    s.attacks = (s.attacks || []).map((a) => ({ open: false, common: {}, params: {}, edited: {}, ...a }));
    s.phases = (s.phases && s.phases.length ? s.phases : [createDefaultPhase(0)])
      .map((p, i) => ({ ...createDefaultPhase(i), ...p }));
    return s;
  }

  /** A BossConfig saved by the older form (no draft): attacks and phases, nothing else. */
  function adoptLegacyConfig(config) {
    const s = createDefaultBossConfig();
    s.displayName = config.displayName || '';
    s.title = config.title || '';
    s.description = config.description || '';
    const commonNames = (catalog?.common || []).map((f) => f.name);
    s.attacks = Object.entries(config.attacks || {}).map(([id, a]) => {
      const typeEntry = catalog?.attacks.find((t) => t.type === a.type);
      const common = defaultsOf(catalog?.common || []);
      Object.entries(a).forEach(([k, v]) => { if (commonNames.includes(k)) common[k] = v; });
      const params = { ...defaultsOf(typeEntry?.params || []), ...(a.params || {}) };
      if (params.projectile && params.projectile.libraryId) {
        params.projectile = { ...params.projectile, prefab: params.projectile.libraryId };
        delete params.projectile.libraryId;
      }
      return { id, type: a.type, open: false, common, params };
    });
    s.phases = (config.phases || []).map((p, i) => ({ ...createDefaultPhase(i), ...p, openingAttack: p.openingAttack || '' }));
    if (!s.phases.length) s.phases = [createDefaultPhase(0)];
    // Legacy values are already non-default where they matter; mark them so they keep exporting.
    s.attacks.forEach((a) => { a.edited = {}; });
    return s;
  }

  async function linkLibraryEntry(id) {
    await catalogReady;
    const entry = await getEnemy(id);
    if (!entry) return;
    currentBossId = entry.id;
    state = entry.draft ? adoptDraft(entry.draft) : adoptLegacyConfig(entry.config || {});
    resetAttackIdCounter(0); // createAttack skips ids already in use
    await loadEntries();
    renderAll();
  }

  async function loadEntries() {
    entries = (await listEntries()).filter((e) => e.sprite && e.kind !== 'boss');
    entriesById = new Map(entries.map((e) => [e.id, e]));
    sourceCache.clear();
    artSignature = '';
  }

  /** Called when the tab is shown: the library may have changed in another tab. */
  async function refreshLibraries() {
    await catalogReady;
    await loadEntries();
    renderAll();
  }

  catalogReady.then(() => loadEntries()).then(renderAll);

  return { refreshLibraries, linkLibraryEntry };
}
