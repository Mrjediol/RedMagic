// modules/enemy-form.js
// -----------------------------------------------------------------------------
// The Enemy Creator tab itself: builds the full EnemyConfig form out of the generic widgets in
// enemy-form-fields.js, owns the live editable state (enemy-defaults.js's shape), and keeps the
// JSON preview + Ajv validation (enemy-config-schema.js) in sync on every change.
//
// UI sections mirror the schema's own grouping exactly: presence / projectileArt / tuning (the
// tuning block additionally gets plain visual sub-headers matching EnemyTuning.cs's own
// [Header(...)] order — Tipo, Vida, Retroceso, Rangos, Movimiento, Dormido, Vuelo, Ataque,
// Ataque·melé, Ataque·kamikaze, Ataque·a distancia, Contacto, Animación, Objetivo — this is
// cosmetic labeling only, the exported JSON stays exactly as flat as the schema itself.

import * as F from './enemy-form-fields.js';
import { createDefaultEnemyConfig, createDefaultProjectileSpec, PROJECTILE_AIMING_FIELDS } from './enemy-defaults.js';
import { buildExportObject } from './enemy-export.js';
import { loadEnemyConfigValidator, validateEnemyConfig } from './enemy-config-schema.js';
import { downloadBlob } from './export-manifest.js';
import { listEntries, getEnemy, updateEnemy } from './enemy-library.js';
import { buildCombinedBundle } from './enemy-bundle.js';

const KNOWN_LAYERS = ['Ground', 'Platform'];

// EnemyTuning's raw C# defaults (Melee-shaped: attackRange 1.6, personalSpace 0) don't make sense
// once the enemy is actually ranged — a "ranged" enemy that never backs off and only fires from
// melee distance is a real gameplay bug someone will hit at 100% frequency, not an edge case.
// These only kick in the moment archetype/staticAttack makes the enemy ranged, and only while the
// field is still sitting at its untouched generic default — a value the user typed on purpose,
// ranged or not, is never overwritten.
const GENERIC_ATTACK_RANGE = 1.6;
const GENERIC_PERSONAL_SPACE = 0;
const GENERIC_MUZZLE_OFFSET = { x: 0.65, y: 0.1 };
const RANGED_ATTACK_RANGE = 6;
const RANGED_PERSONAL_SPACE = 3;
// ProjectileSpec.muzzleOffset's own default (y=0.1) sits barely above the feet — fine for
// whatever the class's original reference use was, but for a BottomCenter-pivoted enemy sprite
// it visibly spawns the shot at ground level. Ogro's own hand-tuned real value (Assets/Art/
// Characters/Ogro/Ogro.enemy.asset) is y=1; used here as a far more broadly reasonable default
// than y=0.1 for "roughly hand height on a biped enemy".
const RANGED_MUZZLE_OFFSET = { x: 0.7, y: 1 };

const TUNING_FIELDS = [
  // group, key, kind, label, opts
  ['Vida', 'maxHealth', 'number', 'Vida máxima', { min: 1 }],
  ['Vida', 'invulnerabilityDuration', 'number', 'Duración invulnerabilidad (i-frames)', { min: 0 }],
  ['Vida', 'hurtSfxId', 'text', 'SFX al recibir daño', {}],
  ['Vida', 'deathSfxId', 'text', 'SFX al morir', {}],

  ['Retroceso', 'knockbackHorizontal', 'number', 'Retroceso horizontal', { min: 0 }],
  ['Retroceso', 'knockbackVertical', 'number', 'Retroceso vertical', { min: 0 }],
  ['Retroceso', 'knockbackDuration', 'number', 'Duración retroceso (s)', { min: 0 }],
  ['Retroceso', 'knockbackResistance', 'number', 'Resistencia a retroceso (0-1)', { min: 0, max: 1 }],

  ['Rangos', 'detectionRange', 'number', 'Rango de detección', { min: 0 }],
  ['Rangos', 'loseInterestGrace', 'number', 'Gracia antes de perder interés (s)', { min: 0 }],
  ['Rangos', 'attackRange', 'number', 'Rango de ataque', { min: 0 }],
  ['Rangos', 'personalSpace', 'number', 'Espacio personal (retirada)', { min: 0 }],
  ['Rangos', 'retreatReleaseFactor', 'number', 'Factor de liberación de retirada', { min: 1, max: 2 }],
  ['Rangos', 'verticalTolerance', 'number', 'Tolerancia vertical', { min: 0 }],

  ['Movimiento', 'moveSpeed', 'number', 'Velocidad de movimiento', { min: 0 }],
  ['Movimiento', 'retreatSpeed', 'number', 'Velocidad de retirada', { min: 0 }],
  ['Movimiento', 'stopAtLedges', 'bool', 'Se detiene en bordes', {}],
  ['Movimiento', 'ledgeProbeDepth', 'number', 'Profundidad sondeo de borde', { min: 0 }],
  ['Movimiento', 'gravityScale', 'number', 'Escala de gravedad', { min: 0 }],

  ['Dormido', 'sleepsUntilDetected', 'bool', 'Duerme hasta ser detectado', {}],

  ['Vuelo', 'hoverOffset', 'number', 'Desfase de vuelo (hover, puede ser negativo)', {}],

  ['Ataque', 'attackDamage', 'number', 'Daño de ataque', { min: 0 }],
  ['Ataque', 'attackCooldown', 'number', 'Enfriamiento de ataque (s)', { min: 0 }],
  ['Ataque', 'attackKnockbackMultiplier', 'number', 'Multiplicador de retroceso', { min: 0 }],
  ['Ataque', 'attackReleaseFallback', 'number', 'Fallback de liberación (s)', { min: 0 }],
  ['Ataque', 'rootedWhileAttacking', 'bool', 'Enraizado mientras ataca', {}],

  ['Ataque · melé', 'meleeHitboxSize', 'vector2', 'Tamaño de la caja de golpe', {}],
  ['Ataque · melé', 'meleeHitboxOffset', 'vector2', 'Desfase de la caja de golpe', {}],

  ['Ataque · kamikaze', 'selfDestruct', 'bool', 'Kamikaze (el ataque es explotar)', {}],
  ['Ataque · kamikaze', 'explosionRadius', 'number', 'Radio de explosión', { min: 0 }],
  ['Ataque · kamikaze', 'explosionShake', 'number', 'Sacudida de cámara al explotar', { min: 0 }],

  ['Ataque · a distancia', 'aimAtTarget', 'bool', 'Apunta al objetivo', { rangedOnly: true }],
  ['Ataque · a distancia', 'projectileSprite', 'assetRef', 'Sprite del proyectil (sólo si el proyectil no tiene prefab)', { rangedOnly: true }],
  ['Ataque · a distancia', 'projectileTint', 'color', 'Tinte del proyectil', { rangedOnly: true }],

  ['Contacto', 'contactDamage', 'number', 'Daño por contacto', { min: 0 }],
  ['Contacto', 'contactDamageCooldown', 'number', 'Enfriamiento daño por contacto (s)', { min: 0 }],
  ['Contacto', 'contactKnockbackMultiplier', 'number', 'Multiplicador retroceso por contacto', { min: 0 }],

  ['Animación', 'idleAnimSpeed', 'number', 'Velocidad anim. Idle', { min: 0.01 }],
  ['Animación', 'moveAnimSpeed', 'number', 'Velocidad anim. Move', { min: 0.01 }],
  ['Animación', 'attackAnimSpeed', 'number', 'Velocidad anim. Attack', { min: 0.01 }],
  ['Animación', 'hurtAnimSpeed', 'number', 'Velocidad anim. Hurt', { min: 0.01 }],
  ['Animación', 'deathAnimSpeed', 'number', 'Velocidad anim. Death', { min: 0.01 }],
  ['Animación', 'wakeAnimSpeed', 'number', 'Velocidad anim. Wake', { min: 0.01 }],

  ['Objetivo', 'targetTag', 'text', 'Etiqueta del objetivo', {}],
];

const PROJECTILE_SPEC_FIELDS = [
  ['use', 'text', 'Referencia a biblioteca (use)', 'id de una entrada en una biblioteca de proyectiles — vacío = todo inline'],
  ['prefab', 'assetRef', 'Prefab', 'vacío = proyectil construido en código'],
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

export function initEnemyCreator({ formRoot, laneInfoEl, previewEl, summaryEl, exportBtn, exportCombinedBtn, getLaneNames }) {
  const state = createDefaultEnemyConfig();
  const fieldErrorSetters = {}; // instancePath ('/tuning/archetype', etc.) -> setError()
  let validate = null;

  loadEnemyConfigValidator()
    .then((v) => { validate = v; refresh(); })
    .catch((err) => {
      summaryEl.innerHTML = `<p class="efSummaryError">No se pudo cargar el validador de esquema: ${err.message}</p>`;
    });

  // ============================================================ top-level fields

  const topSection = document.createElement('div');
  topSection.className = 'efSection';
  topSection.innerHTML = '<h2>Enemigo</h2>';

  const nameField = F.textField({
    label: 'Nombre del enemigo (enemyName)', value: state.enemyName,
    placeholder: 'ej. MushroomWarrior',
    hint: 'Nombra el prefab: Enemy_<nombre>.prefab. Sólo letras/números/_, empezando por letra.',
    onChange: (v) => { state.enemyName = v; refresh(); },
  });
  registerError('/enemyName', nameField.setError);
  topSection.appendChild(nameField.row);

  // --- art: pick a sheet authored in this tool's shared library, or fall back to a manual path
  // into a sheet already imported into Unity in a past session (see enemy-defaults.js's comment).
  const artModeSwitch = document.createElement('div');
  artModeSwitch.className = 'modeSwitch';
  const artBtnLibrary = document.createElement('button'); artBtnLibrary.textContent = 'Desde biblioteca';
  const artBtnManual = document.createElement('button'); artBtnManual.textContent = 'Escribir ruta manualmente';
  artModeSwitch.appendChild(artBtnLibrary);
  artModeSwitch.appendChild(artBtnManual);
  const artModeRow = document.createElement('div');
  artModeRow.className = 'efRow';
  const artModeLabel = document.createElement('label');
  artModeLabel.textContent = 'Hoja de sprites (art)';
  artModeRow.appendChild(artModeLabel);
  artModeRow.appendChild(artModeSwitch);
  topSection.appendChild(artModeRow);

  const artLibrarySelect = document.createElement('select');
  const artLibraryRow = document.createElement('div');
  artLibraryRow.className = 'efRow';
  artLibraryRow.appendChild(artLibrarySelect);
  const artLibraryHint = document.createElement('p');
  artLibraryHint.className = 'efHint';
  artLibraryHint.textContent = 'Enemigos guardados en la pestaña Sprites ("💾 Guardar como enemigo") o en Biblioteca.';
  artLibraryRow.appendChild(artLibraryHint);
  topSection.appendChild(artLibraryRow);

  const manualArtField = F.assetRefField({
    label: 'Ruta manual (art)', value: state.art,
    hint: 'Ruta al asset SpriteSheetRecipe (<Nombre>.sheet.asset) ya generado por el pipeline de sprites en una sesión anterior.',
    onChange: (v) => { state.art = v; refresh(); },
  });
  registerError('/art', manualArtField.setError);
  topSection.appendChild(manualArtField.row);

  let libraryEntriesCache = [];

  // Enemy-kind entries only. A projectile or VFX entry is a perfectly valid library record but
  // not a valid 'art' for an EnemyConfig: EnemyFactory reads that sheet for the enemy's own body
  // clips and its thrown prop, neither of which a projectile/VFX sheet has. Offering them would
  // produce a config that imports into a broken enemy instead of failing at authoring time.
  async function refreshArtLibraryOptions() {
    libraryEntriesCache = await listEntries('enemy');
    if (libraryEntriesCache.length === 0) {
      artLibrarySelect.innerHTML = '<option value="">(sin enemigos guardados — usa la pestaña Sprites)</option>';
      return;
    }
    artLibrarySelect.innerHTML = ['<option value="">Selecciona un enemigo guardado…</option>']
      .concat(libraryEntriesCache.map((e) =>
        `<option value="${e.id}" ${e.id === state.artLibraryId ? 'selected' : ''}>${e.enemyName} — ${new Date(e.updatedAt).toLocaleString()}</option>`
      )).join('');
  }

  artLibrarySelect.addEventListener('change', () => {
    state.artLibraryId = artLibrarySelect.value || null;
    const picked = libraryEntriesCache.find((e) => e.id === state.artLibraryId);
    if (picked && !state.enemyName.trim()) {
      state.enemyName = picked.enemyName;
      nameField.input.value = picked.enemyName;
    }
    updateCombinedExportAvailability();
    refresh();
  });

  function setArtMode(mode) {
    state.artMode = mode;
    artBtnLibrary.classList.toggle('active', mode === 'library');
    artBtnManual.classList.toggle('active', mode === 'manual');
    artLibraryRow.hidden = mode !== 'library';
    manualArtField.row.hidden = mode !== 'manual';
    if (mode === 'library') refreshArtLibraryOptions();
    updateCombinedExportAvailability();
    refresh();
  }
  artBtnLibrary.addEventListener('click', () => setArtMode('library'));
  artBtnManual.addEventListener('click', () => setArtMode('manual'));

  // Sprite pivot — only takes effect on a combined-bundle import (the one path that actually cuts
  // a fresh SpriteSheetRecipe); a manual/already-imported 'art' path ignores it. See the schema's
  // $comment on 'anchor' and RedMagic.Pipeline.AnchorMode.
  const anchorField = F.enumField({
    label: 'Pivote del sprite (anchor)',
    value: state.anchor,
    options: [
      { value: 'Center', label: 'Centro — vuela, flota, sin pies' },
      { value: 'BottomCenter', label: 'Pies (BottomCenter) — camina por el suelo' },
    ],
    hint: 'Sólo se aplica al importar el .zip combinado (sprites + config) — un enemigo cuya art ya apunta a una hoja existente ignora este campo.',
    onChange: (v) => { state.anchor = v; refresh(); },
  });
  registerError('/anchor', anchorField.setError);
  topSection.appendChild(anchorField.row);

  const prefabFolderField = F.textField({
    label: 'Carpeta del prefab (prefabFolder)', value: state.prefabFolder,
    hint: 'Vacío = Assets/Prefabs/Enemies.',
    onChange: (v) => { state.prefabFolder = v; refresh(); },
  });
  registerError('/prefabFolder', prefabFolderField.setError);
  topSection.appendChild(prefabFolderField.row);

  const tierField = F.enumField({
    label: 'Tier de economía (tier)', value: state.tier,
    options: [{ value: 'Basic', label: 'Basic' }, { value: 'Elite', label: 'Elite' }, { value: 'Boss', label: 'Boss' }],
    hint: 'Tabla de drop de moneda por tier (CurrencyConfig).',
    onChange: (v) => { state.tier = v; refresh(); },
  });
  registerError('/tier', tierField.setError);
  topSection.appendChild(tierField.row);

  formRoot.appendChild(topSection);

  // ============================================================ presence

  const presenceSection = document.createElement('div');
  presenceSection.className = 'efSection';
  presenceSection.innerHTML = '<h2>Presencia</h2>';

  presenceSection.appendChild(F.numberField({
    label: 'Escala del sprite', value: state.presence.spriteScale, min: 0.01,
    onChange: (v) => { state.presence.spriteScale = v; refresh(); },
  }).row);
  presenceSection.appendChild(F.vector2Field({
    label: 'Tamaño del collider (0,0 = automático)', value: state.presence.colliderSize,
    onChange: (v) => { state.presence.colliderSize = v; refresh(); },
  }).row);
  presenceSection.appendChild(F.vector2Field({
    label: 'Desfase del collider (0,0 = automático)', value: state.presence.colliderOffset,
    onChange: (v) => { state.presence.colliderOffset = v; refresh(); },
  }).row);
  presenceSection.appendChild(F.numberField({
    label: 'Orden de dibujado (sortingOrder)', value: state.presence.sortingOrder, step: 1,
    onChange: (v) => { state.presence.sortingOrder = Math.round(v); refresh(); },
  }).row);
  presenceSection.appendChild(F.textField({
    label: 'Etiqueta (tag)', value: state.presence.tag,
    hint: '"Untagged" normal; "Enemy" para esbirros de un jefe.',
    onChange: (v) => { state.presence.tag = v; refresh(); },
  }).row);

  formRoot.appendChild(presenceSection);

  // ============================================================ projectileArt (de-emphasized unless ranged)

  const projectileArtSection = document.createElement('div');
  projectileArtSection.className = 'efSection';
  projectileArtSection.innerHTML = '<h2>Arte del proyectil <span class="efRangedBadge">sólo arquetipos a distancia</span></h2>';

  projectileArtSection.appendChild(F.textField({
    label: 'Fila con el prop suelto (propState)', value: state.projectileArt.propState,
    hint: 'Fila de la lámina cuyo objeto suelto se convierte en el proyectil.',
    onChange: (v) => { state.projectileArt.propState = v; refresh(); },
  }).row);
  projectileArtSection.appendChild(F.numberField({
    label: 'Escala del sprite del proyectil', value: state.projectileArt.scale, min: 0.01,
    onChange: (v) => { state.projectileArt.scale = v; refresh(); },
  }).row);

  formRoot.appendChild(projectileArtSection);

  // ============================================================ tuning

  const tuningSection = document.createElement('div');
  tuningSection.className = 'efSection';
  tuningSection.innerHTML = '<h2>Tuning (EnemyStats)</h2>';
  formRoot.appendChild(tuningSection);

  // --- Tipo (archetype/staticAttack) ---
  const tipoGroup = addGroup(tuningSection, 'Tipo');
  const archetypeField = F.enumField({
    label: 'Arquetipo', value: state.tuning.archetype,
    options: ['Static', 'Melee', 'Ranged', 'FlyingMelee', 'FlyingRanged'].map((v) => ({ value: v, label: v })),
    hint: 'Decide cómo se mueve y cómo ataca.',
    onChange: (v) => { state.tuning.archetype = v; updateRangedEmphasis(); applyRangedDefaultsIfUntouched(); refresh(); },
  });
  registerError('/tuning/archetype', archetypeField.setError);
  tipoGroup.appendChild(archetypeField.row);

  const staticAttackField = F.enumField({
    label: 'Ataque de Static (staticAttack)', value: state.tuning.staticAttack,
    options: [{ value: 'Melee', label: 'Melee' }, { value: 'Ranged', label: 'Ranged' }],
    hint: 'Sólo se usa si el arquetipo es Static.',
    onChange: (v) => { state.tuning.staticAttack = v; updateRangedEmphasis(); applyRangedDefaultsIfUntouched(); refresh(); },
  });
  registerError('/tuning/staticAttack', staticAttackField.setError);
  tipoGroup.appendChild(staticAttackField.row);

  // --- generic flat fields, driven by TUNING_FIELDS ---
  const groupEls = { Tipo: tipoGroup };
  const rangedRows = []; // rows to de-emphasize when the enemy isn't ranged
  const fieldInputs = {}; // key -> <input>, so applyRangedDefaultsIfUntouched() can update what's on screen

  TUNING_FIELDS.forEach(([groupName, key, kind, label, opts]) => {
    const group = groupEls[groupName] || (groupEls[groupName] = addGroup(tuningSection, groupName));
    let built;

    const onChange = (v) => { state.tuning[key] = v; refresh(); };

    if (kind === 'number') built = F.numberField({ label, value: state.tuning[key], min: opts.min, max: opts.max, step: opts.step, onChange });
    else if (kind === 'bool') built = F.boolField({ label, value: state.tuning[key], onChange });
    else if (kind === 'text') built = F.textField({ label, value: state.tuning[key], onChange });
    else if (kind === 'assetRef') built = F.assetRefField({ label, value: state.tuning[key], onChange });
    else if (kind === 'vector2') built = F.vector2Field({ label, value: state.tuning[key], onChange });
    else if (kind === 'color') built = F.colorField({ label, value: state.tuning[key], onChange });

    registerError(`/tuning/${key}`, built.setError);
    group.appendChild(built.row);
    if (opts.rangedOnly) rangedRows.push(built.row);
    if (built.input) fieldInputs[key] = built.input;
  });

  /**
   * Bumps attackRange/personalSpace/muzzleOffset to sensible ranged values the moment the enemy
   * becomes ranged (see the constants at the top of this file) — but only fields still sitting at
   * their untouched generic default, so a value the user already typed is never clobbered.
   */
  function applyRangedDefaultsIfUntouched() {
    if (!isRangedNow()) return;

    if (state.tuning.attackRange === GENERIC_ATTACK_RANGE) {
      state.tuning.attackRange = RANGED_ATTACK_RANGE;
      if (fieldInputs.attackRange) fieldInputs.attackRange.value = RANGED_ATTACK_RANGE;
    }
    if (state.tuning.personalSpace === GENERIC_PERSONAL_SPACE) {
      state.tuning.personalSpace = RANGED_PERSONAL_SPACE;
      if (fieldInputs.personalSpace) fieldInputs.personalSpace.value = RANGED_PERSONAL_SPACE;
    }

    const muzzle = state.tuning.projectileSpec.muzzleOffset;
    if (state.tuning.projectileMode === 'inline' &&
        muzzle.x === GENERIC_MUZZLE_OFFSET.x && muzzle.y === GENERIC_MUZZLE_OFFSET.y) {
      state.tuning.projectileSpec.muzzleOffset = { ...RANGED_MUZZLE_OFFSET };
      renderProjectileInline(); // small subform, cheap to rebuild wholesale
    }
  }

  // --- Ataque · a distancia: the projectile toggle + inline sub-form ---
  const projGroup = groupEls['Ataque · a distancia'];
  rangedRows.push(projGroup);

  const projToggleRow = document.createElement('div');
  projToggleRow.className = 'efRow';
  const projLabel = document.createElement('label');
  projLabel.textContent = 'Proyectil (tuning.projectile)';
  projToggleRow.appendChild(projLabel);
  const modeSwitch = document.createElement('div');
  modeSwitch.className = 'modeSwitch';
  const btnDefault = document.createElement('button'); btnDefault.textContent = 'Usar por defecto';
  const btnInline = document.createElement('button'); btnInline.textContent = 'Configurar inline';
  modeSwitch.appendChild(btnDefault); modeSwitch.appendChild(btnInline);
  projToggleRow.appendChild(modeSwitch);
  // Insert the toggle at the TOP of the "Ataque · a distancia" group, before aimAtTarget/etc.
  projGroup.insertBefore(projToggleRow, projGroup.firstChild);

  const projInlineBox = document.createElement('div');
  projInlineBox.className = 'efProjectileInline';
  projGroup.insertBefore(projInlineBox, projToggleRow.nextSibling);

  /**
   * The projectile's ART, picked from the shared library instead of typed as a Unity path. This is
   * the authoring half of the lazy-resolution flow: choosing an entry here exports
   * `tuning.projectile.libraryId`, and the Unity importer builds (or reuses) that entry's pooled
   * prefab on the spot — so the projectile never has to be imported into Unity as a separate step
   * first. Leaving it on "(ninguno)" is the unchanged old behaviour: either a hand-typed `prefab`
   * path below, or nothing at all (a projectile built in code).
   */
  function renderProjectileLibraryRow() {
    const row = document.createElement('div');
    row.className = 'efRow';

    const label = document.createElement('label');
    label.textContent = 'Arte desde biblioteca';
    row.appendChild(label);

    const wrap = document.createElement('div');
    wrap.style.flex = '1';

    const select = document.createElement('select');
    select.innerHTML = '<option value="">(ninguno — usar el campo Prefab de abajo)</option>';
    wrap.appendChild(select);

    const hint = document.createElement('p');
    hint.className = 'efHint';
    hint.textContent = 'Un proyectil guardado en la pestaña Sprites (tipo Proyectil). Se construirá en '
      + 'Unity al importar este enemigo; no hace falta importarlo antes por separado.';
    wrap.appendChild(hint);

    row.appendChild(wrap);
    projInlineBox.appendChild(row);

    // Populated async so the sub-form renders immediately; the picked value is re-applied once the
    // options exist, since a <select> silently drops a value it has no <option> for.
    listEntries('projectile').then((entries) => {
      if (entries.length === 0) {
        select.innerHTML = '<option value="">(no hay proyectiles guardados — créalos en la pestaña Sprites)</option>';
        return;
      }
      select.innerHTML = '<option value="">(ninguno — usar el campo Prefab de abajo)</option>'
        + entries.map((e) => `<option value="${e.id}">${e.enemyName}</option>`).join('');
      select.value = state.tuning.projectileSpec.libraryId || '';
    });

    select.addEventListener('change', () => {
      state.tuning.projectileSpec.libraryId = select.value;
      refresh();
    });
  }

  function renderProjectileInline() {
    projInlineBox.innerHTML = '';
    projInlineBox.hidden = state.tuning.projectileMode !== 'inline';
    if (projInlineBox.hidden) return;

    renderProjectileLibraryRow();

    PROJECTILE_SPEC_FIELDS.forEach(([key, kind, label, optsOrHint]) => {
      const isHintString = typeof optsOrHint === 'string';
      const opts = isHintString ? {} : optsOrHint;
      const hint = isHintString ? optsOrHint : opts.hint;
      const onChange = (v) => { state.tuning.projectileSpec[key] = v; refresh(); };

      let built;
      if (kind === 'number') built = F.numberField({ label, value: state.tuning.projectileSpec[key], min: opts.min, step: opts.step, hint, onChange });
      else if (kind === 'text') built = F.textField({ label, value: state.tuning.projectileSpec[key], hint, onChange });
      else if (kind === 'assetRef') built = F.assetRefField({ label, value: state.tuning.projectileSpec[key], hint, onChange });
      else if (kind === 'vector2') built = F.vector2Field({ label, value: state.tuning.projectileSpec[key], hint, onChange });
      else if (kind === 'bool') built = F.boolField({ label, value: state.tuning.projectileSpec[key], hint, onChange });
      else if (kind === 'enum') built = F.enumField({ label, value: state.tuning.projectileSpec[key], options: opts.options, hint, onChange });

      registerError(`/tuning/projectile/${key}`, built.setError);
      projInlineBox.appendChild(built.row);
    });
  }

  function setProjectileMode(mode) {
    state.tuning.projectileMode = mode;
    if (mode === 'inline' && !state.tuning.projectileSpec) state.tuning.projectileSpec = createDefaultProjectileSpec();
    btnDefault.classList.toggle('active', mode === 'default');
    btnInline.classList.toggle('active', mode === 'inline');
    renderProjectileInline();
    // Catches the case where archetype was already ranged before switching to "configurar
    // inline" — the muzzleOffset bump in applyRangedDefaultsIfUntouched only applies once a
    // projectileSpec actually exists to bump.
    if (mode === 'inline') applyRangedDefaultsIfUntouched();
    refresh();
  }
  btnDefault.addEventListener('click', () => setProjectileMode('default'));
  btnInline.addEventListener('click', () => setProjectileMode('inline'));
  setProjectileMode(state.tuning.projectileMode);

  // --- obstacleLayers / hitLayers ---
  const objetivoGroup = groupEls['Objetivo'];
  objetivoGroup.appendChild(F.layerMaskField({
    label: 'Capas que puede dañar (hitLayers)', value: state.tuning.hitLayers, knownLayers: KNOWN_LAYERS,
    onChange: (v) => { state.tuning.hitLayers = v; refresh(); },
  }).row);
  objetivoGroup.appendChild(F.layerMaskField({
    label: 'Capas de terreno (obstacleLayers)', value: state.tuning.obstacleLayers, knownLayers: KNOWN_LAYERS,
    hint: 'Normalmente Ground + Platform — ver COMPATIBILITY.md sobre por qué faltar Platform rompe enemigos en puentes.',
    onChange: (v) => { state.tuning.obstacleLayers = v; refresh(); },
  }).row);

  function addGroup(parent, title) {
    const g = document.createElement('div');
    g.className = 'efGroup';
    const h = document.createElement('h3');
    h.textContent = title;
    g.appendChild(h);
    parent.appendChild(g);
    return g;
  }

  function registerError(path, setter) {
    fieldErrorSetters[path] = setter;
  }

  function isRangedNow() {
    const a = state.tuning.archetype;
    if (a === 'Ranged' || a === 'FlyingRanged') return true;
    if (a === 'Static') return state.tuning.staticAttack === 'Ranged';
    return false;
  }

  function updateRangedEmphasis() {
    const ranged = isRangedNow();
    rangedRows.forEach((el) => el.classList.toggle('efDeemphasized', !ranged));
    projectileArtSection.classList.toggle('efDeemphasized', !ranged);
  }
  updateRangedEmphasis();

  // ============================================================ live preview + validation

  function clearAllErrors() {
    Object.values(fieldErrorSetters).forEach((setter) => setter(null));
  }

  async function refresh() {
    const exportObj = buildExportObject(state);
    previewEl.textContent = JSON.stringify(exportObj, null, 2);

    if (!validate) return; // still loading the schema
    clearAllErrors();

    const { valid, errors } = validateEnemyConfig(validate, exportObj);
    exportBtn.disabled = !valid;

    if (valid) {
      summaryEl.innerHTML = '<p class="efSummaryOk">✓ Válido según enemy-config.schema.json</p>';
      return;
    }

    errors.forEach((e) => { if (fieldErrorSetters[e.path]) fieldErrorSetters[e.path](e.message); });
    summaryEl.innerHTML =
      `<p class="efSummaryError">✗ ${errors.length} error${errors.length > 1 ? 'es' : ''} de validación:</p>` +
      `<ul>${errors.map((e) => `<li><code>${e.path || '(raíz)'}</code> — ${e.message}</li>`).join('')}</ul>`;
  }

  /** Once a draft is linked to a library entry, every export also persists the config onto it —
   *  this is what lets the Biblioteca tab and a later "editar en Enemy Creator" session offer
   *  "exportar combinado" without the user re-filling the form. Never fatal: the download itself
   *  already happened by the time this runs. */
  async function persistConfigToLibraryIfLinked(exportObj) {
    if (state.artMode !== 'library' || !state.artLibraryId) return;
    try { await updateEnemy(state.artLibraryId, { config: exportObj }); }
    catch (err) { console.warn('[enemy-form] no se pudo guardar el config en la biblioteca:', err); }
  }

  exportBtn.addEventListener('click', async () => {
    const exportObj = buildExportObject(state);
    const name = (state.enemyName.trim() || 'Enemy').replace(/[^A-Za-z0-9_]/g, '_');
    const blob = new Blob([JSON.stringify(exportObj, null, 2)], { type: 'application/json' });
    downloadBlob(blob, `${name}.enemy.json`);
    await persistConfigToLibraryIfLinked(exportObj);
  });

  // ============================================================ combined bundle export (sprites + config)

  function updateCombinedExportAvailability() {
    if (!exportCombinedBtn) return;
    const available = state.artMode === 'library' && !!state.artLibraryId;
    exportCombinedBtn.disabled = !available;
    exportCombinedBtn.title = available
      ? ''
      : 'Sólo disponible con un enemigo de la biblioteca seleccionado como art (no con ruta manual).';
  }

  if (exportCombinedBtn) {
    exportCombinedBtn.addEventListener('click', async () => {
      if (state.artMode !== 'library' || !state.artLibraryId) return;

      const entry = await getEnemy(state.artLibraryId);
      if (!entry) {
        summaryEl.innerHTML = '<p class="efSummaryError">El enemigo de la biblioteca ya no existe — vuelve a seleccionarlo.</p>';
        await refreshArtLibraryOptions();
        return;
      }

      const exportObj = buildExportObject(state);
      const enemyName = exportObj.enemyName || entry.enemyName;
      const blob = await buildCombinedBundle({
        enemyName, kind: entry.kind, libraryId: entry.id, sprite: entry.sprite, configObj: exportObj,
      });
      downloadBlob(blob, `${enemyName.replace(/[^A-Za-z0-9_]/g, '_')}.bundle.zip`);
      await persistConfigToLibraryIfLinked(exportObj);
    });
  }

  setArtMode(state.artMode);

  // ============================================================ Sprites-tab lane cross-reference (display-only)

  function refreshLaneInfo() {
    const names = getLaneNames ? getLaneNames() : [];
    if (!names || names.length === 0) {
      laneInfoEl.hidden = true;
      return;
    }
    laneInfoEl.hidden = false;
    laneInfoEl.textContent = `Animaciones detectadas en esta sesión (pestaña Sprites): ${names.join(', ')}`;
  }

  refresh();

  /**
   * Links this draft to a library entry from outside (the Biblioteca tab's "Editar en Enemy
   * Creator" action) — switches to library mode, selects it, and pre-fills enemyName if empty.
   */
  async function linkLibraryEntry(id, enemyName) {
    await refreshArtLibraryOptions();
    state.artLibraryId = id;
    artLibrarySelect.value = id;
    if (!state.enemyName.trim() && enemyName) {
      state.enemyName = enemyName;
      nameField.input.value = enemyName;
    }
    setArtMode('library');
  }

  return { refreshLaneInfo, refreshArtLibrary: refreshArtLibraryOptions, linkLibraryEntry };
}
