// modules/boss-export.js
// -----------------------------------------------------------------------------
// Boss Creator form state (boss-defaults.js) -> the boss-config.json BossConfigImporter.cs reads.
// One function, used by the preview, the JSON download and the .zip bundle, so they never disagree.
//
// What goes where in Unity:
//  - `attacks.<id>`: base BossAttack fields at the top level, subclass fields in `params`. Only
//    values that DIFFER from the type's default are written, so re-importing does not undo what was
//    tuned by hand in Unity on fields the web never touched. A ProjectileSpec is the exception:
//    Unity rebuilds it whole (ProjectileConfigImporter.Resolve starts from `new ProjectileSpec()`),
//    so once anything in it changes it is written in full.
//  - art picked from the library -> `{ "art": "<id>" }`, and `artAssets.<id>` says which frames
//    build that prefab (BossArtBuilder.cs). Folders are RELATIVE ("Art/<libraryId>"): the importer
//    resolves them against Assets/Art/Bosses/<slug>/Source/, where the .zip puts the frames.
//  - `body`: the animations (from the chosen library entry's lanes) + prefab stats + movement
//    (BossBodyBuilder.cs). Stats and movement only apply the FIRST time the prefab is created;
//    after that the prefab is tuned in Unity and only clips/controller/definition are relinked.

import { defaultsOf, clone } from './boss-defaults.js';

/** Folder name for a lane inside the zip (and in `folder` keys) — shared with boss-bundle.js. */
export function laneFolder(name) {
  const safe = String(name || '').replace(/[^A-Za-z0-9_-]/g, '_');
  return safe || 'Anim';
}

function safeId(raw, fallback) {
  const cleaned = (raw || '').trim().replace(/[^A-Za-z0-9_-]/g, '_');
  return /^[A-Za-z]/.test(cleaned) ? cleaned : fallback;
}

const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1);

/** Animation lanes of a library entry that actually have frames, in their order. */
export function framedLanes(entry) {
  return (entry?.sprite?.lanes || []).filter((l) => l.type === 'animation' && l.frameBoxIndices.length > 0);
}

/** Every animation name the body will have in Unity (its lanes + derived clips). */
export function bodyAnimationNames(state, entriesById) {
  const entry = entriesById.get(state.body.libraryId);
  const names = framedLanes(entry).map((l) => l.name);
  state.body.derived.forEach((d) => { if (d.name && !names.includes(d.name)) names.push(d.name); });
  return names;
}

const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/**
 * @returns {{doc: object, art: Array<{id, libraryId, kind}>, problems: string[], notes: string[]}}
 *   `problems` block the export (Unity would fail); `notes` are things worth knowing.
 */
export function buildBossExport(state, catalog, entriesById) {
  const problems = [];
  const notes = [];

  /** Two lanes whose names sanitize to the same folder would mix their frames in the zip. */
  function checkFolders(lanes, who) {
    const seen = new Map();
    lanes.forEach((l) => {
      const f = laneFolder(l.name);
      if (seen.has(f)) problems.push(`${who}: "${seen.get(f)}" y "${l.name}" acaban en la misma carpeta "${f}"; renombra una.`);
      else seen.set(f, l.name);
    });
  }

  // ---------------------------------------------------------------- art registry
  const artByKey = new Map();
  const usedArtIds = new Set();

  function artRef(libraryId, kind, where) {
    if (!libraryId) return null;
    const entry = entriesById.get(libraryId);
    if (!entry) { notes.push(`${where}: el arte elegido ya no está en la biblioteca → se usa el de por defecto.`); return null; }
    if (framedLanes(entry).length === 0) { notes.push(`${where}: '${entry.enemyName}' no tiene frames → por defecto.`); return null; }

    const key = `${libraryId}:${kind}`;
    if (!artByKey.has(key)) {
      let id = safeId(`${entry.enemyName}_${cap(kind)}`, `Art_${cap(kind)}`);
      while (usedArtIds.has(id)) id = `${id}2`;
      usedArtIds.add(id);
      artByKey.set(key, { id, libraryId, kind, entry });
    }
    return { art: artByKey.get(key).id };
  }

  // ---------------------------------------------------------------- body
  const body = state.body;
  const bodyEntry = entriesById.get(body.libraryId);
  const animNames = bodyAnimationNames(state, entriesById);

  let bodyDoc = null;
  if (!body.libraryId) {
    notes.push('Sin cuerpo: sólo se crean los ataques y la definición (sin prefab jugable).');
  } else if (!bodyEntry) {
    problems.push('El cuerpo elegido ya no está en la biblioteca.');
  } else {
    const lanes = framedLanes(bodyEntry);
    if (!lanes.some((l) => l.name === 'Idle')) problems.push(`El cuerpo '${bodyEntry.enemyName}' necesita una animación llamada exactamente "Idle".`);

    const animations = {};
    checkFolders(lanes, `El cuerpo '${bodyEntry.enemyName}'`);
    lanes.forEach((lane) => {
      if (lane.name.includes('_')) problems.push(`Animación "${lane.name}": el nombre no puede llevar "_" (Unity nombra los clips <jefe>_<animación>).`);
      const o = body.anims[lane.name] || {};
      const a = { fps: o.fps ?? lane.fps ?? 10, loop: o.loop ?? !!lane.loop };
      if (Number.isInteger(o.releaseFrame) && o.releaseFrame >= 0) {
        a.releaseFrame = Math.min(o.releaseFrame, lane.frameBoxIndices.length - 1);
      }
      if (laneFolder(lane.name) !== lane.name) a.folder = laneFolder(lane.name);
      animations[lane.name] = a;
    });

    const derived = {};
    body.derived.forEach((d, i) => {
      if (!d.name || !d.from) return;
      if (!lanes.some((l) => l.name === d.from)) { problems.push(`Clip derivado "${d.name}": "${d.from}" no es una animación del cuerpo.`); return; }
      if (d.name.includes('_')) problems.push(`Clip derivado "${d.name}": el nombre no puede llevar "_".`);
      derived[d.name] = { from: d.from, first: d.first | 0, count: Math.max(1, d.count | 0), fps: d.fps || 10, loop: !!d.loop };
    });

    bodyDoc = { folder: 'Body', libraryId: body.libraryId, entryName: bodyEntry.enemyName };
    ['height', 'hoverHeight', 'health', 'contactDamage', 'arenaHalfWidth', 'arenaHeight',
      'activationRadius', 'colliderWidth', 'colliderHeight'].forEach((k) => { bodyDoc[k] = body[k]; });
    bodyDoc.animations = animations;
    if (Object.keys(derived).length) bodyDoc.derived = derived;

    if (body.movement?.type) {
      const m = catalog?.movements.find((x) => x.type === body.movement.type);
      bodyDoc.movement = { type: body.movement.type, params: clone(body.movement.params || (m ? defaultsOf(m.params) : {})) };
    }

    const circle = artRef(body.warnCircle, 'warning', 'Aviso círculo');
    const arrow = artRef(body.warnArrow, 'warning', 'Aviso flecha');
    if (circle) bodyDoc.warnCircle = circle.art;
    if (arrow) bodyDoc.warnArrow = arrow.art;
  }

  // ---------------------------------------------------------------- attacks
  const attacks = {};
  const idMap = new Map(); // form id -> exported id
  const usedIds = new Set();
  const commonDefaults = defaultsOf(catalog?.common || []);

  state.attacks.forEach((attack, i) => {
    let id = safeId(attack.id, `Ataque${i + 1}`);
    while (usedIds.has(id)) id = `${id}_${i + 1}`;
    usedIds.add(id);
    idMap.set(attack.id, id);

    const typeEntry = catalog?.attacks.find((a) => a.type === attack.type);
    const where = `Ataque ${id}`;
    const out = { type: attack.type };

    // Base fields: only what differs from the default (gesture/displayName whenever set).
    const edited = attack.edited || {};
    Object.entries(attack.common || {}).forEach(([k, v]) => {
      if (k === 'gesture') {
        if (typeof v === 'string' && v.trim()) out[k] = v.trim();
        return;
      }
      if (k === 'displayName') {
        // Blank (or the C# placeholder "Ataque") -> the attack's id, so every card has its own name.
        const name = typeof v === 'string' ? v.trim() : '';
        out[k] = name && name !== commonDefaults.displayName ? name : attack.id;
        return;
      }
      if (!same(v, commonDefaults[k]) || edited[k]) out[k] = clone(v);
    });

    if (out.gesture && body.libraryId && !animNames.includes(out.gesture)) {
      notes.push(`${where}: el gesto "${out.gesture}" no es una animación del cuerpo → el ataque no animará.`);
    }
    if (out.gesture && bodyEntry) {
      const rf = body.anims[out.gesture]?.releaseFrame;
      const isLane = framedLanes(bodyEntry).some((l) => l.name === out.gesture);
      if (isLane && !(Number.isInteger(rf) && rf >= 0)) {
        notes.push(`${where}: "${out.gesture}" no tiene frame de suelta → el golpe sale al acabar la animación.`);
      }
    }

    if (typeEntry) {
      const params = exportFields(typeEntry.params, attack.params || {}, where, edited, '');
      if (Object.keys(params).length) out.params = params;
    } else {
      notes.push(`${where}: el tipo "${attack.type}" no está en el catálogo de Unity → se exportan sus valores tal cual.`);
      if (attack.params && Object.keys(attack.params).length) out.params = clone(attack.params);
    }

    attacks[id] = out;
  });

  function exportFields(fields, values, where, edited, prefix) {
    const out = {};
    fields.forEach((f) => {
      const v = values[f.name];
      if (f.type === 'unity') return;
      if (f.type === 'art') {
        const ref = artRef(v, f.artKind, `${where} · ${f.artLabel || f.label}`);
        if (ref) out[f.name] = ref;
        return;
      }
      if (f.type === 'object') {
        const sub = values[f.name] || {};
        if (f.spec === 'projectile') {
          const changed = f.fields.some((c) => (c.type === 'art' ? !!sub[c.name]
            : c.type !== 'unity' && (!same(sub[c.name], c.default) || edited[`${prefix}${f.name}.${c.name}`])));
          if (!changed) return;
          const full = {};
          f.fields.forEach((c) => {
            if (c.type === 'unity') return;
            if (c.type === 'art') {
              const ref = artRef(sub[c.name], c.artKind, `${where} · ${f.artLabel || f.label}`);
              if (ref) full[c.name] = ref;
            } else if (c.type === 'object') {
              full[c.name] = clone(sub[c.name]);
            } else {
              full[c.name] = clone(sub[c.name]);
            }
          });
          out[f.name] = full;
        } else {
          const nested = exportFields(f.fields, sub, where, edited, `${prefix}${f.name}.`);
          if (Object.keys(nested).length) out[f.name] = nested;
        }
        return;
      }
      if (v === undefined || (same(v, f.default) && !edited[`${prefix}${f.name}`])) return;
      out[f.name] = clone(v);
    });
    return out;
  }

  if (state.attacks.length === 0) problems.push('Añade al menos un ataque.');

  // ---------------------------------------------------------------- phases
  const phases = state.phases.map((p, i) => {
    const deck = p.attacks.filter((id) => idMap.has(id)).map((id) => idMap.get(id));
    if (deck.length === 0) notes.push(`Fase ${i + 1}: mazo vacío.`);
    const out = {
      displayName: p.displayName || `Fase ${i + 1}`,
      startsAtHealth: i === 0 ? 1 : p.startsAtHealth,
      attacks: deck,
      pauseBetweenAttacks: clone(p.pauseBetweenAttacks),
      damageScale: p.damageScale,
      damageTakenMultiplier: p.damageTakenMultiplier,
      speedScale: p.speedScale,
      accent: p.accent,
      transitionSeconds: p.transitionSeconds,
      transitionShake: p.transitionShake,
      frenzyBelowHealth: p.frenzyBelowHealth ?? 0,
      frenzySpeedScale: p.frenzySpeedScale ?? 1.35,
    };
    if (p.openingAttack && idMap.has(p.openingAttack)) out.openingAttack = idMap.get(p.openingAttack);
    return out;
  });

  // ---------------------------------------------------------------- art assets
  const artAssets = {};
  const art = [];
  artByKey.forEach(({ id, libraryId, kind, entry }, key) => {
    const opts = state.art[key] || {};
    let lanes = framedLanes(entry);
    checkFolders(lanes, `El arte '${entry.enemyName}'`);
    if (kind === 'prop') lanes = [...lanes].sort((a, b) => (b.name === 'Move') - (a.name === 'Move'));

    const states = {};
    lanes.forEach((l) => { states[laneFolder(l.name)] = { folder: laneFolder(l.name), fps: l.fps || 10, loop: !!l.loop }; });

    const a = { kind, folder: `Art/${libraryId}`, libraryId, entryName: entry.enemyName, states };
    if (kind !== 'warning') a.worldWidth = opts.worldWidth ?? 1.5;
    if (kind === 'projectile') {
      a.colliderScale = opts.colliderScale ?? 0.8;
      a.faceTravelDirection = opts.faceTravelDirection ?? true;
    }
    if (kind === 'warning') a.tint = !!opts.tint;
    artAssets[id] = a;
    art.push({ id, libraryId, kind, key });
  });

  const doc = { kind: 'boss', displayName: state.displayName.trim() };
  if (state.title.trim()) doc.title = state.title.trim();
  if (state.description.trim()) doc.description = state.description.trim();
  if (Object.keys(artAssets).length) doc.artAssets = artAssets;
  if (bodyDoc) doc.body = bodyDoc;
  doc.attacks = attacks;
  doc.phases = phases;

  if (!doc.displayName) problems.push('Ponle nombre al jefe.');

  return { doc, art, problems, notes };
}
