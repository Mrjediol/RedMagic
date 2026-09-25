// modules/fx-form.js
// -----------------------------------------------------------------------------
// The Projectile/FX Creator tab: authors an FxConfig (docs/schemas/fx-config.schema.json) for a
// projectile or a VFX as its OWN reusable library entity, instead of those numbers only existing
// nested inside whichever enemy happens to throw it.
//
// Built as a near-mirror of modules/enemy-form.js on purpose — same generic widgets
// (enemy-form-fields.js), same "Desde biblioteca" art picker, same live Ajv preview, same two
// export buttons, same persist-onto-the-linked-record rule. What differs is only the document:
// a much smaller one, and a `kind` switch that hides the projectile-only half.
//
// THE ENTRY AND ITS CONFIG ARE ONE RECORD, not two. A projectile authored in the Sprites tab is
// already a `kind:'projectile'` library record; this tab fills in that same record's `config`
// block, exactly as Enemy Creator fills in an enemy record's. That is what "selectable elsewhere
// by libraryId" means with no second storage system: the id an EnemyConfig's
// `tuning.projectile.libraryId` already points at is the id that now also carries the tuning.

import * as F from './enemy-form-fields.js';
import { createDefaultFxConfig, MOVEMENT_MODES } from './fx-defaults.js';
import { PROJECTILE_AIMING_FIELDS } from './enemy-defaults.js';
import { buildFxExportObject } from './fx-export.js';
import { loadValidator, validateConfig } from './config-schema.js';
import { downloadBlob } from './export-manifest.js';
import { listEntries, getEnemy, updateEnemy } from './enemy-library.js';
import { buildCombinedBundle } from './enemy-bundle.js';
import { KINDS } from './entry-kinds.js';

// Same rows, same order and same labels as enemy-form.js's PROJECTILE_SPEC_FIELDS, minus the two
// art keys (`prefab`, `libraryId`): for a standalone entry the prefab is what this config BUILDS
// and the art is the top-level picker, so offering them here would let a projectile point at some
// other projectile's prefab. `use` stays: it is the "inherit from a shared library entry" key and
// is as meaningful standalone as it is inline.
const PROJECTILE_SPEC_FIELDS = [
  ['use', 'text', 'Heredar de (use)', 'id de una entrada en una biblioteca de proyectiles JSON — vacío = todo propio'],
  ['speed', 'number', 'Velocidad', { min: 0.1 }],
  ['lifetime', 'number', 'Tiempo de vida (s)', { min: 0.05 }],
  ['size', 'vector2', 'Tamaño', {}],
  ['muzzleOffset', 'vector2', 'Desfase de salida (boca)', {}],
  ['pierce', 'number', 'Perforación (objetivos atravesados)', { min: 0, step: 1 }],
  ['homingTurnRate', 'number', 'Giro autoguiado (°/s)', { min: 0 }],
  ['homingRange', 'number', 'Rango autoguiado', { min: 0 }],
  ['arcGravity', 'number', 'Gravedad de arco (parábola)', { min: 0 }],
  ['impactRadius', 'number', 'Radio de explosión al impactar', { min: 0 }],
  ['impactDamage', 'number', 'Daño de esa explosión', { min: 0 }],
  ...PROJECTILE_AIMING_FIELDS,
];

export function initFxCreator({ formRoot, previewEl, summaryEl, exportBtn, exportCombinedBtn }) {
  const state = createDefaultFxConfig();
  const fieldErrorSetters = {}; // instancePath -> setError()
  let validate = null;

  loadValidator('fx')
    .then((v) => { validate = v; refresh(); })
    .catch((err) => {
      summaryEl.innerHTML = `<p class="efSummaryError">No se pudo cargar el validador de esquema: ${err.message}</p>`;
    });

  function registerError(path, setter) { fieldErrorSetters[path] = setter; }

  // ============================================================ qué es (projectile | fx)

  const topSection = document.createElement('div');
  topSection.className = 'efSection';
  topSection.innerHTML = '<h2>Proyectil / VFX</h2>';

  const kindField = F.enumField({
    label: 'Tipo',
    value: state.kind,
    options: [
      { value: 'projectile', label: `${KINDS.projectile.icon} Proyectil — vuela y golpea` },
      { value: 'fx', label: `${KINDS.fx.icon} VFX — sólo se ve` },
    ],
    hint: 'Un VFX no lleva ProjectileSpec: nada lo dispara, así que velocidad/perforación/autoguiado '
        + 'no existen para él (lo dice el propio esquema, no sólo esta pantalla).',
    onChange: (v) => { state.kind = v; applyKind(); refresh(); },
  });
  registerError('/kind', kindField.setError);
  topSection.appendChild(kindField.row);

  const nameField = F.textField({
    label: 'Nombre (name)', value: state.name,
    placeholder: 'ej. FireOrb',
    hint: 'Nombra el prefab: Fx_<nombre>.prefab. Sólo letras/números/_, empezando por letra.',
    onChange: (v) => { state.name = v; refresh(); },
  });
  registerError('/name', nameField.setError);
  topSection.appendChild(nameField.row);

  // --- art: an already-saved sheet of the SAME kind, picked from the shared library. This tab
  // never authors sprites: the Sprites tab owns that pipeline for all three sheet kinds, and
  // duplicating it here would mean two places to fix a detection bug.
  const artRow = document.createElement('div');
  artRow.className = 'efRow';
  const artLabel = document.createElement('label');
  artLabel.textContent = 'Arte desde biblioteca (libraryId)';
  artRow.appendChild(artLabel);

  const artSelect = document.createElement('select');
  artRow.appendChild(artSelect);

  const artHint = document.createElement('p');
  artHint.className = 'efHint';
  artRow.appendChild(artHint);
  topSection.appendChild(artRow);

  let artEntriesCache = [];

  async function refreshArtOptions() {
    const meta = KINDS[state.kind];
    artHint.textContent = `Hojas guardadas en la pestaña Sprites con el tipo puesto a "${meta.label}". `
      + 'La configuración se guarda sobre esa misma entrada, así que el id que ya usan los enemigos '
      + 'para su proyectil es el que ahora también lleva estos números.';

    artEntriesCache = await listEntries(state.kind);
    if (artEntriesCache.length === 0) {
      artSelect.innerHTML = `<option value="">(no hay ${meta.plural.toLowerCase()} guardados — créalos en la pestaña Sprites)</option>`;
      return;
    }
    artSelect.innerHTML = [`<option value="">Selecciona un ${meta.label.toLowerCase()} guardado…</option>`]
      .concat(artEntriesCache.map((e) =>
        `<option value="${e.id}" ${e.id === state.libraryId ? 'selected' : ''}>${e.enemyName} — ${new Date(e.updatedAt).toLocaleString()}</option>`
      )).join('');
    artSelect.value = state.libraryId || '';
  }

  artSelect.addEventListener('change', async () => {
    state.libraryId = artSelect.value || '';
    const picked = artEntriesCache.find((e) => e.id === state.libraryId);
    if (picked) {
      if (!state.name.trim()) {
        state.name = picked.enemyName;
        nameField.input.value = picked.enemyName;
      }
      // Reopening an entry that already has an FxConfig restores it instead of silently showing a
      // fresh default over the top of saved values — the same "pick up where I left off" promise
      // the Sprites tab's own saved-entries panel makes.
      if (picked.config && picked.config.kind === state.kind) adoptConfig(picked.config);
    }
    updateCombinedExportAvailability();
    refresh();
  });

  formRoot.appendChild(topSection);

  // ============================================================ movimiento

  const movementSection = document.createElement('div');
  movementSection.className = 'efSection';
  movementSection.innerHTML = '<h2>Movimiento <span class="efRangedBadge">sólo proyectiles</span></h2>';

  const movementHint = document.createElement('p');
  movementHint.className = 'efHint';
  movementSection.appendChild(movementHint);

  const movementField = F.enumField({
    label: 'Comportamiento (movement)',
    value: state.movement,
    options: MOVEMENT_MODES.map(({ value, label }) => ({ value, label })),
    hint: 'Hoy SÓLO "Straight" tiene lógica propia (ProjectileMovementPlaceholder). Los demás se '
        + 'guardan en el config y se estampan en el prefab, pero vuelan recto hasta que exista su '
        + 'script — y entonces el cambio será sustituir un componente, no reexportar nada.',
    onChange: (v) => { state.movement = v; applyMovementHint(); refresh(); },
  });
  registerError('/movement', movementField.setError);
  movementSection.appendChild(movementField.row);

  function applyMovementHint() {
    const mode = MOVEMENT_MODES.find((m) => m.value === state.movement);
    movementHint.textContent = mode ? mode.hint : '';
  }
  applyMovementHint();

  formRoot.appendChild(movementSection);

  // ============================================================ tuning (ProjectileSpec)

  const tuningSection = document.createElement('div');
  tuningSection.className = 'efSection';
  tuningSection.innerHTML = '<h2>Tuning del proyectil (ProjectileSpec) <span class="efRangedBadge">sólo proyectiles</span></h2>';

  const tuningNote = document.createElement('p');
  tuningNote.className = 'efHint';
  tuningNote.style.marginBottom = '10px';
  // Says out loud what the schema's $comment says, because this is the exact misunderstanding the
  // enemy-projectile-tuning-lives-on-enemystats convention exists to prevent.
  tuningNote.innerHTML = 'Estos valores son el <strong>punto de partida</strong> de la entrada: lo que '
    + 'se copia al elegir este proyectil en un enemigo/jefe, y lo que usa el placeholder para volar '
    + 'solo en Play. Lo que vuela de verdad en partida sale de <code>EnemyStats ▸ Tuning ▸ '
    + 'projectile</code> (o del ProjectileSpec del ataque del jefe): <code>ProjectileFactory.Spawn</code> '
    + 'reescribe el prefab en cada disparo.<br><br>'
    + 'No hay <code>damage</code> ni <code>count</code> aquí a propósito: el daño lo pone quien dispara '
    + '(<code>tuning.attackDamage</code>) y cuántos salen es la forma del <em>ataque</em>, no una '
    + 'propiedad del proyectil — ver la nota "SCOPE CORRECTION" de <code>projectile-config.schema.json</code>.';
  tuningSection.appendChild(tuningNote);

  const specInputs = {}; // key -> the built widget, so adoptConfig() can push values back onto the DOM

  PROJECTILE_SPEC_FIELDS.forEach(([key, kind, label, optsOrHint]) => {
    const isHintString = typeof optsOrHint === 'string';
    const opts = isHintString ? {} : optsOrHint;
    const hint = isHintString ? optsOrHint : opts.hint;
    const onChange = (v) => { state.projectile[key] = v; refresh(); };

    let built;
    if (kind === 'number') built = F.numberField({ label, value: state.projectile[key], min: opts.min, step: opts.step, hint, onChange });
    else if (kind === 'text') built = F.textField({ label, value: state.projectile[key], hint, onChange });
    else if (kind === 'vector2') built = F.vector2Field({ label, value: state.projectile[key], hint, onChange });
    else if (kind === 'bool') built = F.boolField({ label, value: state.projectile[key], hint, onChange });
    else if (kind === 'enum') built = F.enumField({ label, value: state.projectile[key], options: opts.options, hint, onChange });

    registerError(`/projectile/${key}`, built.setError);
    specInputs[key] = built;
    tuningSection.appendChild(built.row);
  });

  formRoot.appendChild(tuningSection);

  /** Shows/hides the projectile-only half and re-lists the art picker for the new kind. */
  function applyKind() {
    const isProjectile = state.kind === 'projectile';
    movementSection.hidden = !isProjectile;
    tuningSection.hidden = !isProjectile;
    // Switching kind invalidates the picked entry: a VFX sheet is not a valid art for a projectile
    // and vice versa, and keeping the id would export a config pointing at the wrong kind.
    if (state.libraryId && !artEntriesCache.some((e) => e.id === state.libraryId && e.kind === state.kind)) {
      state.libraryId = '';
    }
    refreshArtOptions();
    updateCombinedExportAvailability();
  }

  /** Replaces the whole editable state from a saved FxConfig, and repaints every widget. */
  function adoptConfig(config) {
    state.movement = config.movement || 'Straight';
    if (movementField.input) movementField.input.value = state.movement;
    applyMovementHint();

    if (config.name && !state.name.trim()) {
      state.name = config.name;
      nameField.input.value = config.name;
    }

    if (config.projectile) {
      Object.entries(config.projectile).forEach(([key, value]) => {
        if (!(key in state.projectile)) return;
        state.projectile[key] = value;

        // The widgets are built once and live for the session, so restoring a saved config has to
        // push values back onto the DOM by hand. vector2Field exposes two boxes rather than one
        // `input` (see enemy-form-fields.js), which is the only shape that needs its own branch.
        const widget = specInputs[key];
        if (!widget) return;
        if (widget.xInput && value && typeof value === 'object') {
          widget.xInput.value = value.x;
          widget.yInput.value = value.y;
        } else if (widget.input && widget.input.type === 'checkbox') {
          widget.input.checked = !!value;
        } else if (widget.input) {
          widget.input.value = value;
        }
      });
    }
  }

  // ============================================================ live preview + validation

  function clearAllErrors() {
    Object.values(fieldErrorSetters).forEach((setter) => setter(null));
  }

  function refresh() {
    const exportObj = buildFxExportObject(state);
    previewEl.textContent = JSON.stringify(exportObj, null, 2);

    if (!validate) return; // still loading the schema
    clearAllErrors();

    const { valid, errors } = validateConfig(validate, exportObj);
    exportBtn.disabled = !valid;

    if (valid) {
      const artWarning = state.libraryId
        ? ''
        : '<p class="efHint">Sin arte elegido: el config es válido, pero Unity no podrá construir '
          + 'ningún prefab hasta que apunte a una entrada de la biblioteca.</p>';
      summaryEl.innerHTML = `<p class="efSummaryOk">✓ Válido según fx-config.schema.json</p>${artWarning}`;
      return;
    }

    errors.forEach((e) => { if (fieldErrorSetters[e.path]) fieldErrorSetters[e.path](e.message); });
    summaryEl.innerHTML =
      `<p class="efSummaryError">✗ ${errors.length} error${errors.length > 1 ? 'es' : ''} de validación:</p>` +
      `<ul>${errors.map((e) => `<li><code>${e.path || '(raíz)'}</code> — ${e.message}</li>`).join('')}</ul>`;
  }

  /** Same rule as the Enemy Creator: every export also writes the config onto the linked record,
   *  which is what makes the entry show up in Biblioteca with a "config" badge and a working
   *  "Exportar combinado" without the form being refilled. Never fatal — the download already
   *  happened by the time this runs. */
  async function persistConfigToLibrary(exportObj) {
    if (!state.libraryId) return;
    try { await updateEnemy(state.libraryId, { config: exportObj }); }
    catch (err) { console.warn('[fx-form] no se pudo guardar el config en la biblioteca:', err); }
  }

  exportBtn.addEventListener('click', async () => {
    const exportObj = buildFxExportObject(state);
    const name = (state.name.trim() || KINDS[state.kind].fallbackName).replace(/[^A-Za-z0-9_]/g, '_');
    const blob = new Blob([JSON.stringify(exportObj, null, 2)], { type: 'application/json' });
    downloadBlob(blob, `${name}.${state.kind}.json`);
    await persistConfigToLibrary(exportObj);
  });

  // ============================================================ combined bundle (sprites + config)

  function updateCombinedExportAvailability() {
    if (!exportCombinedBtn) return;
    const available = !!state.libraryId;
    exportCombinedBtn.disabled = !available;
    exportCombinedBtn.title = available ? '' : 'Elige primero el arte desde la biblioteca.';
  }

  if (exportCombinedBtn) {
    exportCombinedBtn.addEventListener('click', async () => {
      if (!state.libraryId) return;

      const entry = await getEnemy(state.libraryId);
      if (!entry) {
        summaryEl.innerHTML = '<p class="efSummaryError">Esa entrada ya no existe en la biblioteca — vuelve a seleccionarla.</p>';
        await refreshArtOptions();
        return;
      }

      const exportObj = buildFxExportObject(state);
      const name = exportObj.name || entry.enemyName;
      const blob = await buildCombinedBundle({
        enemyName: name, kind: entry.kind, libraryId: entry.id, sprite: entry.sprite, configObj: exportObj,
      });
      downloadBlob(blob, `${name.replace(/[^A-Za-z0-9_]/g, '_')}.bundle.zip`);
      await persistConfigToLibrary(exportObj);
    });
  }

  applyKind();
  refresh();

  /**
   * Links this draft to a library entry from outside (the Biblioteca tab's "Editar en Proyectil/FX
   * Creator" action): adopts that entry's kind, selects it, and restores its saved config if it
   * already has one.
   */
  async function linkLibraryEntry(id, entryName, kind) {
    if (kind && kind !== state.kind) {
      state.kind = kind;
      if (kindField.input) kindField.input.value = kind;
    }
    state.libraryId = id;
    applyKind();
    await refreshArtOptions();
    artSelect.value = id;

    const entry = await getEnemy(id);
    if (entry) {
      if (!state.name.trim()) {
        state.name = entry.enemyName || entryName || '';
        nameField.input.value = state.name;
      }
      if (entry.config && entry.config.kind === state.kind) adoptConfig(entry.config);
    }

    updateCombinedExportAvailability();
    refresh();
  }

  return { refreshArtLibrary: refreshArtOptions, linkLibraryEntry };
}
