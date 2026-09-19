// modules/entry-kinds.js
// -----------------------------------------------------------------------------
// What KIND of thing a library entry is — `enemy` | `projectile` | `fx` | `boss` — and everything
// that differs between them. Single source of truth for the kind dimension, the same role
// `animation-lanes.js`'s CANONICAL_ANIMATION_NAMES already plays for the enemy animation
// vocabulary (which this file imports rather than restating, so there is still exactly one list
// of enemy state names in the app).
//
// Why a kind at all: the Sprites tab's machinery — background removal, detect/grid slicing, box
// editing, lane assignment, manifest+PNG export — is entirely generic. The only enemy-specific
// things in it were the lane NAMES it pre-creates and the wording around them. Splitting that out
// here makes the same tab author a projectile or a VFX with no duplicated pipeline, and gives the
// exported manifest/config a `kind` field so the Unity side can discriminate instead of assuming
// every bundle is an enemy.
//
// Adding a fifth kind = one more entry in KINDS below. Nothing else in the app hardcodes the list.

import { CANONICAL_ANIMATION_NAMES } from './animation-lanes.js';

/**
 * Per-kind definition.
 *
 * - `sheet`      whether this kind OWNS a sprite sheet — i.e. whether it can be authored in the
 *                Sprites tab at all and therefore appears in its "4. Qué es" selector. True for
 *                the three sheet kinds; false for `boss`, which has no frames of its own and
 *                names an existing enemy entry as its art/stat baseline instead. Everything that
 *                assumes `entry.sprite` exists (Cargar en Sprites, Exportar zip, the thumbnail
 *                and lane badges) is gated on this, so a sheetless kind never reaches code that
 *                would throw on a missing `sprite`.
 * - `lanes`      the full canonical vocabulary offered in the "+ Nueva" dropdown and in
 *                "Animaciones automáticas"'s row mapping. A name outside this list is still
 *                allowed via "Personalizado…" — the list is what protects the COMMON names from
 *                a typo, not a whitelist. Empty for a sheetless kind.
 * - `defaultLanes` the subset pre-created on image load. Anything canonical but not here is the
 *                "uncommon / optional" case, one click away in the same dropdown.
 * - `projectileLanes` whether this kind can carry `type:'projectile'` lanes (the separate thrown
 *                prop nested under `Projectiles/` in the export). Only an enemy throws something
 *                that is itself a separate animated object; a projectile entry IS that object.
 * - `creator`    which config-authoring tab owns this kind's `config` block, as the tab id used
 *                by `.tabBtn[data-tab=…]`, or null for a kind nobody configures. This replaced a
 *                plain `config: true|false` boolean the moment a second creator tab existed:
 *                the Biblioteca tab's "Editar en …" action needs to know WHICH tab to switch to,
 *                not merely that some tab exists.
 * - `configFile` the file name that kind's config takes at the root of a combined bundle. Read by
 *                modules/enemy-bundle.js and matched on the Unity side by CombinedBundleImporter
 *                (`enemy-config.json`) / FxPrefabBuilder (`fx-config.json`).
 */
export const KINDS = {
  enemy: {
    id: 'enemy',
    sheet: true,
    label: 'Enemigo',
    plural: 'Enemigos',
    icon: '🍄',
    nameLabel: 'Nombre del enemigo',
    namePlaceholder: 'ej. MushroomWarrior',
    fallbackName: 'Enemy',
    saveLabel: '💾 Guardar como enemigo',
    emptyMessage: 'Todavía no hay enemigos guardados. Usa "💾 Guardar como enemigo" en la pestaña Sprites.',
    lanes: CANONICAL_ANIMATION_NAMES,
    // Wake is excluded only because it is the uncommon 6th state (sleeping enemies), not because
    // it is any less canonical — see animation-lanes.js.
    defaultLanes: CANONICAL_ANIMATION_NAMES.filter((n) => n !== 'Wake'),
    projectileLanes: true,
    creator: 'enemy',
    configFile: 'enemy-config.json',
  },

  projectile: {
    id: 'projectile',
    sheet: true,
    label: 'Proyectil',
    plural: 'Proyectiles',
    icon: '🔥',
    nameLabel: 'Nombre del proyectil',
    namePlaceholder: 'ej. FireOrb',
    fallbackName: 'Projectile',
    saveLabel: '💾 Guardar como proyectil',
    emptyMessage: 'Todavía no hay proyectiles guardados. Cambia el tipo a "Proyectil" en la pestaña Sprites y guarda uno.',
    // The life of a projectile, in flight order. Mirrors the shape the project already ships by
    // hand for the Árbol Ancestral's orb (Orbe_Idle / Orbe_Move / Orbe_Impact).
    lanes: ['Awake', 'Move', 'Impact'],
    // Awake (the spawn/charge-up flash before it starts travelling) is optional: most projectiles
    // are simply Move + Impact, which is what ProjectileFactory needs at minimum.
    defaultLanes: ['Move', 'Impact'],
    projectileLanes: false,
    creator: 'fx',
    configFile: 'fx-config.json',
  },

  fx: {
    id: 'fx',
    sheet: true,
    label: 'VFX',
    plural: 'VFX',
    icon: '✨',
    nameLabel: 'Nombre del efecto',
    namePlaceholder: 'ej. GreenFireHazard',
    fallbackName: 'Fx',
    saveLabel: '💾 Guardar como VFX',
    emptyMessage: 'Todavía no hay efectos guardados. Cambia el tipo a "VFX" en la pestaña Sprites y guarda uno.',
    // Appear → persist → finish. Covers both shapes of VFX this project actually spawns: a
    // one-shot (explosion, impact flash — a single `Spawn` lane with loop off, what VfxOneShot /
    // SpriteFlipbook(oneShot) consume) and a sustained effect (a hazard patch, an aura — all
    // three lanes, what Pipeline.SpriteStateMachine consumes as named states).
    lanes: ['Spawn', 'Loop', 'End'],
    // All three, unlike the other two kinds: none of them is the "uncommon" one, and an empty
    // lane costs nothing — populateSpriteZip skips any lane with no frames, so a one-shot
    // authored as just `Spawn` exports exactly one animation.
    defaultLanes: ['Spawn', 'Loop', 'End'],
    projectileLanes: false,
    creator: 'fx',
    configFile: 'fx-config.json',
  },

  // The one kind with no sheet of its own. A boss is assembled, not drawn: it names an existing
  // enemy entry as its body/stat baseline and its own content is decks of attacks and the phases
  // that draw from them. Everything sheet-shaped is therefore empty or false here, and `sheet:
  // false` is what keeps it out of the Sprites tab's type selector — offering "author a boss
  // sheet" there would produce an entry whose lanes nothing reads.
  boss: {
    id: 'boss',
    sheet: false,
    label: 'Jefe',
    plural: 'Jefes',
    icon: '👑',
    nameLabel: 'Nombre del jefe',
    namePlaceholder: 'ej. EntCristalino',
    fallbackName: 'Boss',
    saveLabel: '💾 Guardar jefe',
    emptyMessage: 'Todavía no hay jefes guardados. Créalos en la pestaña Boss Creator.',
    lanes: [],
    defaultLanes: [],
    projectileLanes: false,
    creator: 'boss',
    configFile: 'boss-config.json',
  },
};

/** Kind ids in display order. */
export const KIND_IDS = Object.keys(KINDS);

/** The kinds the Sprites tab can author — the ones that own frames. See `sheet` above. */
export const SHEET_KIND_IDS = KIND_IDS.filter((id) => KINDS[id].sheet);

/** The default kind for a brand-new sheet and for any legacy record saved before kinds existed. */
export const DEFAULT_KIND = 'enemy';

/** Never throws: an unknown/absent kind resolves to the enemy definition (legacy records). */
export function kindOf(kind) {
  return KINDS[kind] || KINDS[DEFAULT_KIND];
}

/** Normalizes any stored/incoming value to a valid kind id. */
export function normalizeKind(kind) {
  return KINDS[kind] ? kind : DEFAULT_KIND;
}

/** The canonical lane vocabulary for a kind (the "+ Nueva" dropdown). */
export function canonicalLanesFor(kind) {
  return kindOf(kind).lanes;
}

/** The lanes pre-created when a sheet of this kind is loaded. */
export function defaultLanesFor(kind) {
  return kindOf(kind).defaultLanes;
}
