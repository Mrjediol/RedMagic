// modules/attack-cards.js
// -----------------------------------------------------------------------------
// The catalog-driven ATTACK CARDS as a reusable editor: a list of attacks, each one a generic type
// from Unity's catalog (modules/boss-catalog.js) with its base fields, its gesture, its parameters
// and its art slots. Nothing about a specific attack is hardcoded: fields, defaults, tooltips and
// art slots all come from the catalog, so a new C# archetype shows up with no web change.
//
// Used by the Enemy Creator's "Ataques" section (the deck of a normal enemy). It is the same card
// the Boss Creator draws (modules/boss-form.js, which keeps its own copy for now — it predates this
// module and is left untouched on purpose; move it onto this editor when that tab is next reworked).
//
// The attack STATE is the Boss Creator's shape, so both tabs export through the same rules:
//   { id, type, open, common: {base fields}, params: {type fields}, edited: {path: true}, ...extra }
//
// RENDERING: the list is rebuilt on a STRUCTURAL change (add/remove/reorder/duplicate/type/id);
// scalar edits mutate the state in place and only call onChange() — a rebuild per keystroke would
// lose focus mid-typing.

import * as F from './enemy-form-fields.js';
import { defaultsOf, withDefaults, clone } from './boss-defaults.js';
import { ATTACK_NOTES, ART_KIND_NOTES, attackLabel, attackGroup } from './boss-catalog-notes.js';
import { framedLanes } from './boss-export.js';

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

const ID_PATTERN = /^[A-Za-z][A-Za-z0-9_-]*$/;

/** A fresh attack of `type` (catalog defaults), with the first free "AtaqueN" id. */
export function newAttack(catalog, type, takenIds = []) {
  const taken = new Set(takenIds);
  let n = 1;
  while (taken.has(`Ataque${n}`)) n++;
  const entry = catalog?.attacks.find((a) => a.type === type) || catalog?.attacks[0];
  return {
    id: `Ataque${n}`,
    // Field paths edited in the web ('damage', 'projectile.speed'…): always exported, even when put
    // back to the default, so the web's value wins over what the asset had.
    edited: {},
    type: entry ? entry.type : type,
    open: true,
    common: { ...defaultsOf(catalog?.common || []), displayName: '' },
    params: defaultsOf(entry?.params || []),
  };
}

/**
 * @param {object} o
 * @param {HTMLElement} o.root - where the list is drawn (cleared on every render).
 * @param {() => object|null} o.getCatalog - Unity's catalog, or null while it is missing.
 * @param {() => object[]} o.getAttacks - the attack states (edited in place).
 * @param {() => object[]} o.getEntries - library entries that own a sheet (art slot pickers).
 * @param {() => string[]} o.gestureNames - gestures the body has (rig gestures / animation names).
 * @param {string} [o.noGesturesLabel] - the empty option's text when the body has none.
 * @param {(a: object) => boolean} [o.typeFilter] - which catalog types are offered.
 * @param {string[]} [o.mainCommon] - base fields shown up front; the rest go in a folded block.
 * @param {string[]} [o.hiddenCommon] - base fields not shown at all (they do nothing for this owner).
 * @param {(attack: object, card: HTMLElement) => void} [o.renderExtra] - owner-specific rows.
 * @param {string} [o.intro] - hint paragraph above the list.
 * @param {string} [o.idHint] - hint under the id field.
 * @param {() => void} o.onChange - a value changed (refresh preview/validation).
 * @param {() => void} [o.onStructure] - the list changed shape (already re-rendered).
 * @param {(attack: object) => void} [o.onAdded]
 * @param {(attack: object) => void} [o.onRemoved]
 * @param {(oldId: string, newId: string) => void} [o.onRenamed]
 */
export function createAttackEditor(o) {
  const mainCommon = o.mainCommon || ['displayName', 'telegraph', 'recovery', 'damage', 'cooldownInAttacks'];
  const hiddenCommon = new Set(o.hiddenCommon || []);
  const typeFilter = o.typeFilter || (() => true);
  const changed = () => o.onChange();
  const structure = () => { render(); if (o.onStructure) o.onStructure(); o.onChange(); };

  const entriesById = () => new Map(o.getEntries().map((e) => [e.id, e]));

  // ============================================================ gestos

  let gestureSelects = [];

  function fillGesture(select, names, value) {
    select.innerHTML = '';
    const opts = [''].concat(names);
    if (value && !names.includes(value)) opts.push(value);
    opts.forEach((n) => {
      const empty = names.length ? '(ninguno)' : (o.noGesturesLabel || '(el cuerpo no tiene gestos)');
      const opt = el('option', null, n === '' ? empty : (names.includes(n) ? n : `${n} (no existe)`));
      opt.value = n;
      select.appendChild(opt);
    });
    select.value = value || '';
  }

  function gestureField(label, value, hint, onChange) {
    const row = el('div', 'efRow');
    row.appendChild(el('label', null, label));
    const select = document.createElement('select');
    let current = value;
    fillGesture(select, o.gestureNames(), current);
    select.addEventListener('change', () => { current = select.value; onChange(current); });
    gestureSelects.push({ select, get: () => current });
    row.appendChild(select);
    if (hint) row.appendChild(el('p', 'efHint', hint));
    return row;
  }

  /** The body's gestures changed (another art picked): refill every gesture dropdown in place. */
  function refreshGestures() {
    const names = o.gestureNames();
    gestureSelects = gestureSelects.filter((s) => s.select.isConnected);
    gestureSelects.forEach(({ select, get }) => fillGesture(select, names, get()));
  }

  const looksLikeGesture = (f) => f.type === 'string' && /(gesture|state)$/i.test(f.name);

  // ============================================================ campos genéricos (desde el catálogo)

  function entryOptions(select, value, preferredKinds, emptyLabel) {
    const entries = o.getEntries();
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
        const opt = el('option', null, `${e.enemyName} (${framedLanes(e).map((l) => l.name).join(', ') || 'sin frames'})`);
        opt.value = e.id;
        group.appendChild(opt);
      });
      select.appendChild(group);
    });
    select.value = entries.some((e) => e.id === value) ? value : '';
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
      const e = entriesById().get(select.value);
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

  /** Renders a catalog field list into `container`, editing `values` in place. */
  function renderFields(container, fields, values, ctx, prefix = '') {
    fields.forEach((f) => {
      if (f.header) container.appendChild(el('h4', 'bcH4', f.header));
      const hint = f.tooltip || '';
      const set = (v) => { values[f.name] = v; if (ctx.edited) ctx.edited[`${prefix}${f.name}`] = true; changed(); };

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
            ? gestureField(`${f.label} (gesto)`, values[f.name], hint, set)
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
            options: f.options.map((v) => ({ value: v, label: v })), onChange: set,
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

  // ============================================================ tipos

  function typeSelector(value) {
    const catalog = o.getCatalog();
    const select = document.createElement('select');
    const groups = new Map();
    // Classic groups first, then "De partes" (stable sort keeps the catalog order inside each).
    [...(catalog?.attacks || [])].filter(typeFilter)
      .sort((a, b) => (a.category === 'rig') - (b.category === 'rig')).forEach((a) => {
        const g = attackGroup(a);
        if (!groups.has(g)) groups.set(g, []);
        groups.get(g).push(a);
      });
    groups.forEach((list, g) => {
      const og = document.createElement('optgroup');
      og.label = g;
      list.forEach((a) => {
        const opt = el('option', null, `${attackLabel(a.type, a)} — ${a.type}`);
        opt.value = a.type;
        og.appendChild(opt);
      });
      select.appendChild(og);
    });
    if (value && ![...select.options].some((opt) => opt.value === value)) {
      const known = catalog?.attacks.some((a) => a.type === value);
      const opt = el('option', null, `${value} (${known ? 'no vale aquí' : 'no está en el catálogo'})`);
      opt.value = value;
      select.appendChild(opt);
    }
    if (value) select.value = value;
    return select;
  }

  function artSlotsOf(fields) {
    const out = [];
    (fields || []).forEach((f) => {
      if (f.type === 'art') out.push({ label: f.artLabel || f.label });
      if (f.type === 'object') out.push(...artSlotsOf(f.fields));
    });
    return out;
  }

  // ============================================================ carta

  function attackCard(attack, index) {
    const catalog = o.getCatalog();
    const attacks = o.getAttacks();
    const typeEntry = catalog?.attacks.find((a) => a.type === attack.type);
    const card = el('div', 'bcCard');

    const head = el('div', 'bcCardHead');
    const title = el('span', 'bcCardTitle bcClickable', `${attack.open ? '▾' : '▸'} ${attack.id} · ${attackLabel(attack.type, typeEntry)}`);
    title.onclick = () => { attack.open = !attack.open; render(); };
    head.appendChild(title);

    const btns = el('div', 'bcBtns');
    const mk = (txt, tip, fn, cls = 'small') => { const b = el('button', cls, txt); b.title = tip; b.onclick = fn; btns.appendChild(b); return b; };
    mk('↑', 'Subir', () => { [attacks[index - 1], attacks[index]] = [attacks[index], attacks[index - 1]]; structure(); }).disabled = index === 0;
    mk('↓', 'Bajar', () => { [attacks[index + 1], attacks[index]] = [attacks[index], attacks[index + 1]]; structure(); }).disabled = index === attacks.length - 1;
    mk('⧉', 'Duplicar', () => {
      const copy = clone(attack);
      let n = 2; while (attacks.some((a) => a.id === `${attack.id}${n}`)) n++;
      copy.id = `${attack.id}${n}`;
      attacks.splice(index + 1, 0, copy);
      if (o.onAdded) o.onAdded(copy);
      structure();
    });
    mk('✕', 'Eliminar', () => {
      attacks.splice(index, 1);
      if (o.onRemoved) o.onRemoved(attack);
      structure();
    }, 'small danger');
    head.appendChild(btns);
    card.appendChild(head);

    const slots = artSlotsOf(typeEntry?.params);
    const gestureTxt = attack.common.gesture ? `gesto ${attack.common.gesture}` : 'sin gesto';
    card.appendChild(el('p', 'efHint', `${ATTACK_NOTES[attack.type]?.note || ''} · ${gestureTxt} · daño ${attack.common.damage ?? '?'}`
      + (slots.length ? ` · arte: ${slots.map((s) => s.label).join(', ')}` : '')));

    if (!attack.open) return card;

    const grid = el('div', 'bcGrid');
    const idField = F.textField({
      label: 'Id', value: attack.id,
      hint: o.idHint || 'Letras, números, - y _. Se aplica al salir del campo.',
      onChange: () => {},
    });
    idField.input.addEventListener('change', () => {
      const v = idField.input.value.trim();
      const old = attack.id;
      if (v === old) return;
      if (!ID_PATTERN.test(v) || attacks.some((a) => a !== attack && a.id === v)) {
        idField.setError(!ID_PATTERN.test(v) ? 'Id no válido.' : 'Ya hay un ataque con ese id.');
        idField.input.value = old;
        return;
      }
      idField.setError(null);
      attack.id = v;
      if (o.onRenamed) o.onRenamed(old, v);
      structure();
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
      structure();
    });
    typeRow.appendChild(ts);
    grid.appendChild(typeRow);

    grid.appendChild(gestureField('Gesto (el aviso del cuerpo)', attack.common.gesture,
      'El gesto que hace de aviso: el ataque sale cuando el gesto suelta.',
      (v) => { attack.common.gesture = v; changed(); }));
    card.appendChild(grid);

    if (o.renderExtra) o.renderExtra(attack, card);

    // Base fields: the common ones in view, the rest folded.
    const common = (catalog?.common || []).filter((f) => f.name !== 'gesture' && !hiddenCommon.has(f.name));
    attack.common = withDefaults(catalog?.common || [], attack.common);
    attack.edited = attack.edited || {};
    const plain = (list) => list.map((f) => ({ ...f, header: undefined }));

    const mainGrid = el('div', 'bcGrid');
    renderFields(mainGrid, plain(common.filter((f) => mainCommon.includes(f.name))), attack.common, { edited: attack.edited });
    card.appendChild(mainGrid);

    const rest = common.filter((f) => !mainCommon.includes(f.name));
    if (rest.length) {
      const more = el('details', 'bcDetails');
      more.appendChild(el('summary', null, 'Más (peso, enfriamiento, temblor…)'));
      const moreGrid = el('div', 'bcGrid');
      renderFields(moreGrid, plain(rest), attack.common, { edited: attack.edited });
      more.appendChild(moreGrid);
      card.appendChild(more);
    }

    if (typeEntry) {
      attack.params = withDefaults(typeEntry.params, attack.params || {});
      const params = el('details', 'bcDetails');
      params.open = true;
      params.appendChild(el('summary', null, `Parámetros de ${attackLabel(attack.type, typeEntry)}`));
      const box = el('div', 'bcParams');
      renderFields(box, typeEntry.params, attack.params, { edited: attack.edited });
      params.appendChild(box);
      card.appendChild(params);
    } else if (catalog) {
      card.appendChild(el('p', 'efSummaryError', `"${attack.type}" no está en el catálogo de Unity.`));
    }

    return card;
  }

  // ============================================================ lista

  function render() {
    const catalog = o.getCatalog();
    const attacks = o.getAttacks();
    o.root.innerHTML = '';
    gestureSelects = [];

    if (!catalog) {
      o.root.appendChild(el('p', 'efSummaryError', 'Falta el catálogo de Unity (modules/boss-catalog.js).'));
      o.root.appendChild(el('p', 'efHint', 'Abre el proyecto en Unity una vez (se genera solo al compilar) o usa '
        + 'Tools ▸ Web ▸ Exportar catálogo de jefes, y recarga esta página.'));
      return;
    }

    if (o.intro) o.root.appendChild(el('p', 'efHint', o.intro));

    const list = el('div', 'bcList');
    attacks.forEach((attack, index) => list.appendChild(attackCard(attack, index)));
    o.root.appendChild(list);

    const add = el('div', 'bcAddRow');
    const typeSelect = typeSelector('');
    add.appendChild(el('span', 'efHint', 'Nuevo:'));
    const addBtn = el('button', 'small primary', '＋ Añadir ataque');
    addBtn.onclick = () => {
      const attack = newAttack(catalog, typeSelect.value, attacks.map((a) => a.id));
      attacks.push(attack);
      if (o.onAdded) o.onAdded(attack);
      structure();
    };
    add.appendChild(typeSelect);
    add.appendChild(addBtn);
    o.root.appendChild(add);
  }

  return { render, refreshGestures };
}
