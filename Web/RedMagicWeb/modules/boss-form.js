// modules/boss-form.js
// -----------------------------------------------------------------------------
// The Boss Creator tab: assembles a BossConfig (docs/schemas/boss-config.schema.json) — a base
// enemy, a library of attacks, and the phases that draw from it — and saves it to the shared
// library as a `kind:'boss'` record.
//
// WEB-ONLY, ON PURPOSE. Nothing imports this into Unity yet: the boss importer is future work,
// until boss 2 has art. What ships today is the data model, the form and the storage, so that
// filling one in later is fast. Everything here is therefore shaped to match what BossDefinition /
// BossPhase / BossAttack ALREADY are (which is what the schema mirrors), never to match an
// importer that does not exist — see boss-defaults.js's ATTACK_SLOTS note.
//
// WHY IT DOESN'T LOOK LIKE enemy-form.js. The other two creator tabs author a FLAT document and
// are table-driven (one row per schema field). This one authors two nested LISTS whose lengths the
// user controls, so the form is two card lists rebuilt from state on every structural change
// (`renderAttacks()` / `renderPhases()`) with the generic widgets from enemy-form-fields.js inside
// each card. Scalar edits mutate state in place and only refresh the preview; add/remove/reorder
// rebuilds the list — a full rebuild on every keystroke would lose focus mid-typing.

import * as F from './enemy-form-fields.js';
import {
  ATTACK_SLOTS, createDefaultBossConfig, createDefaultAttack, createDefaultPhase,
} from './boss-defaults.js';
import { buildBossExportObject } from './boss-export.js';
import { loadValidator, validateConfig } from './config-schema.js';
import { downloadBlob } from './export-manifest.js';
import { listEntries, getEnemy, saveEnemy, updateEnemy } from './enemy-library.js';

export function initBossCreator({ formRoot, previewEl, summaryEl, exportBtn, saveBtn, onSaved }) {
  let state = createDefaultBossConfig();
  let validate = null;

  // The library record this draft is stored as, or null until it is saved once. Same role as
  // app.js's `currentLibraryId` for the Sprites tab: re-saving updates in place instead of
  // duplicating the boss.
  let currentBossId = null;

  loadValidator('boss')
    .then((v) => { validate = v; refresh(); })
    .catch((err) => {
      summaryEl.innerHTML = `<p class="efSummaryError">No se pudo cargar el validador de esquema: ${err.message}</p>`;
    });

  // ============================================================ identidad + base

  const topSection = document.createElement('div');
  topSection.className = 'efSection';
  topSection.innerHTML = '<h2>Jefe</h2>';

  const nameField = F.textField({
    label: 'Nombre (displayName)', value: state.displayName,
    placeholder: 'ej. Ent Cristalino',
    hint: 'El nombre que sale en la barra de vida del jefe.',
    onChange: (v) => { state.displayName = v; refresh(); },
  });
  topSection.appendChild(nameField.row);

  const titleField = F.textField({
    label: 'Epíteto (title)', value: state.title,
    placeholder: 'ej. Guardián del Bosque',
    hint: 'La línea pequeña bajo el nombre. Opcional.',
    onChange: (v) => { state.title = v; refresh(); },
  });
  topSection.appendChild(titleField.row);

  const descriptionField = F.textField({
    label: 'Descripción (description)', value: state.description,
    hint: 'Nota de diseño. Opcional, sólo se ve en el inspector de Unity.',
    onChange: (v) => { state.description = v; refresh(); },
  });
  topSection.appendChild(descriptionField.row);

  // --- base: the library enemy this boss is built on. Enemy-kind entries only: that is the one
  // kind carrying body animation lanes in EnemyAnimation.cs's vocabulary and, once exported from
  // Enemy Creator at least once, an EnemyConfig — the two things a boss prefab will need.
  const baseRow = document.createElement('div');
  baseRow.className = 'efRow';
  const baseLabel = document.createElement('label');
  baseLabel.textContent = 'Enemigo base (base.enemyLibraryId)';
  baseRow.appendChild(baseLabel);
  const baseSelect = document.createElement('select');
  baseRow.appendChild(baseSelect);
  const baseHint = document.createElement('p');
  baseHint.className = 'efHint';
  baseHint.textContent = 'Un enemigo guardado en la biblioteca: su hoja es el cuerpo del jefe y su '
    + 'EnemyConfig el punto de partida de sus stats. Se guarda como referencia por id, no como copia, '
    + 'así que reexportar el enemigo reexporta el arte del jefe.';
  baseRow.appendChild(baseHint);
  topSection.appendChild(baseRow);

  let baseEntriesCache = [];

  async function refreshBaseOptions() {
    baseEntriesCache = await listEntries('enemy');
    if (baseEntriesCache.length === 0) {
      baseSelect.innerHTML = '<option value="">(no hay enemigos guardados — créalos en la pestaña Sprites)</option>';
      return;
    }
    baseSelect.innerHTML = ['<option value="">Selecciona un enemigo base…</option>']
      .concat(baseEntriesCache.map((e) =>
        `<option value="${e.id}">${e.enemyName} — ${new Date(e.updatedAt).toLocaleString()}</option>`
      )).join('');
    baseSelect.value = state.baseEnemyLibraryId || '';
  }

  baseSelect.addEventListener('change', () => {
    state.baseEnemyLibraryId = baseSelect.value || '';
    const picked = baseEntriesCache.find((e) => e.id === state.baseEnemyLibraryId);
    // Stored alongside the id purely so the config stays readable (and a dangling reference
    // diagnosable) if that entry is later renamed or deleted — see the schema's `base` $comment.
    state.baseEnemyName = picked ? picked.enemyName : '';
    if (picked && !state.displayName.trim()) {
      state.displayName = picked.enemyName;
      nameField.input.value = picked.enemyName;
    }
    refresh();
  });

  formRoot.appendChild(topSection);

  // ============================================================ ataques

  const attacksSection = document.createElement('div');
  attacksSection.className = 'efSection';
  attacksSection.innerHTML = '<h2>Ataques</h2>';

  const attacksIntro = document.createElement('p');
  attacksIntro.className = 'efHint';
  attacksIntro.innerHTML = 'Una biblioteca de ataques con un <strong>id local</strong> cada uno; las fases '
    + 'de abajo referencian esos ids. Es una biblioteca y no ataques sueltos por fase porque en Unity '
    + 'el mazo de una fase son <em>referencias</em> a assets BossAttack compartidos: el mismo ataque '
    + 'puede estar en varias fases, o en ninguna.';
  attacksSection.appendChild(attacksIntro);

  const attacksList = document.createElement('div');
  attacksList.className = 'bcList';
  attacksSection.appendChild(attacksList);

  const addAttackBtn = document.createElement('button');
  addAttackBtn.className = 'small';
  addAttackBtn.textContent = '＋ Añadir ataque';
  addAttackBtn.onclick = () => {
    state.attacks.push(createDefaultAttack());
    renderAttacks(); renderPhases(); refresh();
  };
  attacksSection.appendChild(addAttackBtn);

  formRoot.appendChild(attacksSection);

  // Projectile-kind entries, listed once and reused by every attack card's art picker (rather than
  // one library round-trip per card on every re-render).
  let projectileEntriesCache = [];

  async function refreshProjectileOptions() {
    projectileEntriesCache = await listEntries('projectile');
    renderAttacks();
  }

  function renderAttacks() {
    attacksList.innerHTML = '';

    if (state.attacks.length === 0) {
      attacksList.innerHTML = '<p class="efHint">Sin ataques. Un BossConfig necesita al menos uno.</p>';
      return;
    }

    state.attacks.forEach((attack, index) => {
      const card = document.createElement('div');
      card.className = 'bcCard';

      const head = document.createElement('div');
      head.className = 'bcCardHead';
      const title = document.createElement('span');
      title.className = 'bcCardTitle';
      title.textContent = `${attack.id} · ${attack.type}`;
      head.appendChild(title);

      const del = document.createElement('button');
      del.className = 'small danger';
      del.textContent = '✕';
      del.title = 'Eliminar este ataque (y quitarlo de los mazos que lo usen)';
      del.onclick = () => {
        state.attacks.splice(index, 1);
        // The decks hold ids, so removing the attack has to remove it from every phase too —
        // buildBossExportObject filters dangling ids anyway, but leaving them in the live state
        // would make a re-added id with the same name silently reappear in old decks.
        state.phases.forEach((p) => { p.attacks = p.attacks.filter((id) => id !== attack.id); });
        renderAttacks(); renderPhases(); refresh();
      };
      head.appendChild(del);
      card.appendChild(head);

      card.appendChild(F.textField({
        label: 'Id local (lo referencian las fases)', value: attack.id,
        onChange: (v) => {
          const old = attack.id;
          attack.id = v;
          // Keep every deck pointing at this attack while it is being renamed, instead of the
          // reference silently going dangling on the first keystroke.
          state.phases.forEach((p) => { p.attacks = p.attacks.map((id) => (id === old ? v : id)); });
          title.textContent = `${attack.id} · ${attack.type}`;
          refresh();
        },
      }).row);

      const slot = ATTACK_SLOTS.find((s) => s.type === attack.type);
      card.appendChild(F.enumField({
        label: 'Arquetipo (type)', value: attack.type,
        options: ATTACK_SLOTS.map((s) => ({ value: s.type, label: s.label })),
        hint: slot ? `Pregunta al jugador: ${slot.question}` : '',
        onChange: (v) => { attack.type = v; renderAttacks(); refresh(); },
      }).row);

      card.appendChild(F.textField({
        label: 'Nombre visible (displayName)', value: attack.displayName,
        hint: 'Vacío = el nombre del asset.',
        onChange: (v) => { attack.displayName = v; refresh(); },
      }).row);

      const grid = document.createElement('div');
      grid.className = 'bcGrid';
      [
        ['telegraph', 'Telegrafía (s)', { min: 0 }, 'El aviso. Es lo que lo hace esquivable.'],
        ['recovery', 'Recuperación (s)', { min: 0 }, 'La ventana de DPS del jugador.'],
        ['cooldownSeconds', 'Enfriamiento (s)', { min: 0 }, '0 = sin límite de tiempo.'],
        ['cooldownInAttacks', 'Enfriamiento (ataques)', { min: 0, step: 1 }, '1 = nunca dos veces seguidas.'],
        ['minPhase', 'Fase mínima', { min: 1, step: 1 }, 'Por debajo no se sortea nunca.'],
        ['damage', 'Daño', { min: 0 }, 'Lo escala damageScale de la fase.'],
      ].forEach(([key, label, opts, hint]) => {
        grid.appendChild(F.numberField({
          label, value: attack[key], min: opts.min, step: opts.step, hint,
          onChange: (v) => { attack[key] = opts.step === 1 ? Math.round(v) : v; refresh(); },
        }).row);
      });
      card.appendChild(grid);

      // Only the archetypes that actually carry an embedded ProjectileSpec get the art picker —
      // on the others the key would land in `params.projectile` and the importer would report a
      // field it cannot resolve, which is worse than not offering it.
      if (slot && slot.projectile) {
        const projRow = document.createElement('div');
        projRow.className = 'efRow';
        const projLabel = document.createElement('label');
        projLabel.textContent = 'Proyectil desde biblioteca (params.projectile.libraryId)';
        projRow.appendChild(projLabel);

        const projSelect = document.createElement('select');
        projSelect.innerHTML = '<option value="">(ninguno)</option>'
          + projectileEntriesCache.map((e) =>
            `<option value="${e.id}" ${e.id === attack.projectileLibraryId ? 'selected' : ''}>${e.enemyName}</option>`
          ).join('');
        projSelect.addEventListener('change', () => {
          attack.projectileLibraryId = projSelect.value;
          refresh();
        });
        projRow.appendChild(projSelect);

        const projHint = document.createElement('p');
        projHint.className = 'efHint';
        projHint.textContent = projectileEntriesCache.length === 0
          ? 'No hay proyectiles guardados — créalos en Sprites y afínalos en Proyectil/VFX Creator.'
          : 'Un proyectil de la biblioteca. Usa exactamente la misma vía que el proyectil de un '
            + 'enemigo, así que Unity lo construye al importar sin haberlo importado antes.';
        projRow.appendChild(projHint);
        card.appendChild(projRow);
      }

      attacksList.appendChild(card);
    });
  }

  // ============================================================ fases

  const phasesSection = document.createElement('div');
  phasesSection.className = 'efSection';
  phasesSection.innerHTML = '<h2>Fases</h2>';

  const phasesIntro = document.createElement('p');
  phasesIntro.className = 'efHint';
  phasesIntro.innerHTML = 'En orden, de más vida a menos. La primera fase <strong>tiene que</strong> empezar '
    + 'en 1: <code>BossDefinition.OnValidate</code> lo fuerza en Unity, así que el esquema rechaza cualquier '
    + 'otro valor ahí en vez de dejar que el config prometa algo que el asset no va a conservar.';
  phasesSection.appendChild(phasesIntro);

  const phasesList = document.createElement('div');
  phasesList.className = 'bcList';
  phasesSection.appendChild(phasesList);

  const addPhaseBtn = document.createElement('button');
  addPhaseBtn.className = 'small';
  addPhaseBtn.textContent = '＋ Añadir fase';
  addPhaseBtn.onclick = () => {
    state.phases.push(createDefaultPhase(state.phases.length));
    renderPhases(); refresh();
  };
  phasesSection.appendChild(addPhaseBtn);

  formRoot.appendChild(phasesSection);

  function renderPhases() {
    phasesList.innerHTML = '';

    state.phases.forEach((phase, index) => {
      const card = document.createElement('div');
      card.className = 'bcCard';

      const head = document.createElement('div');
      head.className = 'bcCardHead';
      const title = document.createElement('span');
      title.className = 'bcCardTitle';
      title.textContent = `${index + 1}. ${phase.displayName}`;
      head.appendChild(title);

      // The first phase is structural (it is the fight's starting state), so it has no delete
      // button rather than a delete that produces a config the schema rejects.
      if (index > 0) {
        const del = document.createElement('button');
        del.className = 'small danger';
        del.textContent = '✕';
        del.onclick = () => { state.phases.splice(index, 1); renderPhases(); refresh(); };
        head.appendChild(del);
      }
      card.appendChild(head);

      card.appendChild(F.textField({
        label: 'Nombre (displayName)', value: phase.displayName,
        onChange: (v) => { phase.displayName = v; title.textContent = `${index + 1}. ${v}`; refresh(); },
      }).row);

      const grid = document.createElement('div');
      grid.className = 'bcGrid';

      const healthField = F.numberField({
        label: 'Entra por debajo de (vida 0-1)', value: phase.startsAtHealth, min: 0, max: 1,
        hint: index === 0 ? 'La primera fase siempre es 1 (Unity lo fuerza).' : 'ej. 0.5 = a media vida.',
        onChange: (v) => { phase.startsAtHealth = v; refresh(); },
      });
      if (index === 0) healthField.input.disabled = true;
      grid.appendChild(healthField.row);

      [
        ['damageScale', 'Escala de daño hecho', { min: 0.01 }, 'Multiplica el daño de cada ataque de la fase.'],
        ['damageTakenMultiplier', 'Daño recibido ×', { min: 0.01 }, 'Menos de 1 = acorazado. Nunca 0.'],
        ['speedScale', 'Ritmo ×', { min: 0.1 }, '1.3 = todo un 30% más rápido.'],
        ['transitionSeconds', 'Transición (s)', { min: 0 }, 'Invulnerable al entrar; sin esto una ráfaga se salta la fase.'],
      ].forEach(([key, label, opts, hint]) => {
        grid.appendChild(F.numberField({
          label, value: phase[key], min: opts.min, hint,
          onChange: (v) => { phase[key] = v; refresh(); },
        }).row);
      });
      card.appendChild(grid);

      // The deck: a checkbox per attack in the library. A multi-select list rather than a set of
      // dropdowns because a deck is a SUBSET, and because `minPhase` already gates availability
      // on top of it — so seeing every attack with its own tick is the readable form.
      const deckRow = document.createElement('div');
      deckRow.className = 'efRow';
      const deckLabel = document.createElement('label');
      deckLabel.textContent = 'Mazo de la fase (attacks)';
      deckRow.appendChild(deckLabel);

      const deck = document.createElement('div');
      deck.className = 'bcDeck';
      if (state.attacks.length === 0) {
        deck.innerHTML = '<p class="efHint">Añade ataques arriba primero.</p>';
      } else {
        state.attacks.forEach((attack) => {
          const wrap = document.createElement('label');
          wrap.className = 'efCheckboxRow';
          const cb = document.createElement('input');
          cb.type = 'checkbox';
          cb.checked = phase.attacks.includes(attack.id);
          cb.addEventListener('change', () => {
            phase.attacks = cb.checked
              ? [...phase.attacks, attack.id]
              : phase.attacks.filter((id) => id !== attack.id);
            refresh();
          });
          wrap.appendChild(cb);
          wrap.appendChild(document.createTextNode(`${attack.id} · ${attack.type}`));
          deck.appendChild(wrap);
        });
      }
      deckRow.appendChild(deck);

      const deckHint = document.createElement('p');
      deckHint.className = 'efHint';
      deckHint.textContent = 'Un mazo vacío es legal (la fase sólo actuaría por su ataque de apertura, '
        + 'que aún no se autoriza aquí). Recuerda que "fase mínima" del ataque sigue filtrando esta lista.';
      deckRow.appendChild(deckHint);
      card.appendChild(deckRow);

      phasesList.appendChild(card);
    });
  }

  // ============================================================ live preview + validation

  function refresh() {
    const exportObj = buildBossExportObject(state);
    previewEl.textContent = JSON.stringify(exportObj, null, 2);

    if (!validate) return;

    const { valid, errors } = validateConfig(validate, exportObj);
    exportBtn.disabled = !valid;
    if (saveBtn) saveBtn.disabled = !valid;

    if (valid) {
      summaryEl.innerHTML = '<p class="efSummaryOk">✓ Válido según boss-config.schema.json</p>'
        + '<p class="efHint">Aún no hay importador de jefes en Unity: esto se guarda en la biblioteca '
        + 'y se exporta como JSON, listo para cuando lo haya.</p>';
      return;
    }

    // No per-field error setters here, unlike the flat forms: an Ajv instancePath into this
    // document is positional (/phases/1/attacks/0), so mapping it back to a widget would mean
    // registering a setter per list element on every rebuild. The summary carries the same paths.
    summaryEl.innerHTML =
      `<p class="efSummaryError">✗ ${errors.length} error${errors.length > 1 ? 'es' : ''} de validación:</p>` +
      `<ul>${errors.map((e) => `<li><code>${e.path || '(raíz)'}</code> — ${e.message}</li>`).join('')}</ul>`;
  }

  // ============================================================ exportar / guardar

  exportBtn.addEventListener('click', () => {
    const exportObj = buildBossExportObject(state);
    const name = (state.displayName.trim() || 'Boss').replace(/[^A-Za-z0-9_]/g, '_');
    const blob = new Blob([JSON.stringify(exportObj, null, 2)], { type: 'application/json' });
    downloadBlob(blob, `${name}.boss.json`);
  });

  if (saveBtn) {
    saveBtn.addEventListener('click', async () => {
      const exportObj = buildBossExportObject(state);

      // A boss record carries no `sprite` — it has no frames of its own, which is exactly what
      // `KINDS.boss.sheet === false` declares. Its card borrows the BASE enemy's thumbnail so the
      // Biblioteca grid still shows what this boss is made of instead of an empty tile.
      let thumbnail = null;
      if (state.baseEnemyLibraryId) {
        const base = await getEnemy(state.baseEnemyLibraryId);
        thumbnail = base ? base.thumbnail : null;
      }

      const record = currentBossId
        ? await updateEnemy(currentBossId, { enemyName: exportObj.displayName, thumbnail, config: exportObj })
        : await saveEnemy({
          kind: 'boss',
          enemyName: exportObj.displayName,
          sprite: null,
          thumbnail,
          config: exportObj,
        });

      currentBossId = record.id;
      summaryEl.innerHTML = `<p class="efSummaryOk">✓ Jefe "${record.enemyName}" guardado en la biblioteca `
        + `(${new Date(record.updatedAt).toLocaleTimeString()}).</p>`;
      if (onSaved) onSaved(record);
    });
  }

  // ============================================================ reopen a saved boss

  /** Rebuilds the form state from an exported BossConfig (the inverse of boss-export.js). */
  function adoptConfig(config) {
    state = createDefaultBossConfig();
    state.displayName = config.displayName || '';
    state.title = config.title || '';
    state.description = config.description || '';
    state.baseEnemyLibraryId = config.base?.enemyLibraryId || '';
    state.baseEnemyName = config.base?.enemyName || '';

    state.attacks = Object.entries(config.attacks || {}).map(([id, a]) => ({
      ...createDefaultAttack(ATTACK_SLOTS.find((s) => s.type === a.type) || ATTACK_SLOTS[0]),
      id,
      type: a.type,
      displayName: a.displayName || '',
      telegraph: a.telegraph ?? 0.8,
      recovery: a.recovery ?? 1.1,
      cooldownSeconds: a.cooldownSeconds ?? 0,
      cooldownInAttacks: a.cooldownInAttacks ?? 1,
      minPhase: a.minPhase ?? 1,
      damage: a.damage ?? 18,
      projectileLibraryId: a.params?.projectile?.libraryId || '',
    }));

    state.phases = (config.phases || []).map((p, i) => ({
      ...createDefaultPhase(i),
      displayName: p.displayName || `Fase ${i + 1}`,
      startsAtHealth: p.startsAtHealth ?? (i === 0 ? 1 : 0.5),
      attacks: [...(p.attacks || [])],
      damageScale: p.damageScale ?? 1,
      damageTakenMultiplier: p.damageTakenMultiplier ?? 1,
      speedScale: p.speedScale ?? 1,
      transitionSeconds: p.transitionSeconds ?? (i === 0 ? 0 : 1.8),
    }));

    if (state.attacks.length === 0) state.attacks = [createDefaultAttack()];
    if (state.phases.length === 0) state.phases = [createDefaultPhase(0)];

    nameField.input.value = state.displayName;
    titleField.input.value = state.title;
    descriptionField.input.value = state.description;
  }

  /** Opens a saved `kind:'boss'` record for editing (Biblioteca's "Editar en Boss Creator"). */
  async function linkLibraryEntry(id) {
    const entry = await getEnemy(id);
    if (!entry || !entry.config) return;
    currentBossId = entry.id;
    adoptConfig(entry.config);
    await refreshBaseOptions();
    baseSelect.value = state.baseEnemyLibraryId || '';
    await refreshProjectileOptions();
    renderPhases();
    refresh();
  }

  /** Re-lists both pickers — called on switching to this tab, since the library changes elsewhere. */
  async function refreshLibraries() {
    await refreshBaseOptions();
    await refreshProjectileOptions();
    baseSelect.value = state.baseEnemyLibraryId || '';
    renderPhases();
  }

  renderAttacks();
  renderPhases();
  refresh();

  return { refreshLibraries, linkLibraryEntry };
}
