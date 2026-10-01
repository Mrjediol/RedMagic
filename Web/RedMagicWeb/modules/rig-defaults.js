// modules/rig-defaults.js
// -----------------------------------------------------------------------------
// Defaults of the 🦴 Rig Composer: a new part, a new rig document, and the ProceduralRig scalar
// fields with their C# initializers (Assets/Scripts/Rigging/ProceduralRig.cs) — so a web-authored
// rig starts exactly where a hand-made one would. The Transform-typed rig fields (aimPart, muzzle,
// stretchPart, stretchRiders) are NOT here: they are roles, set per part in the composer.
//
// KEEP IN SYNC with ProceduralRig.cs's field initializers. The schema (schemas/rig-config.schema.json)
// lists the names; this file only adds the default and the Spanish label shown in the form.

/** Grouped scalar fields of ProceduralRig, in the Inspector's order. type: number | vector2 | int | string | color | layers. */
export const RIG_FIELD_GROUPS = [
  { title: 'Flotar', fields: [
    ['hoverBob', 'vector2', { x: 0, y: 0.08 }, 'Vaivén de todo el cuerpo (u)'],
    ['hoverSpeed', 'number', 2.2, 'Velocidad del vaivén'],
  ] },
  { title: 'Inclinación al moverse', fields: [
    ['leanDegrees', 'number', 12, 'Grados a velocidad plena'],
    ['leanFullSpeed', 'number', 2.5, 'Velocidad (u/s) de inclinación completa'],
    ['leanResponse', 'number', 8, 'Rapidez con que sigue a la velocidad'],
  ] },
  { title: 'Inercia', fields: [
    ['followStiffness', 'number', 0, 'Rigidez del muelle (0 = sin inercia)'],
    ['followDamping', 'number', 9, 'Amortiguación'],
    ['followMaxOffset', 'number', 0.6, 'Separación máxima (u)'],
    ['moveGesture', 'string', '', 'Gesto al desplazarse (nombre)'],
  ] },
  { title: 'Apuntar', fields: [
    ['aimArtAngle', 'number', 0, 'Ángulo del cañón en el dibujo (°)'],
    ['aimMaxAngle', 'number', 75, 'Giro máximo desde el reposo (°)'],
  ] },
  { title: 'Ataque', fields: [
    ['windup', 'number', 0.4, 'Carga (s): el golpe sale al acabar'],
    ['recover', 'number', 0.45, 'Vuelta al reposo (s)'],
    ['windupPullAngle', 'number', 10, 'Anticipación del brazo (°)'],
    ['windupBodyTilt', 'number', 6, 'Cuerpo echado atrás al cargar (°)'],
    ['recoilDelay', 'number', 0.06, 'Espera antes de la coz (s)'],
    ['recoilKick', 'number', 0.09, 'Duración de la coz (s)'],
    ['recoilAngle', 'number', 15, 'Coz: grados que sube'],
    ['recoilDistance', 'number', 0.3, 'Coz: retroceso (u)'],
    ['bodyRecoil', 'number', 0.15, 'Empujón del cuerpo (u)'],
  ] },
  { title: 'Fogonazo', fields: [
    ['muzzleFlashColor', 'color', '#8cffffe6', 'Color'],
    ['muzzleFlashSize', 'number', 0.55, 'Tamaño'],
    ['muzzleFlashTime', 'number', 0.1, 'Duración (s)'],
  ] },
  { title: 'Plegar / desplegar', fields: [
    ['foldedStretch', 'number', 0.05, 'Escala Y del cuello plegado'],
    ['foldedRiderScaleY', 'number', 0.6, 'Escala Y de los pasajeros plegados'],
    ['foldedRiderOffset', 'vector2', { x: 0, y: 0 }, 'Desplazamiento de los pasajeros plegados'],
    ['wakeDuration', 'number', 0.8, 'Despertar (s)'],
    ['sleepDuration', 'number', 0.55, 'Plegarse (s)'],
    ['wakeOvershoot', 'number', 2.2, 'Rebote al desplegarse'],
    ['wakeFlickers', 'int', 0, 'Parpadeos de ojos al despertar'],
    ['wakeFlickerPortion', 'number', 0.4, 'Tramo final en que se abren los ojos (0-1)'],
  ] },
  { title: 'Piezas lanzadas (jefes)', fields: [
    ['drivenLease', 'number', 0.25, 'Vuelve sola si el ataque la suelta (s)'],
    ['drivenReturn', 'number', 0.35, 'Duración de la vuelta (s)'],
  ] },
  { title: 'Golpe recibido', fields: [
    ['staggerStrength', 'number', 2.5, 'Multiplicador en un tambaleo'],
    ['hurtDuration', 'number', 0.28, 'Duración (s)'],
    ['hurtTilt', 'number', 14, 'Tambaleo (°)'],
    ['hurtSquash', 'number', 0.12, 'Aplastamiento (0.1 = 10%)'],
  ] },
  { title: 'Muerte (se desmonta)', fields: [
    ['breakImpulse', 'number', 3.5, 'Velocidad de salida (u/s)'],
    ['breakUpward', 'number', 4, 'Velocidad extra hacia arriba (u/s)'],
    ['breakSpin', 'number', 540, 'Giro máximo (°/s)'],
    ['breakGravity', 'number', 20, 'Gravedad de las piezas'],
    ['breakBounce', 'number', 0.35, 'Rebote (0-1)'],
    ['breakGround', 'layers', ['Ground', 'Platform'], 'Capas contra las que rebotan'],
  ] },
];

/** { field: default } for every scalar rig field. */
export function createDefaultRigFields() {
  const out = {};
  RIG_FIELD_GROUPS.forEach((g) => g.fields.forEach(([key, , def]) => {
    out[key] = typeof def === 'object' ? JSON.parse(JSON.stringify(def)) : def;
  }));
  return out;
}

/** Radians between consecutive parts' sway phases — the golden angle, same as ProceduralRigTool. */
const GOLDEN_PHASE = 2.39996;

let _uidCounter = 0;
export function newUid() {
  _uidCounter += 1;
  return `p${Date.now().toString(36)}${_uidCounter}`;
}

/**
 * A part of the body. Geometry lives in the part's ORIGINAL canvas pixels (y down): `pivot` is the
 * joint (it becomes the sprite's own pivot in Unity, so RigPart.pivot exports as 0), `center` the
 * visible centre / contact point. `position` is where the pivot sits in the parent (Unity units, y up).
 */
export function createPart({ name, image = null, index = 0 }) {
  const pivot = image ? { x: image.cropX + image.w / 2, y: image.cropY + image.h / 2 } : { x: 0, y: 0 };
  return {
    uid: newUid(),
    name,
    parent: null,
    image,            // { dataURL, w, h, origW, origH, cropX, cropY } | null (empty node)
    pivot,
    center: null,     // null = the pivot
    position: { x: 0, y: 0 },
    rotation: 0,
    scale: { x: 1, y: 1 },
    rigPart: true,
    pointAngle: 0,
    leanFollow: 0,
    sway: {
      move: { x: 0, y: 0.03 },
      rotate: 2,
      scale: { x: 0, y: 0 },
      speed: 1.6 + 0.35 * (index % 3),
      phase: (index * GOLDEN_PHASE) % (Math.PI * 2),
    },
    breaksOnDeath: true,
    variants: { asleep: null, awake: null, attacking: null, stages: [] },
    slot: null,       // template slot this part fills: { id, label, hint, optional, pivot, size, … } (rig-templates.js)
  };
}

/** A saved composer document back into a valid state (fills anything added since it was saved). */
export function adoptRigState(draft) {
  const base = createDefaultRigState();
  const clone = (o) => JSON.parse(JSON.stringify(o));
  const s = { ...base, ...clone(draft) };
  s.roles = { ...base.roles, ...(draft.roles || {}) };
  s.rig = { ...base.rig, ...(draft.rig || {}) };
  s.bossLimbs = { ...base.bossLimbs, ...(draft.bossLimbs || {}) };
  s.bossBody = { ...base.bossBody, ...(draft.bossBody || {}) };
  s.parts = (s.parts || []).map((p) => ({ ...createPart({ name: p.name }), ...p, variants: { asleep: null, awake: null, attacking: null, stages: [], ...(p.variants || {}) } }));
  return s;
}

/** A whole composer document — also what is stored as the library record's `draft`. */
export function createDefaultRigState() {
  return {
    name: '',
    kind: 'enemy',                 // rig-config 'kind': enemy | boss
    driver: 'code',
    pixelsPerUnit: 100,
    rootScale: 1,
    parts: [],
    roles: {
      aim: null,                   // part uid
      muzzle: null,                // {x,y} in the aim part's original pixels
      stretch: null,
      riders: [],
      limbs: [],
      orbiters: [],
      grips: {},                   // limb uid -> {x,y} original pixels
    },
    rig: createDefaultRigFields(),
    collider: { type: 'box', size: { x: 1, y: 1 }, offset: { x: 0, y: 0.5 } },
    corpse: { linger: 1.1, fadeDuration: 0.4 },
    bossLimbs: { contactRadius: 0.9, orbiterRadius: 0.5 },
    bossBody: { health: 2500, arenaHalfWidth: 15, arenaHeight: 12, activationRadius: 18, contactDamage: 12, bodyRenderer: null },
    bossConfigId: null,            // library entry (kind boss) whose config travels in the zip
    template: null,                // { id, height, fitted } when made from a body template (rig-templates.js)
    gestures: null,                // RigGesture list (poses by part uid | 'Visual'); null = not exported, Unity keeps its own
  };
}
