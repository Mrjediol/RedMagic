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
import { createDefaultEnemyConfig, createDefaultProjectileSpec, PROJECTILE_AIMING_FIELDS } from './enemy-defaults.js';
import { buildEnemyExport } from './enemy-export.js';
import { createAttackEditor } from './attack-cards.js';
import { adoptAttacksFromConfig } from './attack-export.js';
import { createArtOptions } from './boss-defaults.js';
import { framedLanes } from './boss-export.js';
import { ART_KIND_NOTES } from './boss-catalog-notes.js';
import { suggestedEnemyConfig } from './rig-templates.js';
import { loadEnemyConfigValidator, validateEnemyConfig } from './enemy-config-schema.js';
import { downloadBlob } from './export-manifest.js';
import { listEntries, getEnemy, updateEnemy } from './enemy-library.js';
import { buildCombinedBundle } from './enemy-bundle.js';
import { buildRigBundle } from './rig-export.js';
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
  // Plus part-based enemies (🦴 rig entries, enemy kind): their body is the rig, this form only
  // authors their behaviour, which is saved on the rig record as `enemyConfig`.
  async function refreshArtLibraryOptions() {
    const rigs = (await listEntries('rig')).filter((e) => e.draft && e.draft.kind !== 'boss');
    libraryEntriesCache = [...(await listEntries('enemy')), ...rigs];
    if (libraryEntriesCache.length === 0) {
      artLibrarySelect.innerHTML = '<option value="">(sin enemigos guardados — usa la pestaña Sprites)</option>';
      return;
    }
    artLibrarySelect.innerHTML = ['<option value="">Selecciona un enemigo guardado…</option>']
      .concat(libraryEntriesCache.map((e) =>
        `<option value="${e.id}" ${e.id === state.artLibraryId ? 'selected' : ''}>${e.kind === 'rig' ? '🦴 ' : ''}${e.enemyName}${e.kind === 'rig' ? ' (por partes)' : ''} — ${new Date(e.updatedAt).toLocaleString()}</option>`
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
    refreshVariantGestures();
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
  deckSection.className = 'efSection';
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

  // --- el mismo ataque propio, lanzado con otro gesto (varias formas de hacer el mismo ataque)
  const variantsRoot = document.createElement('div');
  builtInCard.appendChild(variantsRoot);

  function renderVariants() {
    variantsRoot.innerHTML = '';
    const list = state.builtInVariants;
    variantsRoot.appendChild(Object.assign(document.createElement('p'), {
      className: 'efHint',
      textContent: 'Variantes con otro gesto: el MISMO ataque propio (mismo proyectil / golpe y mismos números) lanzado con un gesto distinto del cuerpo. '
        + 'Se sortean con su peso junto al resto.',
    }));
    list.forEach((v, index) => {
      const card = document.createElement('div');
      card.className = 'bcCard';
      const head = document.createElement('div');
      head.className = 'bcCardHead';
      const title = document.createElement('span');
      title.className = 'bcCardTitle';
      title.textContent = `Ataque propio · variante ${index + 1}`;
      const del = document.createElement('button');
      del.className = 'small danger';
      del.textContent = '✕';
      del.onclick = () => { list.splice(index, 1); state.deckTouched = true; renderVariants(); refresh(); };
      head.append(title, del);
      card.appendChild(head);

      const gestureRow = document.createElement('div');
      gestureRow.className = 'efRow';
      const label = document.createElement('label');
      label.textContent = 'Gesto';
      const select = document.createElement('select');
      const fill = () => {
        const names = bodyGestureNames();
        const opts = [''].concat(names);
        if (v.gesture && !names.includes(v.gesture)) opts.push(v.gesture);
        select.innerHTML = '';
        opts.forEach((n) => {
          const o = document.createElement('option');
          o.value = n;
          o.textContent = n === '' ? (names.length ? '(el ataque normal del cuerpo)' : '(elige arriba el arte del enemigo)') : (names.includes(n) ? n : `${n} (no existe)`);
          select.appendChild(o);
        });
        select.value = v.gesture || '';
      };
      fill();
      select.onchange = () => { v.gesture = select.value; state.deckTouched = true; refresh(); };
      select.dataset.variantGesture = '1';
      select.refill = fill;
      gestureRow.append(label, select);
      card.appendChild(gestureRow);

      const grid = document.createElement('div');
      grid.className = 'bcGrid';
      const num = (labelText, key, min, hint) => grid.appendChild(F.numberField({
        label: labelText, value: v[key], min, hint,
        onChange: (x) => { v[key] = x; state.deckTouched = true; refresh(); },
      }).row);
      num('Peso en el sorteo', 'weight', 0.01);
      num('Distancia mínima (u)', 'minRange', 0, 'Más cerca que esto no sale. 0 = sin mínimo.');
      num('Distancia máxima (u)', 'maxRange', 0, '0 = el "Rango de ataque" del enemigo.');
      grid.appendChild(F.enumField({
        label: 'Altura del objetivo', value: v.targetHeight,
        options: [{ value: 'Any', label: 'Cualquiera' }, { value: 'TargetAbove', label: 'Sólo si está por encima' }, { value: 'TargetBelow', label: 'Sólo si está por debajo' }],
        onChange: (x) => { v.targetHeight = x; state.deckTouched = true; refresh(); },
      }).row);
      card.appendChild(grid);
      variantsRoot.appendChild(card);
    });

    const add = document.createElement('button');
    add.className = 'small primary';
    add.textContent = '＋ Repetir el ataque propio con otro gesto';
    add.onclick = () => {
      list.push({ gesture: '', weight: 1, minRange: 0, maxRange: 0, targetHeight: 'Any' });
      state.deckTouched = true;
      renderVariants();
      refresh();
    };
    variantsRoot.appendChild(add);
  }

  /** The body's gestures changed (another art picked): refill the variants' gesture dropdowns. */
  function refreshVariantGestures() {
    variantsRoot.querySelectorAll('select[data-variant-gesture]').forEach((s) => s.refill && s.refill());
  }

  function describeBuiltIn() {
    const t = state.tuning;
    const what = t.selfDestruct ? `explota (radio ${t.explosionRadius})`
      : isRangedNow() ? 'dispara su proyectil' : 'golpea de cerca (caja de melé)';
    builtInSummary.textContent = `${what} · daño ${t.attackDamage} · a ${t.attackRange} u · gesto: el ataque del cuerpo (Attack / windup del rig).`;
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

  /** Gestures the body can play: a rig's named gestures, or a sprite sheet's animation names. */
  function bodyGestureNames() {
    const entry = linkedEntry();
    if (!entry) return [];
    if (entry.kind === 'rig') return (entry.draft?.gestures || []).map((g) => g.name).filter(Boolean);
    return framedLanes(entry).map((l) => l.name);
  }

  const deckEditor = createAttackEditor({
    root: deckRoot,
    getCatalog: () => catalog,
    getAttacks: () => state.deck,
    getEntries: () => artEntries,
    gestureNames: bodyGestureNames,
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
      attack.deck = { minRange: 0, maxRange: 0, targetHeight: 'Any', ...(attack.deck || {}) };
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
    onChange: () => refresh(),
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

  // ============================================================ orden final: lo importante a mano

  // Arriba: el enemigo (nombre y cuerpo), su tipo, su proyectil y sus ataques. Debajo, plegado, el resto.
  const quick = document.createElement('div');
  quick.className = 'efSection';
  quick.innerHTML = '<h2>Tipo, proyectil y ataques</h2>';
  quick.append(tipoGroup, groupEls['Ataque · a distancia']);
  formRoot.insertBefore(quick, tuningSection);
  formRoot.insertBefore(deckSection, tuningSection);
  formRoot.append(presenceSection, projectileArtSection);   // lo menos usado, al final y plegado

  // Lo de importar / generar, fuera del paso: vida y tier junto a las estadísticas.
  const importOptions = addGroup(topSection, 'Opciones de importación (pivote, carpeta del prefab)', false);
  importOptions.append(anchorField.row, prefabFolderField.row);
  groupEls['Vida'].appendChild(tierField.row);

  renderVariants();
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
    const { projectile, attacks: deckJson, ...flat } = t;

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
    state.builtInVariants = (Array.isArray(deckJson) ? deckJson : []).filter((e) => e.builtIn).map((e) => ({
      gesture: e.gesture || '', weight: e.weight ?? 1, minRange: e.minRange ?? 0, maxRange: e.maxRange ?? 0, targetHeight: e.targetHeight || 'Any',
    }));
    state.deck = (Array.isArray(deckJson) ? deckJson : []).filter((e) => !e.builtIn).map((e) => {
      const attack = adopted.byId[e.attack];
      if (!attack) return null;   // a card that points at a Unity asset: only editable there
      attack.deck = { minRange: e.minRange ?? 0, maxRange: e.maxRange ?? 0, targetHeight: e.targetHeight || 'Any' };
      return attack;
    }).filter(Boolean);
    state.attackArt = adopted.artOptions;
    state.deckTouched = Array.isArray(deckJson);

    syncers.forEach((sync) => sync());
    setProjectileMode(state.tuning.projectileMode);
    updateRangedEmphasis();
    deckArtSignature = null;
    renderVariants();
    deckEditor.render();
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
      if (entry.kind === 'rig') {
        // Part-based: the rig's own .zip (parts + rig-config) with this behaviour as enemy-config.json.
        const all = await listEntries(null);
        const blob = await buildRigBundle(adoptRigState(entry.draft), { enemyConfig: exportObj, entriesById: new Map(all.map((e) => [e.id, e])) });
        downloadBlob(blob, `${entry.enemyName.replace(/[^A-Za-z0-9_]/g, '_')}.rig.zip`);
        await persistConfigToLibraryIfLinked(exportObj);
        summaryEl.insertAdjacentHTML('afterbegin', '<p class="efSummaryOk">Exportado el .zip del rig: en Unity, Tools ▸ Web ▸ Rig ▸ Importar rig.</p>');
        return;
      }
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
    refreshVariantGestures();
    refresh();
  }

  /** Called when the tab is shown: the library (art entries, gestures of a rig) may have changed. */
  async function refreshArtLibrary() {
    await refreshArtLibraryOptions();
    await catalogReady;
    await loadArtEntries();
    deckEditor.render();
    refreshVariantGestures();
    refresh();
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

  /** The linked body was renamed outside the form. */
  function setEnemyName(name) {
    if (!name || state.enemyName === name) return;
    state.enemyName = name;
    nameField.input.value = name;
    refresh();
  }

  return { refreshLaneInfo, refreshArtLibrary, linkLibraryEntry, linkedId, persistLinked, setEnemyName };
}
