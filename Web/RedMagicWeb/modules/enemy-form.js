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
//
// "Ataques" (last section) is the enemy's DECK: its built-in attack plus any number of catalog
// attacks — the same generic attacks bosses use, drawn with the shared cards of
// modules/attack-cards.js and exported by modules/attack-export.js (top-level `attacks` /
// `artAssets` + `tuning.attacks`). An enemy without catalog attacks exports exactly what it
// always did.
//
// Picking (or being sent) a library entry that already has a saved behaviour loads it into the
// form: adoptConfig() writes the state and every bound widget follows (bind() / syncers).

import * as F from './enemy-form-fields.js';
import { createDefence } from './enemy-defaults.js';
import { createDefaultEnemyConfig, createDefaultProjectileSpec, PROJECTILE_AIMING_FIELDS } from './enemy-defaults.js';
import { buildEnemyExport } from './enemy-export.js';
import { createAttackEditor, newAttack } from './attack-cards.js';
import { normalizeGesture, effectiveReleases } from './rig-gestures.js';
import { adoptAttacksFromConfig } from './attack-export.js';
import { createArtOptions, defaultsOf as defaultsOfFields } from './boss-defaults.js';
import { framedLanes } from './boss-export.js';
import { ART_KIND_NOTES, attackLabel } from './boss-catalog-notes.js';
import { suggestedEnemyConfig } from './rig-templates.js';
import { loadEnemyConfigValidator, validateEnemyConfig } from './enemy-config-schema.js';
import { downloadBlob } from './export-manifest.js';
import { listEntries, getEnemy, updateEnemy } from './enemy-library.js';
import { buildCombinedBundle } from './enemy-bundle.js';
import { adoptRigState } from './rig-defaults.js';

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

  // Unity's attack catalog (generated file; may be missing on a fresh checkout) and the library
  // entries the deck's art slots can pick. Both only matter to the "Ataques" section.
  let catalog = null;
  let artEntries = [];
  let artEntriesById = new Map();
  const catalogReady = import('./boss-catalog.js')
    .then((m) => { catalog = m.BOSS_CATALOG; })
    .catch(() => { catalog = null; });

  /**
   * Widgets are built once, bound to the state's INITIAL values. bind() remembers how to write the
   * current value back into a widget, so adoptConfig() can load a saved config into the form
   * without rebuilding it.
   */
  const syncers = [];

  // refresh() runs while the form is still being built (the projectile toggle calls it); the
  // "Ataques" section — built last — only takes part once it exists.
  let deckReady = false;

  const escapeHtml = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));

  function bind(kind, built, get) {
    syncers.push(() => {
      const v = get();
      if (kind === 'bool') built.input.checked = !!v;
      else if (kind === 'vector2') { built.xInput.value = v?.x ?? 0; built.yInput.value = v?.y ?? 0; }
      else if (kind === 'color') {
        const hex = typeof v === 'string' && v.length >= 7 ? v : '#ffffffff';
        built.colorInput.value = hex.slice(0, 7);
        built.alphaInput.value = Math.round((parseInt(hex.slice(7, 9) || 'ff', 16) / 255) * 100);
      } else if (built.input) built.input.value = v ?? '';
    });
    return built;
  }

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
  bind('text', nameField, () => state.enemyName);
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
  bind('text', manualArtField, () => state.art);
  registerError('/art', manualArtField.setError);
  topSection.appendChild(manualArtField.row);

  let libraryEntriesCache = [];

  // Enemy-kind entries only. A projectile or VFX entry is a perfectly valid library record but
  // not a valid 'art' for an EnemyConfig: EnemyFactory reads that sheet for the enemy's own body
  // clips and its thrown prop, neither of which a projectile/VFX sheet has. Offering them would
  // produce a config that imports into a broken enemy instead of failing at authoring time.
  // Part-based enemies (rig entries) are NOT offered here: 🧬 Enemy Studio links this form to its
  // own body (linkLibraryEntry) and saves the behaviour on the rig record as `enemyConfig`. They stay
  // in the cache so that link resolves; the picker of the 🧩 tab only lists sprite sheets.
  async function refreshArtLibraryOptions() {
    const rigs = (await listEntries('rig')).filter((e) => e.draft && e.draft.kind !== 'boss');
    const sheets = await listEntries('enemy');
    libraryEntriesCache = [...sheets, ...rigs];
    if (sheets.length === 0) {
      artLibrarySelect.innerHTML = '<option value="">(sin enemigos guardados — usa la pestaña Sprites)</option>';
      return;
    }
    artLibrarySelect.innerHTML = ['<option value="">Selecciona un enemigo guardado…</option>']
      .concat(sheets.map((e) =>
        `<option value="${e.id}" ${e.id === state.artLibraryId ? 'selected' : ''}>${e.enemyName} — ${new Date(e.updatedAt).toLocaleString()}</option>`
      )).join('');
  }

  artLibrarySelect.addEventListener('change', async () => {
    state.artLibraryId = artLibrarySelect.value || null;
    const picked = libraryEntriesCache.find((e) => e.id === state.artLibraryId);
    if (picked && !state.enemyName.trim()) {
      state.enemyName = picked.enemyName;
      nameField.input.value = picked.enemyName;
    }
    updateCombinedExportAvailability();
    await loadSavedBehaviour(picked);
    deckEditor.refreshGestures();
    renderDefences();
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
  bind('enum', anchorField, () => state.anchor);
  registerError('/anchor', anchorField.setError);
  topSection.appendChild(anchorField.row);

  const prefabFolderField = F.textField({
    label: 'Carpeta del prefab (prefabFolder)', value: state.prefabFolder,
    hint: 'Vacío = Assets/Prefabs/Enemies.',
    onChange: (v) => { state.prefabFolder = v; refresh(); },
  });
  bind('text', prefabFolderField, () => state.prefabFolder);
  registerError('/prefabFolder', prefabFolderField.setError);
  topSection.appendChild(prefabFolderField.row);

  const tierField = F.enumField({
    label: 'Tier de economía (tier)', value: state.tier,
    options: [{ value: 'Basic', label: 'Basic' }, { value: 'Elite', label: 'Elite' }, { value: 'Boss', label: 'Boss' }],
    hint: 'Tabla de drop de moneda por tier (CurrencyConfig).',
    onChange: (v) => { state.tier = v; refresh(); },
  });
  bind('enum', tierField, () => state.tier);
  registerError('/tier', tierField.setError);
  topSection.appendChild(tierField.row);

  // Rows that only make sense for a sprite-sheet enemy picked by hand (name, which art, pivot,
  // folder): 🧬 Enemy Studio hides them, there the body on screen IS the art.
  [nameField.row, artModeRow, artLibraryRow, manualArtField.row, anchorField.row, prefabFolderField.row]
    .forEach((row) => row.classList.add('efArtIdentity'));
  formRoot.appendChild(topSection);

  // ============================================================ presence

  const presenceSection = document.createElement('details');
  presenceSection.className = 'efSection efFold efSheetOnly';
  presenceSection.innerHTML = '<summary>Presencia (escala, collider, orden de dibujado, etiqueta)</summary>';

  presenceSection.appendChild(bind('number', F.numberField({
    label: 'Escala del sprite', value: state.presence.spriteScale, min: 0.01,
    onChange: (v) => { state.presence.spriteScale = v; refresh(); },
  }), () => state.presence.spriteScale).row);
  presenceSection.appendChild(bind('vector2', F.vector2Field({
    label: 'Tamaño del collider (0,0 = automático)', value: state.presence.colliderSize,
    onChange: (v) => { state.presence.colliderSize = v; refresh(); },
  }), () => state.presence.colliderSize).row);
  presenceSection.appendChild(bind('vector2', F.vector2Field({
    label: 'Desfase del collider (0,0 = automático)', value: state.presence.colliderOffset,
    onChange: (v) => { state.presence.colliderOffset = v; refresh(); },
  }), () => state.presence.colliderOffset).row);
  presenceSection.appendChild(bind('number', F.numberField({
    label: 'Orden de dibujado (sortingOrder)', value: state.presence.sortingOrder, step: 1,
    onChange: (v) => { state.presence.sortingOrder = Math.round(v); refresh(); },
  }), () => state.presence.sortingOrder).row);
  presenceSection.appendChild(bind('text', F.textField({
    label: 'Etiqueta (tag)', value: state.presence.tag,
    hint: '"Untagged" normal; "Enemy" para esbirros de un jefe.',
    onChange: (v) => { state.presence.tag = v; refresh(); },
  }), () => state.presence.tag).row);

  formRoot.appendChild(presenceSection);

  // ============================================================ projectileArt (de-emphasized unless ranged)

  const projectileArtSection = document.createElement('details');
  projectileArtSection.className = 'efSection efFold efSheetOnly';
  projectileArtSection.innerHTML = '<summary>Arte del proyectil desde una hoja de sprites <span class="efRangedBadge">sólo arquetipos a distancia</span></summary>';

  projectileArtSection.appendChild(bind('text', F.textField({
    label: 'Fila con el prop suelto (propState)', value: state.projectileArt.propState,
    hint: 'Fila de la lámina cuyo objeto suelto se convierte en el proyectil.',
    onChange: (v) => { state.projectileArt.propState = v; refresh(); },
  }), () => state.projectileArt.propState).row);
  projectileArtSection.appendChild(bind('number', F.numberField({
    label: 'Escala del sprite del proyectil', value: state.projectileArt.scale, min: 0.01,
    onChange: (v) => { state.projectileArt.scale = v; refresh(); },
  }), () => state.projectileArt.scale).row);

  formRoot.appendChild(projectileArtSection);

  // ============================================================ tuning

  const tuningSection = document.createElement('div');
  tuningSection.className = 'efSection';
  tuningSection.innerHTML = '<h2>Más parámetros <span class="efHint">(plegados: ábrelos cuando quieras tocar algo)</span></h2>';
  formRoot.appendChild(tuningSection);

  // --- Tipo (archetype/staticAttack) ---
  const tipoGroup = addGroup(tuningSection, 'Tipo de enemigo', true);
  const archetypeField = F.enumField({
    label: 'Arquetipo', value: state.tuning.archetype,
    options: ['Static', 'Melee', 'Ranged', 'FlyingMelee', 'FlyingRanged'].map((v) => ({ value: v, label: v })),
    hint: 'Decide cómo se mueve y cómo ataca.',
    onChange: (v) => { state.tuning.archetype = v; updateRangedEmphasis(); applyRangedDefaultsIfUntouched(); refresh(); },
  });
  bind('enum', archetypeField, () => state.tuning.archetype);
  registerError('/tuning/archetype', archetypeField.setError);
  tipoGroup.appendChild(archetypeField.row);

  const staticAttackField = F.enumField({
    label: 'Ataque de Static (staticAttack)', value: state.tuning.staticAttack,
    options: [{ value: 'Melee', label: 'Melee' }, { value: 'Ranged', label: 'Ranged' }],
    hint: 'Sólo se usa si el arquetipo es Static.',
    onChange: (v) => { state.tuning.staticAttack = v; updateRangedEmphasis(); applyRangedDefaultsIfUntouched(); refresh(); },
  });
  bind('enum', staticAttackField, () => state.tuning.staticAttack);
  registerError('/tuning/staticAttack', staticAttackField.setError);
  tipoGroup.appendChild(staticAttackField.row);

  // --- generic flat fields, driven by TUNING_FIELDS ---
  const groupEls = { Tipo: tipoGroup };
  const rangedRows = []; // rows to de-emphasize when the enemy isn't ranged
  const fieldInputs = {}; // key -> <input>, so applyRangedDefaultsIfUntouched() can update what's on screen

  TUNING_FIELDS.forEach(([groupName, key, kind, label, opts]) => {
    const group = groupEls[groupName] || (groupEls[groupName] = addGroup(tuningSection, groupName, groupName === 'Ataque · a distancia'));
    let built;

    const onChange = (v) => { state.tuning[key] = v; refresh(); };

    if (kind === 'number') built = F.numberField({ label, value: state.tuning[key], min: opts.min, max: opts.max, step: opts.step, onChange });
    else if (kind === 'bool') built = F.boolField({ label, value: state.tuning[key], onChange });
    else if (kind === 'text') built = F.textField({ label, value: state.tuning[key], onChange });
    else if (kind === 'assetRef') built = F.assetRefField({ label, value: state.tuning[key], onChange });
    else if (kind === 'vector2') built = F.vector2Field({ label, value: state.tuning[key], onChange });
    else if (kind === 'color') built = F.colorField({ label, value: state.tuning[key], onChange });

    bind(kind, built, () => state.tuning[key]);
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

    // Los números del proyectil (velocidad, vida, tamaño…) plegados: lo habitual es sólo elegir el arte.
    const numbers = document.createElement('details');
    numbers.className = 'efFold efSubFold';
    numbers.innerHTML = '<summary>Números del proyectil (velocidad, vida, tamaño, boca…)</summary>';
    projInlineBox.appendChild(numbers);

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
      numbers.appendChild(built.row);
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

  // A layerMask widget has no single input to write into: adopting a config rebuilds its row.
  function layerMaskRow(key, label, hint) {
    const build = () => F.layerMaskField({
      label, value: state.tuning[key], knownLayers: KNOWN_LAYERS, hint,
      onChange: (v) => { state.tuning[key] = v; refresh(); },
    }).row;
    let row = build();
    objetivoGroup.appendChild(row);
    syncers.push(() => { const next = build(); row.replaceWith(next); row = next; });
  }
  layerMaskRow('hitLayers', 'Capas que puede dañar (hitLayers)');
  layerMaskRow('obstacleLayers', 'Capas de terreno (obstacleLayers)',
    'Normalmente Ground + Platform — ver COMPATIBILITY.md sobre por qué faltar Platform rompe enemigos en puentes.');

  // ============================================================ ataques (la baraja)

  const deckSection = document.createElement('div');
  deckSection.className = 'efSection efDeck';
  deckSection.innerHTML = '<h2>Ataques</h2>';
  formRoot.appendChild(deckSection);

  const deckIntro = document.createElement('p');
  deckIntro.className = 'efHint';
  deckIntro.textContent = 'Un enemigo puede tener VARIOS ataques. Cada vez que le toca atacar (lo decide su rango y '
    + '"Enfriamiento de ataque") se sortea uno, por peso, entre los que están a su distancia y fuera de '
    + 'enfriamiento. Sin ataques de catálogo sale siempre el ataque propio, como hasta ahora.';
  deckSection.appendChild(deckIntro);

  // --- 1ª carta: el ataque propio (melé / disparo / explosión, los números de "Tuning ▸ Ataque").
  const builtInCard = addGroup(deckSection, 'Ataque propio (el de Más parámetros ▸ Ataque)', true);
  const builtInSummary = document.createElement('p');
  builtInSummary.className = 'efHint';
  builtInCard.appendChild(builtInSummary);
  builtInCard.appendChild(bind('bool', F.boolField({
    label: 'Entra en el sorteo', value: state.tuning.builtInAttack,
    hint: 'Desmarcado = sólo ataca con los ataques de catálogo de abajo. Sin ninguno, el propio sale siempre.',
    onChange: (v) => { state.tuning.builtInAttack = v; state.deckTouched = true; refresh(); },
  }), () => state.tuning.builtInAttack).row);
  builtInCard.appendChild(bind('number', F.numberField({
    label: 'Peso en el sorteo', value: state.tuning.builtInWeight, min: 0.01,
    hint: 'Frente al "weight" de cada ataque de catálogo (1 por defecto).',
    onChange: (v) => { state.tuning.builtInWeight = v; state.deckTouched = true; refresh(); },
  }), () => state.tuning.builtInWeight).row);

  function describeBuiltIn() {
    const t = state.tuning;
    const what = t.selfDestruct ? `explota (radio ${t.explosionRadius})`
      : isRangedNow() ? 'dispara su proyectil' : 'golpea de cerca (caja de melé)';
    builtInSummary.textContent = `${what} · daño ${t.attackDamage} · a ${t.attackRange} u · gesto: el ataque del cuerpo (Attack / windup del rig). `
      + 'Para otro disparo o golpe con SU gesto y SU proyectil, añade abajo un "Disparo / golpe (por sueltas del gesto)".';
  }

  // --- el resto: ataques del catálogo de Unity (los mismos que usan los jefes).
  const deckRoot = document.createElement('div');
  deckSection.appendChild(deckRoot);
  const deckArtRoot = document.createElement('div');
  deckSection.appendChild(deckArtRoot);

  /** The linked art entry (a sprite-sheet enemy or a 🦴 rig), or null in manual-path mode. */
  function linkedEntry() {
    if (state.artMode !== 'library' || !state.artLibraryId) return null;
    return libraryEntriesCache.find((e) => e.id === state.artLibraryId) || null;
  }

  // The gestures of the body, live: 🧬 Enemy Studio hands in the composer's own list while it is on
  // screen (setGestureSource), so a release added on the timeline shows up here at once. Otherwise
  // they are read from the linked rig's saved document.
  let gestureSource = null;

  /** The body's gestures as timelines (rig-gestures.js), or [] (a sprite sheet has animations, not gestures). */
  function bodyGestures() {
    if (gestureSource) return gestureSource() || [];
    const entry = linkedEntry();
    if (entry?.kind !== 'rig') return [];
    return (entry.draft?.gestures || []).map((g) => { const copy = JSON.parse(JSON.stringify(g)); normalizeGesture(copy); return copy; });
  }

  /** Gestures the body can play: a rig's named gestures, or a sprite sheet's animation names. */
  function bodyGestureNames() {
    const entry = linkedEntry();
    if (gestureSource || entry?.kind === 'rig') return bodyGestures().map((g) => g.name).filter(Boolean);
    if (!entry) return [];
    return framedLanes(entry).map((l) => l.name);
  }

  /** What a gesture releases — one label per release — or null (not a rig gesture: one release, by its clip). */
  function gestureInfo(name) {
    const g = bodyGestures().find((x) => x.name === name);
    if (!g) return null;
    return {
      releases: effectiveReleases(g).map((r) => {
        const step = g.steps[Math.min(r.step, g.steps.length - 1)];
        const label = `«${step?.name || `paso ${r.step + 1}`}»`;
        return r.at >= 1 ? `al acabar ${label}` : r.at <= 0 ? `al empezar ${label}` : `${label} al ${Math.round(r.at * 100)}%`;
      }),
    };
  }

  const releaseListOf = (attack) => (catalog?.attacks.find((t) => t.type === attack.type)?.params || [])
    .find((p) => p.type === 'objectList' && p.perRelease) || null;

  /**
   * Every per-release list of the deck gets exactly one entry per release of its attack's gesture
   * (the same rule the card applies when it is drawn) — so the export is right even for a card that
   * was never opened after its gesture gained or lost a release.
   */
  function syncReleaseSlots() {
    state.deck.forEach((attack) => {
      const field = releaseListOf(attack);
      const info = field ? gestureInfo((attack.common.gesture || '').trim()) : null;
      if (!info) return;
      const list = Array.isArray(attack.params[field.name]) ? attack.params[field.name] : (attack.params[field.name] = []);
      while (list.length < info.releases.length) list.push(defaultsOfFields(field.fields));
      if (list.length > info.releases.length) list.length = info.releases.length;
    });
  }

  // What mirrors the deck somewhere else (the gesture card of Enemy Studio) redraws when the cards do.
  let onDeckRendered = null;
  // …and what only reads its numbers (the Studio's test stage: an attack's telegraph, its shots) is told of every edit.
  let onDeckChanged = null;

  const deckEditor = createAttackEditor({
    root: deckRoot,
    getCatalog: () => catalog,
    getAttacks: () => state.deck,
    getEntries: () => artEntries,
    gestureNames: bodyGestureNames,
    gestureInfo,
    defaultType: 'ReleaseAttack',
    onRendered: () => { if (onDeckRendered) onDeckRendered(); },
    onRemoved: (attack) => { state.defences.forEach((d) => { if (d.attack === attack.id) d.attack = ''; }); renderDefences(); },
    onRenamed: (oldId, newId) => { state.defences.forEach((d) => { if (d.attack === oldId) d.attack = newId; }); renderDefences(); },
    onAdded: () => { renderDefences(); },
    noGesturesLabel: '(elige arriba el arte del enemigo: sus gestos salen de ahí)',
    // Attacks that need a boss (phases, guard, adds, arena targets) are not offered to an enemy.
    typeFilter: (a) => a.hosts !== 'boss',
    // No phases and no punish windows on a normal enemy: these three do nothing there.
    hiddenCommon: ['minPhase', 'vulnerableSeconds', 'vulnerableMultiplier'],
    intro: 'Ataques de catálogo: tipos genéricos (los mismos de los jefes) con su gesto, sus números y su arte. '
      + 'Cada uno puede usar un gesto distinto del cuerpo: tres hechizos desde la misma mano = tres ataques con tres gestos. '
      + 'Lo que dejes vacío funciona con un placeholder.',
    idHint: 'Nombra el asset en Unity. Letras, números, - y _. Se aplica al salir del campo.',
    renderExtra: (attack, card) => {
      attack.deck = { minRange: 0, maxRange: 0, targetHeight: 'Any', defenceOnly: false, ...(attack.deck || {}) };
      card.appendChild(F.boolField({
        label: 'Sólo desde una defensa (no entra en el sorteo)', value: !!attack.deck.defenceOnly,
        hint: 'Para el ataque que lanza al apartarse ("esquiva y dispara"): elígelo en Defensa ▸ "Ataque que lanza".',
        onChange: (v) => { attack.deck.defenceOnly = v; state.deckTouched = true; deckEditor.render(); refresh(); },
      }).row);
      if (attack.deck.defenceOnly) return;
      const grid = document.createElement('div');
      grid.className = 'bcGrid';
      grid.appendChild(F.numberField({
        label: 'Distancia mínima (u)', value: attack.deck.minRange, min: 0,
        hint: 'Más cerca que esto no sale. 0 = sin mínimo.',
        onChange: (v) => { attack.deck.minRange = v; refresh(); },
      }).row);
      grid.appendChild(F.numberField({
        label: 'Distancia máxima (u)', value: attack.deck.maxRange, min: 0,
        hint: '0 = el "Rango de ataque" del enemigo. Mayor = lo lanza mientras aún se acerca.',
        onChange: (v) => { attack.deck.maxRange = v; refresh(); },
      }).row);
      grid.appendChild(F.enumField({
        label: 'Altura del objetivo', value: attack.deck.targetHeight,
        options: [
          { value: 'Any', label: 'Cualquiera' },
          { value: 'TargetAbove', label: 'Sólo si está por encima' },
          { value: 'TargetBelow', label: 'Sólo si está por debajo' },
        ],
        hint: 'Medio metro de margen respecto al centro del enemigo.',
        onChange: (v) => { attack.deck.targetHeight = v; refresh(); },
      }).row);
      card.appendChild(grid);
    },
    onStructure: () => { state.deckTouched = true; },
    onChange: () => { state.deckTouched = true; refresh(); if (onDeckChanged) onDeckChanged(); },
  });

  /** Size/collider options of every library art the deck uses (same knobs as the Boss Creator). */
  let deckArtSignature = null;
  function renderDeckArt(art) {
    const sig = art.map((a) => a.key).join('|') + `#${artEntries.length}`;
    if (sig === deckArtSignature) return;
    deckArtSignature = sig;

    deckArtRoot.innerHTML = '';
    if (art.length === 0) return;

    const box = addGroup(deckArtRoot, 'Arte usado por los ataques', true);
    art.forEach(({ id, key, kind, libraryId }) => {
      const entry = artEntriesById.get(libraryId);
      const opts = state.attackArt[key] || (state.attackArt[key] = createArtOptions(kind));
      const title = document.createElement('p');
      title.className = 'efHint';
      title.textContent = `${id} — ${ART_KIND_NOTES[kind]?.label || kind} · ${entry?.enemyName || libraryId}`;
      box.appendChild(title);
      const grid = document.createElement('div');
      grid.className = 'bcGrid';
      if (kind !== 'warning') grid.appendChild(F.numberField({ label: 'Ancho en el mundo (u)', value: opts.worldWidth, min: 0.05, onChange: (v) => { opts.worldWidth = v; refresh(); } }).row);
      if (kind === 'projectile') {
        grid.appendChild(F.numberField({ label: 'Collider (× dibujo)', value: opts.colliderScale, min: 0.05, onChange: (v) => { opts.colliderScale = v; refresh(); } }).row);
        grid.appendChild(F.boolField({ label: 'Gira hacia donde vuela', value: opts.faceTravelDirection, onChange: (v) => { opts.faceTravelDirection = v; refresh(); } }).row);
      }
      if (kind === 'warning') grid.appendChild(F.boolField({ label: 'Teñir con el color del ataque', value: opts.tint, onChange: (v) => { opts.tint = v; refresh(); } }).row);
      box.appendChild(grid);
    });
  }

  async function loadArtEntries() {
    artEntries = (await listEntries()).filter((e) => e.sprite && e.kind !== 'boss');
    artEntriesById = new Map(artEntries.map((e) => [e.id, e]));
    deckArtSignature = null;
  }

  /** What every export of this form needs beyond the state itself. */
  const exportContext = () => ({
    catalog,
    entriesById: artEntriesById,
    gestureNames: linkedEntry() ? bodyGestureNames() : null,
  });

  // ============================================================ defensa (movimientos defensivos)

  const defenceSection = document.createElement('div');
  defenceSection.className = 'efSection efDefence';
  defenceSection.innerHTML = '<h2>Defensa</h2>';
  const defenceIntro = document.createElement('p');
  defenceIntro.className = 'efHint';
  defenceIntro.textContent = 'Cómo se aparta y cuándo: si te acercas demasiado, al recibir un golpe o con poca vida. '
    + 'Nunca corta un ataque ya lanzado. Sin ninguno, no esquiva.';
  defenceSection.appendChild(defenceIntro);
  const defencesRoot = document.createElement('div');
  defenceSection.appendChild(defencesRoot);

  const DEFENCE_TRIGGERS = [
    { value: 'TargetNear', label: 'Si el jugador se acerca a menos de…' },
    { value: 'OnHit', label: 'Al recibir un golpe' },
    { value: 'LowHealth', label: 'Al quedarle poca vida (una vez)' },
    { value: 'AfterAttack', label: 'Al acabar un ataque (reposicionarse)' },
  ];
  const DEFENCE_DIRECTIONS = [
    { value: 'Away', label: 'Alejándose del jugador' },
    { value: 'Toward', label: 'Hacia el jugador' },
    { value: 'ByRange', label: 'Según la distancia (cerca: huye; lejos: avanza)' },
  ];
  const DEFENCE_MOVES = [
    { value: 'DashAway', label: 'Sale disparado hacia atrás' },
    { value: 'HopBack', label: 'Salto hacia atrás' },
    { value: 'Strafe', label: 'De lado (voladores; en suelo = hacia atrás)' },
  ];

  function renderDefences() {
    if (!defencesRoot) return;
    defencesRoot.innerHTML = '';
    const list = state.defences;
    const touch = () => { state.defencesTouched = true; refresh(); };

    list.forEach((d, index) => {
      const card = document.createElement('div');
      card.className = 'bcCard';
      const head = document.createElement('div');
      head.className = 'bcCardHead';
      const title = document.createElement('span');
      title.className = 'bcCardTitle';
      title.textContent = `Defensa ${index + 1}`;
      const del = document.createElement('button');
      del.className = 'small danger';
      del.textContent = '✕';
      del.onclick = () => { list.splice(index, 1); renderDefences(); touch(); };
      head.append(title, del);
      card.appendChild(head);

      const grid = document.createElement('div');
      grid.className = 'bcGrid';
      const num = (root, labelText, key, min, hint, step) => {
        const f = F.numberField({ label: labelText, value: d[key], min, hint, step, onChange: (x) => { d[key] = x; touch(); } });
        root.appendChild(f.row);
      };
      grid.appendChild(F.enumField({ label: 'Cuándo', value: d.trigger, options: DEFENCE_TRIGGERS,
        onChange: (x) => { d.trigger = x; renderDefences(); touch(); } }).row);
      if (d.trigger === 'TargetNear') num(grid, 'Distancia (u)', 'distance', 0, 'Más cerca que esto, se aparta.', '0.5');
      if (d.trigger === 'LowHealth') num(grid, 'Vida (0–1)', 'healthFraction', 0, '0.35 = al bajar del 35%.', '0.05');
      grid.appendChild(F.enumField({ label: 'Cómo', value: d.move, options: DEFENCE_MOVES,
        onChange: (x) => { d.move = x; renderDefences(); touch(); } }).row);

      grid.appendChild(F.enumField({ label: 'Hacia dónde', value: d.direction || 'Away', options: DEFENCE_DIRECTIONS,
        hint: 'Según la distancia: si el jugador está más cerca de la distancia de huida que del alcance de ataque, se aparta; si está más cerca del alcance, avanza.',
        onChange: (x) => { d.direction = x; touch(); } }).row);

      const names = bodyGestureNames();
      const opts = [{ value: '', label: names.length ? '(sin gesto)' : '(elige arriba el arte del enemigo)' },
        ...names.map((n) => ({ value: n, label: n }))];
      if (d.gesture && !names.includes(d.gesture)) opts.push({ value: d.gesture, label: `${d.gesture} (no existe)` });
      grid.appendChild(F.enumField({ label: 'Gesto mientras se aparta', value: d.gesture || '', options: opts,
        hint: d.attack ? 'Con un ataque elegido manda el gesto del ataque: éste no se usa.' : 'Mantiene su pose de carga (con sus efectos y sprites) durante el movimiento.',
        onChange: (x) => { d.gesture = x; touch(); } }).row);

      const attackOpts = [{ value: '', label: '(ninguno: sólo se aparta)' },
        ...state.deck.map((a) => ({ value: a.id, label: `${a.id}${a.common.gesture ? ` · gesto ${a.common.gesture}` : ''}` }))];
      if (d.attack && !state.deck.some((a) => a.id === d.attack)) attackOpts.push({ value: d.attack, label: `${d.attack} (no existe)` });
      grid.appendChild(F.enumField({ label: 'Ataque que lanza al apartarse', value: d.attack || '', options: attackOpts,
        hint: 'Uno de los ataques de arriba. Su gesto se reproduce ENTERO mientras se aparta y el ataque sale en sus sueltas: '
          + 'con un paso que "dura el aviso", la primera cae justo al acabar de apartarse. Márcalo "sólo desde una defensa" si no debe salir en el sorteo.',
        onChange: (x) => { d.attack = x; renderDefences(); touch(); } }).row);
      card.appendChild(grid);

      const more = document.createElement('details');
      more.className = 'efFold';
      const sum = document.createElement('summary');
      sum.textContent = 'Números (probabilidad, enfriamiento, recorrido)';
      more.appendChild(sum);
      const moreGrid = document.createElement('div');
      moreGrid.className = 'bcGrid';
      num(moreGrid, 'Probabilidad (0–1)', 'chance', 0, 'De hacerlo cuando se cumple la condición.', '0.1');
      num(moreGrid, 'Enfriamiento (s)', 'cooldown', 0, 'Hasta poder repetirlo.', '0.5');
      moreGrid.appendChild(F.boolField({ label: 'Diagonal según el terreno (voladores)', value: !!d.diagonalByTerrain,
        hint: 'Horizontal: según "Hacia dónde". Vertical: más cerca del techo baja, más cerca del suelo sube.',
        onChange: (x) => { d.diagonalByTerrain = x; renderDefences(); touch(); } }).row);
      if (d.diagonalByTerrain) {
        num(moreGrid, 'Y del suelo', 'floorY', -1000, undefined, '0.5');
        num(moreGrid, 'Y del techo', 'ceilingY', -1000, undefined, '0.5');
        num(moreGrid, 'Ángulo (°)', 'angle', 0, '45 = diagonal limpia.', '5');
        num(moreGrid, 'Varianza (± °)', 'angleVariance', 0, '10 con 45 = entre 35° y 55°.', '1');
      }
      num(moreGrid, 'Recorrido (u)', 'moveDistance', 0, undefined, '0.5');
      if (d.move === 'HopBack') num(moreGrid, 'Altura del salto (u)', 'hopHeight', 0, 'En el suelo, el salto dura lo que tarde en caer.', '0.1');
      num(moreGrid, 'Duración (s)', 'moveDuration', 0.05, d.move === 'HopBack' ? 'Sólo voladores: en el suelo manda el salto.' : undefined, '0.05');
      num(moreGrid, 'Aviso antes de moverse (s)', 'anticipation', 0, 'Quieto, con el gesto ya puesto.', '0.05');
      more.appendChild(moreGrid);
      card.appendChild(more);
      defencesRoot.appendChild(card);
    });

    const add = document.createElement('button');
    add.className = 'small primary';
    add.textContent = '＋ Añadir movimiento defensivo';
    add.onclick = () => { list.push(createDefence()); state.defencesTouched = true; renderDefences(); refresh(); };
    defencesRoot.appendChild(add);
  }
  renderDefences();

  // ============================================================ orden final: lo importante a mano

  // Arriba: el enemigo (nombre y cuerpo), su tipo, su proyectil y sus ataques. Debajo, plegado, el resto.
  const quick = document.createElement('div');
  quick.className = 'efSection';
  quick.innerHTML = '<h2>Tipo, proyectil y ataques</h2>';
  quick.append(tipoGroup, groupEls['Ataque · a distancia']);
  formRoot.insertBefore(quick, tuningSection);
  formRoot.insertBefore(deckSection, tuningSection);
  formRoot.insertBefore(defenceSection, tuningSection);
  formRoot.append(presenceSection, projectileArtSection);   // lo menos usado, al final y plegado

  // Lo de importar / generar, fuera del paso: vida y tier junto a las estadísticas.
  const importOptions = addGroup(topSection, 'Opciones de importación (pivote, carpeta del prefab)', false);
  importOptions.append(anchorField.row, prefabFolderField.row);
  groupEls['Vida'].appendChild(tierField.row);

  deckReady = true;
  catalogReady.then(loadArtEntries).then(() => { deckEditor.render(); refresh(); });

  /** A foldable group of fields (closed unless `open`). */
  function addGroup(parent, title, open = false) {
    const g = document.createElement('details');
    g.className = 'efGroup efFold';
    g.open = open;
    const h = document.createElement('summary');
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
    if (deckReady) syncReleaseSlots();
    const { doc: exportObj, art, problems, notes } = buildEnemyExport(state, deckReady ? exportContext() : {});
    previewEl.textContent = JSON.stringify(exportObj, null, 2);
    if (deckReady) {
      describeBuiltIn();
      renderDeckArt(art);
    }

    if (!validate) return; // still loading the schema
    clearAllErrors();

    const { valid, errors } = validateEnemyConfig(validate, exportObj);
    exportBtn.disabled = !valid || problems.length > 0;

    // Only an enemy with a deck has anything to say here: without one this stays one line.
    const deckHtml = (problems.length ? `<p class="efSummaryError">✗ Ataques:</p><ul>${problems.map((p) => `<li>${escapeHtml(p)}</li>`).join('')}</ul>` : '')
      + (notes.length ? `<p class="efHint">Avisos de los ataques:</p><ul class="bcNotes">${notes.map((n) => `<li>${escapeHtml(n)}</li>`).join('')}</ul>` : '');

    if (valid) {
      summaryEl.innerHTML = '<p class="efSummaryOk">✓ Válido según enemy-config.schema.json</p>' + deckHtml;
      return;
    }

    errors.forEach((e) => { if (fieldErrorSetters[e.path]) fieldErrorSetters[e.path](e.message); });
    summaryEl.innerHTML =
      `<p class="efSummaryError">✗ ${errors.length} error${errors.length > 1 ? 'es' : ''} de validación:</p>` +
      `<ul>${errors.map((e) => `<li><code>${e.path || '(raíz)'}</code> — ${e.message}</li>`).join('')}</ul>` + deckHtml;
  }

  // ============================================================ cargar un comportamiento guardado

  /**
   * A saved EnemyConfig back into the form: state first, then every bound widget. Keys the config
   * does not carry go back to the form's defaults — it is "open that enemy", not a merge.
   */
  function adoptConfig(config) {
    if (!config) return;
    const fresh = createDefaultEnemyConfig();
    const t = config.tuning || {};
    const { projectile, attacks: deckJson, defences: defencesJson, ...flat } = t;
    state.defences = (Array.isArray(defencesJson) ? defencesJson : []).map((d) => ({ ...createDefence(), ...d }));
    state.defencesTouched = Array.isArray(defencesJson);

    state.anchor = config.anchor || fresh.anchor;
    state.prefabFolder = config.prefabFolder || fresh.prefabFolder;
    state.tier = config.tier || fresh.tier;
    state.presence = { ...fresh.presence, ...(config.presence || {}) };
    state.projectileArt = { ...fresh.projectileArt, ...(config.projectileArt || {}) };

    // In place: every onChange closure writes through state.tuning.
    Object.keys(state.tuning).forEach((k) => delete state.tuning[k]);
    Object.assign(state.tuning, fresh.tuning, flat);
    state.tuning.projectileMode = projectile ? 'inline' : 'default';
    state.tuning.projectileSpec = { ...createDefaultProjectileSpec(), ...(projectile || {}) };

    const adopted = adoptAttacksFromConfig(config, catalog);
    state.attackArt = adopted.artOptions;
    const cards = Array.isArray(deckJson) ? deckJson : [];
    const inDraw = new Set(cards.map((e) => e.attack));
    state.deck = [];
    cards.forEach((e) => {
      // A config saved before 2026-10-02: "the own attack again with another gesture". It is now a
      // catalog attack that fires the same thing at each release of that gesture.
      if (e.builtIn) { state.deck.push(attackFromVariant(e)); return; }
      const attack = adopted.byId[e.attack];
      if (!attack) return;   // a card that points at a Unity asset: only editable there
      attack.deck = { minRange: e.minRange ?? 0, maxRange: e.maxRange ?? 0, targetHeight: e.targetHeight || 'Any', defenceOnly: false };
      state.deck.push(attack);
    });
    // Attacks of the config that are not cards of the draw: the ones a defence launches.
    Object.entries(adopted.byId).forEach(([id, attack]) => {
      if (inDraw.has(id) || state.deck.includes(attack)) return;
      attack.deck = { minRange: 0, maxRange: 0, targetHeight: 'Any', defenceOnly: true };
      state.deck.push(attack);
    });
    state.deckTouched = Array.isArray(deckJson);

    syncers.forEach((sync) => sync());
    setProjectileMode(state.tuning.projectileMode);
    updateRangedEmphasis();
    deckArtSignature = null;
    syncReleaseSlots();
    renderDefences();
    deckEditor.render();
  }

  /**
   * The old "own attack with another gesture" card as a ReleaseAttack: the same projectile (or melee
   * box) and numbers the own attack has, at every release of that gesture.
   */
  function attackFromVariant(v) {
    const t = state.tuning;
    const gesture = (v.gesture || '').trim();
    const attack = newAttack(catalog, 'ReleaseAttack', state.deck.map((a) => a.id));
    let id = `Propio${gesture.replace(/[^A-Za-z0-9_-]/g, '')}`, n = 2;
    while (state.deck.some((a) => a.id === id)) id = `Propio${gesture.replace(/[^A-Za-z0-9_-]/g, '')}${n++}`;
    attack.id = id;
    attack.open = false;
    const rig = linkedEntry()?.draft?.rig || {};
    const found = bodyGestures().find((g) => g.name === gesture);
    Object.assign(attack.common, {
      gesture, weight: v.weight > 0 ? v.weight : 1, cooldownInAttacks: 0,
      telegraph: rig.windup ?? 0.4, recovery: found?.recover ?? rig.recover ?? 0.45,
      damage: t.attackDamage, knockbackMultiplier: t.attackKnockbackMultiplier ?? 1,
      accent: t.projectileTint || attack.common.accent,
    });
    ['weight', 'cooldownInAttacks', 'telegraph', 'recovery', 'damage', 'knockbackMultiplier', 'accent'].forEach((k) => { attack.edited[k] = true; });

    const field = releaseListOf(attack);
    const ranged = isRangedNow();
    const spec = t.projectileSpec || {};
    const shot = () => {
      const s = defaultsOfFields(field.fields);
      s.kind = ranged ? 'Projectile' : 'Strike';
      s.aim = t.aimAtTarget === false ? 'Forward' : 'Frozen';
      // The own projectile, when the form has one configured (its art is a library entry).
      if (t.projectileMode === 'inline') {
        Object.keys(s.projectile).forEach((k) => { if (k !== 'prefab' && spec[k] !== undefined) s.projectile[k] = JSON.parse(JSON.stringify(spec[k])); });
        s.projectile.prefab = spec.libraryId || '';
      }
      if (t.meleeHitboxSize) s.strikeSize = { ...t.meleeHitboxSize };
      if (t.meleeHitboxOffset) s.strikeOffset = { ...t.meleeHitboxOffset };
      return s;
    };
    if (field) attack.params[field.name] = Array.from({ length: Math.max(1, found ? effectiveReleases(found).length : 1) }, shot);
    attack.deck = { minRange: v.minRange ?? 0, maxRange: v.maxRange ?? 0, targetHeight: v.targetHeight || 'Any', defenceOnly: false };
    return attack;
  }

  /**
   * The behaviour already saved for a library entry, loaded into the form: an enemy record's own
   * config, a rig's `enemyConfig`, or — for a rig made from a template and never configured here —
   * the template's suggested behaviour. No saved behaviour = the form is left as it is.
   */
  async function loadSavedBehaviour(entry) {
    if (!entry) return;
    await catalogReady;
    const saved = entry.kind === 'rig'
      ? entry.enemyConfig || suggestedEnemyConfig(adoptRigState(entry.draft))
      : entry.config;
    if (!saved || saved.kind === 'boss' || !saved.tuning) return;
    adoptConfig(saved);
    summaryEl.insertAdjacentHTML('afterbegin',
      `<p class="efHint">Cargado el comportamiento guardado de "${escapeHtml(entry.enemyName)}".</p>`);
  }

  /** Once a draft is linked to a library entry, every export also persists the config onto it —
   *  this is what lets the Biblioteca tab and a later "editar en Enemy Creator" session offer
   *  "exportar combinado" without the user re-filling the form. Never fatal: the download itself
   *  already happened by the time this runs. */
  async function persistConfigToLibraryIfLinked(exportObj) {
    if (state.artMode !== 'library' || !state.artLibraryId) return;
    const linked = libraryEntriesCache.find((e) => e.id === state.artLibraryId);
    // A rig record's own `config` is its rig-config: the behaviour goes next to it.
    const patch = linked?.kind === 'rig' ? { enemyConfig: exportObj } : { config: exportObj };
    try { await updateEnemy(state.artLibraryId, patch); }
    catch (err) { console.warn('[enemy-form] no se pudo guardar el config en la biblioteca:', err); }
  }

  exportBtn.addEventListener('click', async () => {
    const exportObj = buildEnemyExport(state, exportContext()).doc;
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

      const exportObj = buildEnemyExport(state, exportContext()).doc;
      if (entry.kind === 'rig') return;   // a part-based enemy is exported whole from Enemy Studio
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
    // Editing THAT entry: its name, and whatever behaviour it already has saved.
    if (enemyName) {
      state.enemyName = enemyName;
      nameField.input.value = enemyName;
    }
    setArtMode('library');
    await loadSavedBehaviour(libraryEntriesCache.find((e) => e.id === id));
    deckEditor.refreshGestures();
    renderDefences();
    refresh();
  }

  /** Called when the tab is shown: the library (art entries, gestures of a rig) may have changed. */
  async function refreshArtLibrary() {
    await refreshArtLibraryOptions();
    await catalogReady;
    await loadArtEntries();
    deckEditor.render();
    renderDefences();
    refresh();
  }

  // ============================================================ the deck seen from a gesture (🧬 Enemy Studio)
  //
  // The Studio edits a gesture and the attacks that use it side by side: the timeline's gesture card
  // shows, for each attack launched with that gesture, one slot per release. It is this same deck —
  // the same state, the same field renderer — drawn a second time in another place.

  let previewAttackId = null;   // which of the attacks of a gesture the Studio's test stage fires

  /** Enemy Studio: where the live gestures come from (null = back to the linked record's saved ones). */
  function setGestureSource(source) {
    gestureSource = source || null;
  }

  /** Enemy Studio: called when the cards are rebuilt, to redraw their mirror. */
  function setDeckMirror(callback, onValue = null) {
    onDeckRendered = callback || null;
    onDeckChanged = onValue || null;
  }

  /** The gestures of the body changed (a release added, a gesture renamed): slots and dropdowns follow. */
  function gesturesChanged() {
    if (!deckReady) return;
    syncReleaseSlots();
    deckEditor.render();
    renderDefences();
    refresh();
  }

  const attacksUsing = (gestureName) => state.deck.filter((a) => (a.common.gesture || '').trim() === gestureName);

  /** The attack of that gesture the test stage fires: the one picked, else the first. */
  function previewAttackOf(gestureName) {
    const using = attacksUsing(gestureName);
    return using.find((a) => a.id === previewAttackId) || using[0] || null;
  }

  /**
   * What the test stage needs to fire a gesture's attack: `{ id, telegraph, accent, shots, artOptions }`
   * (`shots` = its per-release list, null for an attack that is not made of them), or null.
   */
  function gestureShots(gestureName) {
    const attack = previewAttackOf(gestureName);
    if (!attack) return null;
    const field = releaseListOf(attack);
    return {
      id: attack.id,
      telegraph: attack.common.telegraph,
      accent: attack.common.accent,
      shots: field ? attack.params[field.name] || [] : null,
      artOptions: state.attackArt,
    };
  }

  /** The warning of the attack the stage fires with that gesture, set from the timeline (its "Aviso"). */
  function setGestureTelegraph(gestureName, seconds) {
    const attack = previewAttackOf(gestureName);
    if (!attack) return;
    attack.common.telegraph = seconds;
    (attack.edited || (attack.edited = {})).telegraph = true;
    state.deckTouched = true;
    refresh();
  }

  /**
   * The attacks launched with `gestureName`, editable in place: per attack, what leaves at each
   * release, and its three main numbers. `rerender` redraws the host after a structural change.
   */
  function renderGestureAttacks(host, gestureName, rerender) {
    const mk = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text !== undefined) e.textContent = text; return e; };
    const box = mk('div', 'rigFxBox gaBox');
    const using = attacksUsing(gestureName);
    box.appendChild(mk('p', 'rigGroupTitle', `⚔ Ataques que usan este gesto (${using.length})`));
    if (!catalog) { box.appendChild(mk('p', 'hint', 'Falta el catálogo de ataques de Unity (modules/boss-catalog.js).')); host.appendChild(box); return; }
    if (!using.length) box.appendChild(mk('p', 'hint', 'Ninguno todavía. Un ataque hecho con este gesto lanza algo en cada ◆ suelta: un proyectil, un abanico, un anillo, un golpe… o nada.'));

    const chosen = previewAttackOf(gestureName);
    using.forEach((attack) => {
      const typeEntry = catalog.attacks.find((t) => t.type === attack.type);
      const card = mk('div', `bcCard gaCard${attack === chosen ? ' chosen' : ''}`);
      const head = mk('div', 'bcCardHead');
      head.appendChild(mk('span', 'bcCardTitle', `${attack.id} · ${attackLabel(attack.type, typeEntry)}`));
      if (using.length > 1) {
        const pick = mk('button', `small${attack === chosen ? ' active' : ''}`, attack === chosen ? '▶ se prueba éste' : '▶ probar éste');
        pick.dataset.nodirty = '1';
        pick.title = 'Cuál de los ataques de este gesto dispara "▶ Probar".';
        pick.onclick = () => { previewAttackId = attack.id; rerender(); };
        head.appendChild(pick);
      }
      card.appendChild(head);

      const field = releaseListOf(attack);
      const ctx = { edited: attack.edited || (attack.edited = {}), attack };
      if (field) {
        const slots = mk('div', 'bcParams');
        deckEditor.renderFields(slots, [{ ...field, header: undefined }], attack.params, ctx);
        card.appendChild(slots);
      } else {
        card.appendChild(mk('p', 'hint', 'Este tipo de ataque sale en la primera suelta del gesto; sus parámetros están en el paso 4 (Comportamiento ▸ Ataques).'));
      }

      const numbers = mk('div', 'bcGrid');
      const main = ['damage', 'telegraph', 'recovery'];
      deckEditor.renderFields(numbers, (catalog.common || []).filter((f) => main.includes(f.name)).map((f) => ({ ...f, header: undefined })), attack.common, ctx);
      card.appendChild(numbers);
      box.appendChild(card);
    });

    const add = mk('button', 'small primary', '＋ Ataque con este gesto');
    add.title = 'Un "Disparo / golpe (por sueltas del gesto)" nuevo que usa este gesto. Rango, peso y enfriamiento, en el paso 4 (Comportamiento ▸ Ataques).';
    add.onclick = () => {
      const attack = newAttack(catalog, 'ReleaseAttack', state.deck.map((a) => a.id));
      attack.common.gesture = gestureName;
      attack.open = false;
      attack.deck = { minRange: 0, maxRange: 0, targetHeight: 'Any', defenceOnly: false };
      state.deck.push(attack);
      state.deckTouched = true;
      previewAttackId = attack.id;
      gesturesChanged();   // slots to its releases, cards redrawn (→ the mirror too)
    };
    box.appendChild(add);
    host.appendChild(box);
  }

  /**
   * A deep link from Unity ("✎ web" on a card of the deck): opens the `index`-th card of the draw and
   * makes it the one the test stage fires. Returns `{ id, gesture }`, or null if there is no such card.
   */
  function focusAttack(index) {
    const attack = state.deck.filter((a) => !a.deck?.defenceOnly)[index];
    if (!attack) return null;
    state.deck.forEach((a) => { a.open = a === attack; });
    previewAttackId = attack.id;
    deckEditor.render();
    return { id: attack.id, gesture: (attack.common.gesture || '').trim() };
  }

  /** The library entry this form is editing the behaviour of, or null. */
  function linkedId() {
    return state.artMode === 'library' ? state.artLibraryId || null : null;
  }

  /**
   * Saves the form onto its linked entry WITHOUT downloading anything (🧬 Enemy Studio: the behaviour
   * step is saved when it is left, and before the body is saved / exported).
   */
  async function persistLinked() {
    if (!linkedId()) return null;
    const exportObj = buildEnemyExport(state, exportContext()).doc;
    await persistConfigToLibraryIfLinked(exportObj);
    return exportObj;
  }

  /**
   * The 🧩 tab is being shown: if the form is still on a part-based enemy (Enemy Studio left it
   * there), let go of it — that behaviour is edited in the Studio. The numbers stay on screen as a
   * starting point; the art picker goes back to "choose one".
   */
  function unlinkRig() {
    const linked = libraryEntriesCache.find((e) => e.id === state.artLibraryId);
    if (state.artMode !== 'library' || linked?.kind !== 'rig') return;
    state.artLibraryId = null;
    artLibrarySelect.value = '';
    updateCombinedExportAvailability();
    refresh();
  }

  /** The linked body was renamed outside the form. */
  function setEnemyName(name) {
    if (!name || state.enemyName === name) return;
    state.enemyName = name;
    nameField.input.value = name;
    refresh();
  }

  return { refreshLaneInfo, refreshArtLibrary, linkLibraryEntry, linkedId, persistLinked, setEnemyName, unlinkRig,
    setGestureSource, setDeckMirror, gesturesChanged, gestureShots, setGestureTelegraph, renderGestureAttacks, focusAttack };
}
