// modules/attack-export.js
// -----------------------------------------------------------------------------
// Attack-card state (modules/attack-cards.js — the Boss Creator's attack shape) -> the `attacks`
// and `artAssets` blocks Unity reads. The SAME rules as modules/boss-export.js applies to a boss
// (BossConfigImporter.cs / BossArtBuilder.cs read both), written once here for whoever else owns a
// list of catalog attacks — today the Enemy Creator's deck (EnemyDeckImporter.cs).
//
//  - `attacks.<id>`: base BossAttack fields at the top level, subclass fields in `params`. Only
//    values that DIFFER from the type's default are written (or that were edited in the web), so
//    re-importing does not undo what was tuned by hand in Unity on fields the web never touched.
//    A ProjectileSpec is the exception: Unity rebuilds it whole, so once anything in it changes it
//    is written in full.
//  - art picked from the library -> `{ "art": "<id>" }`, and `artAssets.<id>` says which frames
//    build that prefab. Folders are RELATIVE ("Art/<libraryId>"): the importer resolves them
//    against the folder where the bundle's frames land.

import { defaultsOf, clone } from './boss-defaults.js';
import { framedLanes, laneFolder } from './boss-export.js';

function safeId(raw, fallback) {
  const cleaned = (raw || '').trim().replace(/[^A-Za-z0-9_-]/g, '_');
  return /^[A-Za-z]/.test(cleaned) ? cleaned : fallback;
}

const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1);
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/**
 * @param {object[]} attackStates - attack cards, in order.
 * @param {object|null} catalog - Unity's catalog (boss-catalog.js).
 * @param {Map<string, object>} entriesById - library records by id (art slots).
 * @param {object} artOptions - `${libraryId}:${artKind}` -> { worldWidth, colliderScale, faceTravelDirection, tint }.
 * @param {string[]|null} gestureNames - the body's gestures (null = unknown: no gesture notes).
 * @returns {{attacks: object, artAssets: object, art: Array<{id, libraryId, kind, key}>,
 *            idMap: Map<string, string>, problems: string[], notes: string[]}}
 */
export function buildAttacksExport(attackStates, catalog, entriesById, artOptions = {}, gestureNames = null) {
  const problems = [];
  const notes = [];

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

  // ---------------------------------------------------------------- attacks
  const attacks = {};
  const idMap = new Map(); // form id -> exported id
  const usedIds = new Set();
  const commonDefaults = defaultsOf(catalog?.common || []);

  attackStates.forEach((attack, i) => {
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

    if (out.gesture && gestureNames && !gestureNames.includes(out.gesture)) {
      notes.push(`${where}: el gesto "${out.gesture}" no existe en el cuerpo → el aviso será sólo el tiempo de 'telegraph'.`);
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

  // ---------------------------------------------------------------- art assets
  const artAssets = {};
  const art = [];
  artByKey.forEach(({ id, libraryId, kind, entry }, key) => {
    const opts = artOptions[key] || {};
    let lanes = framedLanes(entry);

    const seen = new Map();
    lanes.forEach((l) => {
      const f = laneFolder(l.name);
      if (seen.has(f)) problems.push(`El arte '${entry.enemyName}': "${seen.get(f)}" y "${l.name}" acaban en la misma carpeta "${f}"; renombra una.`);
      else seen.set(f, l.name);
    });
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

  return { attacks, artAssets, art, idMap, problems, notes };
}

/**
 * The reverse trip, for reopening a saved config in the form: an exported `attacks` block (+ the
 * `artAssets` its art refs point at) back into attack-card states. Whatever the config says is
 * marked as edited, so it keeps being exported even where it equals the type's default.
 *
 * @returns {{byId: Object<string, object>, artOptions: object}} `byId` is keyed by the exported id.
 */
export function adoptAttacksFromConfig(config, catalog) {
  const artAssets = config?.artAssets || {};
  const isArtRef = (v) => v && typeof v === 'object' && !Array.isArray(v)
    && Object.keys(v).length === 1 && typeof v.art === 'string';

  /** `{art: id}` -> the library id the form's art pickers hold. */
  const unArt = (v) => {
    if (isArtRef(v)) return artAssets[v.art]?.libraryId || '';
    if (v && typeof v === 'object' && !Array.isArray(v)) {
      const out = {};
      Object.entries(v).forEach(([k, x]) => { out[k] = unArt(x); });
      return out;
    }
    return clone(v);
  };

  const merge = (base, over) => {
    const out = { ...base };
    Object.entries(over || {}).forEach(([k, v]) => {
      out[k] = v && typeof v === 'object' && !Array.isArray(v) && base[k] && typeof base[k] === 'object' && !Array.isArray(base[k])
        ? merge(base[k], v) : v;
    });
    return out;
  };

  const markEdited = (values, prefix, edited) => {
    Object.entries(values || {}).forEach(([k, v]) => {
      if (isArtRef(v)) return;
      if (v && typeof v === 'object' && !Array.isArray(v) && !('x' in v && 'y' in v)) markEdited(v, `${prefix}${k}.`, edited);
      else edited[`${prefix}${k}`] = true;
    });
  };

  const commonNames = (catalog?.common || []).map((f) => f.name);
  const byId = {};
  Object.entries(config?.attacks || {}).forEach(([id, a]) => {
    if (!a || !a.type) return;   // a reference to an existing Unity asset: nothing to edit here
    const typeEntry = catalog?.attacks.find((t) => t.type === a.type);
    const common = { ...defaultsOf(catalog?.common || []), displayName: '' };
    const edited = {};
    Object.entries(a).forEach(([k, v]) => {
      if (!commonNames.includes(k)) return;
      common[k] = clone(v);
      if (k !== 'gesture' && k !== 'displayName') edited[k] = true;
    });
    if (common.displayName === id) common.displayName = '';   // exported from a blank name
    markEdited(a.params, '', edited);
    byId[id] = {
      id, type: a.type, open: false, common, edited,
      params: merge(defaultsOf(typeEntry?.params || []), unArt(a.params || {})),
    };
  });

  const artOptions = {};
  Object.values(artAssets).forEach((a) => {
    if (!a?.libraryId || !a.kind) return;
    artOptions[`${a.libraryId}:${a.kind}`] = {
      worldWidth: a.worldWidth ?? (a.kind === 'warning' ? 2 : 1.5),
      colliderScale: a.colliderScale ?? 0.8,
      faceTravelDirection: a.faceTravelDirection ?? a.kind === 'projectile',
      tint: !!a.tint,
    };
  });

  return { byId, artOptions };
}
