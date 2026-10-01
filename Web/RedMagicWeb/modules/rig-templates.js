// modules/rig-templates.js
// -----------------------------------------------------------------------------
// Body templates of the 🦴 Rig Composer: a template is a BODY SHAPE, not a character — a checklist
// of slots (which parts, the role of each, who hangs from whom, draw order) plus default sway, lean,
// timings, gestures and a suggested behaviour. The same template must fit very different art.
//
// Templates are DATA (RIG_TEMPLATES below): a fourth one is a new object in that list, no code.
// The functions under it are generic: they turn any template into a composer document and fill its
// slots. Default numbers come from the hand-written versions of the three shapes
// (Assets/Scripts/Rigging/Editor/ProceduralEnemyPacks.cs / ProceduralBossPack.cs).
//
// Slot fields (all optional except id / name / label):
//   id, name          slot id (referenced by `parent` and by gesture poses) and the part's name
//   parent            slot id it hangs from (absent = Visual)
//   label, hint       what goes here, shown in the parts list ("brazo que lanza: suelta aquí…")
//   optional          the shape still works without it (left out of the export while empty)
//   node              true = a plain empty pivot the template needs (not a slot to fill)
//   roles             'aim' | 'stretch' | 'rider' | 'limb' | 'orbiter' | 'body'
//   pivot             joint guess, normalized in the TRIMMED image, [x from left, y from TOP]
//   muzzle / grip / center   same convention: muzzle of the aim part, grip of a limb, visible centre
//   barrel            fixed aimArtAngle (absent = computed pivot → muzzle when the slot is filled)
//   at, size          schematic placement of the pivot in its parent and placeholder size (units of
//                     a body `height` tall) — only how the empty checklist is drawn. The origin
//                     (prefab root) is the CENTRE of an enemy's body, never its feet: ranges and
//                     detection are measured from the root, and the ground is the collider's job
//   sway, leanFollow, pointAngle, breaksOnDeath, rigPart     RigPart data
//   states / stage    it wants asleep/awake/attacking sprites / a boss-phase sprite (hint only)
//   match             file-name keywords: PNGs dropped in bulk go to the slot they name
//
// Lengths (sway moves, hover, recoil, gesture moves…) are written for a body `height` units tall and
// rescaled to the real art once the required slots are filled (fitLengths).

import { createDefaultRigState, createPart } from './rig-defaults.js';
import { createDefaultEnemyConfig } from './enemy-defaults.js';
import { buildExportObject } from './enemy-export.js';
import { byUid, childrenOf, worldMatrix, pixelToLocal, apply, invert, multiply, decompose, isOpenSlot } from './rig-model.js';

const pose = (part, move = [0, 0], rotate = 0, scale = [0, 0]) => ({ part, move, rotate, scale });

export const RIG_TEMPLATES = [
  // ============================================================ floating caster
  {
    id: 'floatingCaster',
    label: 'Lanzador flotante',
    kind: 'enemy',
    shape: 'Torso con cabeza, dos brazos y una parte baja. Flota y se inclina al moverse; un brazo apunta y lanza desde su punta, el otro se balancea, la parte baja late y se queda atrás.',
    fits: 'Un dron con cañón, una maga voladora que lanza hechizos con la mano, una calavera flotante con garra, un fantasma con farol…',
    height: 6,
    rig: { hoverBob: { x: 0, y: 0.12 }, leanDegrees: 14, aimMaxAngle: 70 },
    slots: [
      { id: 'lower', name: 'Lower', parent: 'torso', optional: true,
        label: 'Parte baja', hint: 'Llama, falda, cola o estela: late y se queda atrás al moverse. Una llama: sube su velocidad a ~16.',
        pivot: [0.5, 0.05], at: [0, -1.3], size: [1.4, 1.8], leanFollow: 0.6,
        sway: { move: [0, 0], rotate: 4, scale: [0.06, 0.12], speed: 6 },
        match: ['lower', 'flame', 'llama', 'skirt', 'falda', 'tail', 'cola', 'wisp', 'estela', 'thruster'] },
      { id: 'accessory', name: 'Accessory', parent: 'torso', optional: true,
        label: 'Pieza extra', hint: 'Anillo, cinturón, mochila, alas… algo más que cuelga del torso y se mece con él.',
        pivot: [0.5, 0.5], at: [0, -0.9], size: [1.8, 0.6],
        sway: { move: [0, 0.03], rotate: 2, speed: 2.3 },
        match: ['accessory', 'extra', 'ring', 'anillo', 'belt', 'cinturon', 'backpack', 'mochila', 'wing', 'ala', 'cape', 'capa'] },
      { id: 'castArm', name: 'CastArm', parent: 'torso', roles: ['aim'],
        label: 'Brazo que lanza', hint: 'Suelta aquí la mano / arma, dibujada apuntando a la derecha. Pivote en el hombro, boca en la punta: de ahí sale el proyectil. Va detrás del torso (el brazo del fondo); cámbialo con ↑↓ si tu dibujo lo lleva delante.',
        pivot: [0.12, 0.5], muzzle: [0.97, 0.5], at: [0.8, 0.6], size: [2.4, 0.9],
        sway: { move: [0, 0], rotate: 6, speed: 1.5, phase: 3.14 },
        match: ['castarm', 'rightarm', 'armright', 'brazoder', 'brazoarma', 'arma', 'gun', 'weapon', 'staff', 'wand', 'varita', 'arm', 'brazo', 'hand', 'mano', 'claw', 'garra'] },
      { id: 'torso', name: 'Torso',
        label: 'Torso', hint: 'El cuerpo: de él cuelga todo lo demás.',
        pivot: [0.5, 0.5], at: [0, 0], size: [2, 2.6],
        sway: { move: [0, 0.05], rotate: 0, speed: 1.5 },
        match: ['torso', 'body', 'cuerpo', 'chest', 'pecho'] },
      { id: 'head', name: 'Head', parent: 'torso', optional: true,
        label: 'Cabeza', hint: 'Pivote en el cuello (abajo). Cabecea un poco.',
        pivot: [0.5, 0.9], at: [0, 1.3], size: [1.3, 1.3],
        sway: { move: [0, 0], rotate: 3, speed: 1.5 },
        match: ['head', 'cabeza', 'skull', 'craneo'] },
      { id: 'offArm', name: 'OffArm', parent: 'torso', optional: true,
        label: 'Brazo libre', hint: 'El brazo que no lanza, el del lado de la cámara: se dibuja delante de todo y sólo se balancea. Pivote en el hombro.',
        pivot: [0.8, 0.15], at: [-0.9, 0.6], size: [1.2, 1.8], leanFollow: 0.3,
        sway: { move: [0, 0], rotate: 5, speed: 1.5 },
        match: ['offarm', 'leftarm', 'armleft', 'brazoizq', 'brazolibre', 'lefthand', 'manoizq'] },
    ],
    // Until attacks name them (Phase D), these wait in the rig: three gestures on the same casting arm.
    gestures: [
      { name: 'Flick', strikeTime: 0.06, recover: 0.3, shake: 0,
        windup: [pose('castArm', [-0.1, 0], -18), pose('Visual', [0, 0], 2)],
        strike: [pose('castArm', [0.15, 0], 14)] },
      { name: 'Charge', strikeTime: 0.08, recover: 0.45, shake: 1.5,
        windup: [pose('castArm', [-0.2, 0.3], 55), pose('Visual', [-0.15, 0.1], 6), pose('head', [0, 0], 6)],
        strike: [pose('castArm', [0.3, 0], -20), pose('Visual', [0.15, 0], -5)] },
      { name: 'Sweep', strikeTime: 0.14, recover: 0.4, shake: 0,
        windup: [pose('castArm', [0, 0.2], 40), pose('Visual', [0, 0], 4)],
        strike: [pose('castArm', [0.1, -0.2], -45), pose('Visual', [0, 0], -4)] },
    ],
    behaviour: {
      summary: 'FlyingRanged: vuela, mantiene distancia y dispara apuntando.',
      tuning: { archetype: 'FlyingRanged', maxHealth: 30, detectionRange: 9, attackRange: 6, personalSpace: 3,
        moveSpeed: 2.5, retreatSpeed: 2.2, hoverOffset: 1.2, attackDamage: 8, attackCooldown: 1.6,
        contactDamage: 0, aimAtTarget: true, projectileTint: '#73ffffff' },
      projectile: { speed: 9, lifetime: 2.5, size: { x: 0.4, y: 0.4 } },
    },
  },

  // ============================================================ folding sentry
  {
    id: 'foldingSentry',
    label: 'Centinela plegable',
    kind: 'enemy',
    shape: 'Una base fija, un cuello o tallo que se estira desde abajo y una cabeza que viaja en su punta. Duerme plegado, se despliega al detectar y dispara desde la cabeza.',
    fits: 'Una torreta, una planta que sale de una maceta, un periscopio, una caja sorpresa…',
    height: 4,
    rig: { hoverBob: { x: 0, y: 0 }, leanDegrees: 0, aimArtAngle: 0, aimMaxAngle: 22,
      windup: 0.65, recover: 0.5, windupPullAngle: 4, windupBodyTilt: 0,
      recoilAngle: 9, recoilDistance: 0.3, bodyRecoil: 0, hurtTilt: 5, muzzleFlashSize: 0.7,
      foldedStretch: 0.05, foldedRiderScaleY: 0.6, foldedRiderOffset: { x: 0, y: -0.05 } },
    slots: [
      { id: 'stem', name: 'Stem', roles: ['stretch'],
        label: 'Cuello / tallo', hint: 'La pieza que se estira: se escala en vertical desde su pivote, que va en su base.',
        pivot: [0.5, 1], at: [0, -1.5], size: [0.6, 2.3],
        sway: { move: [0, 0], rotate: 0 },
        match: ['stem', 'neck', 'cuello', 'tallo', 'pole', 'spring', 'muelle'] },
      { id: 'head', name: 'Head', roles: ['rider', 'aim'], states: true,
        label: 'Cabeza', hint: 'Viaja en la punta del cuello y dispara. Boca entre los ojos. Añádele sprites dormido / despierto / atacando (inspector ▸ Sprites por estado).',
        pivot: [0.5, 0.5], muzzle: [0.7, 0.45], barrel: 0, at: [0, 1.2], size: [1.6, 1.4],
        sway: { move: [0, 0.04], rotate: 1.5, speed: 1.3 },
        match: ['head', 'cabeza', 'eye', 'ojo', 'flower', 'flor', 'awake'] },
      { id: 'base', name: 'Base', breaksOnDeath: false,
        label: 'Base', hint: 'Fija en el suelo, delante de todo: plegado se lee como una caja cerrada. No sale despedida al morir.',
        pivot: [0.5, 1], at: [0, -2], size: [2.4, 1],
        sway: { move: [0, 0], rotate: 0 },
        match: ['base', 'pot', 'maceta', 'box', 'caja', 'stand', 'pie'] },
    ],
    gestures: [
      { name: 'Nod', strikeTime: 0.08, recover: 0.4, shake: 1,
        windup: [pose('head', [0, 0.15], -6)],
        strike: [pose('head', [0, -0.1], 5)] },
    ],
    behaviour: {
      summary: 'Static + Ranged que duerme hasta detectar (sleepsUntilDetected).',
      tuning: { archetype: 'Static', staticAttack: 'Ranged', sleepsUntilDetected: true, maxHealth: 45,
        detectionRange: 8, loseInterestGrace: 1.5, attackRange: 7.5, verticalTolerance: 4,
        knockbackResistance: 1, attackDamage: 12, attackCooldown: 2, contactDamage: 0, aimAtTarget: true,
        projectileTint: '#73fff2ff' },
      projectile: { speed: 16, lifetime: 1.2, size: { x: 1.3, y: 0.22 }, faceDirection: true },
    },
  },

  // ============================================================ limbed colossus (boss)
  {
    id: 'limbedColossus',
    label: 'Coloso con extremidades',
    kind: 'boss',
    shape: 'Un torso grande con una parte baja colgando, dos extremidades sueltas que los ataques lanzan y, si quieres, piezas pequeñas flotando encima. Pesado: va con inercia.',
    fits: 'Un coloso de hielo, un gólem de piedra, un par de guanteletes flotando alrededor de un núcleo…',
    height: 18,
    rig: { hoverBob: { x: 0, y: 0.47 }, hoverSpeed: 1.3, leanDegrees: 14, leanFullSpeed: 5, leanResponse: 6,
      followStiffness: 55, followDamping: 8, followMaxOffset: 1.84, moveGesture: 'Move',
      hurtTilt: 6, hurtSquash: 0.05, staggerStrength: 3, breakImpulse: 4, breakUpward: 5, breakSpin: 360 },
    corpse: { linger: 1.4, fadeDuration: 0.5 },
    bossLimbs: { contactRadius: 0.9, orbiterRadius: 0.45 },
    slots: [
      { id: 'lower', name: 'Lower', parent: 'torso', optional: true,
        label: 'Parte baja', hint: 'Punta, cola o pedestal que cuelga del torso: gira desde arriba y se queda atrás como un péndulo.',
        pivot: [0.5, 0.05], center: [0.5, 0.6], at: [0, -4.5], size: [3, 5], leanFollow: 0.8,
        sway: { move: [0, 0.1], rotate: 5, speed: 1.2, phase: 2.1 },
        match: ['lower', 'tip', 'punta', 'tail', 'cola', 'pedestal', 'base'] },
      { id: 'torso', name: 'Torso', roles: ['body'], stage: true,
        label: 'Torso', hint: 'El cuerpo: el aura de aviso copia este sprite. Para la fase 2, añádele una etapa (inspector ▸ Sprites por estado: torso agrietado).',
        pivot: [0.5, 0.5], at: [0, 11], size: [8, 9],
        sway: { move: [0, 0.08], rotate: 1.2, speed: 1.1, phase: 0 },
        match: ['torso', 'body', 'cuerpo', 'core', 'nucleo'] },
      { id: 'crown', name: 'Crown', parent: 'torso', node: true, breaksOnDeath: false, at: [0, 6],
        sway: { move: [0, 0.3], rotate: 2, speed: 1.9, phase: 1.3 } },
      { id: 'orbiterA', name: 'OrbiterLeft', parent: 'crown', optional: true, roles: ['orbiter'],
        label: 'Orbital 1', hint: 'Pieza pequeña que flota sobre la cabeza y se lanza en picado (cristal, piedra). Dibujada con la punta hacia arriba.',
        pivot: [0.5, 0.5], center: [0.5, 0.5], at: [-1.6, 0.3], size: [1.1, 1.8], pointAngle: 90,
        sway: { move: [0, 0.22], rotate: 6, speed: 2.0, phase: 2.1 },
        match: ['orbiter', 'crystal', 'cristal', 'crown', 'corona', 'stone', 'piedra', 'shard', 'esquirla'] },
      { id: 'orbiterB', name: 'OrbiterCenter', parent: 'crown', optional: true, roles: ['orbiter'],
        label: 'Orbital 2', hint: 'Otra pieza flotante (opcional).',
        pivot: [0.5, 0.5], center: [0.5, 0.5], at: [0, 0.7], size: [1.1, 1.8], pointAngle: 90,
        sway: { move: [0, 0.22], rotate: 6, speed: 2.3, phase: 0 },
        match: ['orbiter', 'crystal', 'cristal', 'crown', 'corona', 'stone', 'piedra', 'shard', 'esquirla'] },
      { id: 'orbiterC', name: 'OrbiterRight', parent: 'crown', optional: true, roles: ['orbiter'],
        label: 'Orbital 3', hint: 'Otra pieza flotante (opcional).',
        pivot: [0.5, 0.5], center: [0.5, 0.5], at: [1.6, 0.3], size: [1.1, 1.8], pointAngle: 90,
        sway: { move: [0, 0.22], rotate: 6, speed: 2.6, phase: 4.2 },
        match: ['orbiter', 'crystal', 'cristal', 'crown', 'corona', 'stone', 'piedra', 'shard', 'esquirla'] },
      { id: 'leftLimb', name: 'LeftLimb', parent: 'torso', roles: ['limb'],
        label: 'Extremidad izquierda', hint: 'Puño / mano / guantelete que los ataques lanzan, dibujado con los nudillos hacia abajo (frente al lanzar = −90°). Pivote arriba, agarre en los nudillos.',
        pivot: [0.5, 0.1], center: [0.5, 0.7], grip: [0.5, 0.7], at: [-6.5, -1], size: [3.5, 4.5],
        pointAngle: -90, leanFollow: 0.4,
        sway: { move: [0.1, 0.35], rotate: 5, speed: 1.5, phase: 0 },
        match: ['leftlimb', 'leftfist', 'fistleft', 'leftarm', 'lefthand', 'punoizq', 'manoizq', 'brazoizq', 'fist', 'puno', 'hand', 'mano', 'arm', 'brazo', 'gauntlet', 'guantelete'] },
      { id: 'rightLimb', name: 'RightLimb', parent: 'torso', roles: ['limb'],
        label: 'Extremidad derecha', hint: 'La otra extremidad lanzable: se balancea a contratiempo.',
        pivot: [0.5, 0.1], center: [0.5, 0.7], grip: [0.5, 0.7], at: [6.5, -1], size: [3.5, 4.5],
        pointAngle: -90, leanFollow: 0.4,
        sway: { move: [-0.1, 0.35], rotate: 5, speed: 1.5, phase: 3.14159 },
        match: ['rightlimb', 'rightfist', 'fistright', 'rightarm', 'righthand', 'punoder', 'manoder', 'brazoder', 'fist', 'puno', 'hand', 'mano', 'arm', 'brazo', 'gauntlet', 'guantelete'] },
    ],
    // The gesture names the rig attacks and the shared boss attacks already use (BossAttack.gesture).
    // A hanging part rotated NEGATIVE swings back (facing mirrors it for free).
    gestures: [
      { name: 'Brace', strikeTime: 0.08, recover: 0.4, shake: 2,
        windup: [pose('Visual', [0, -0.66], 3), pose('crown', [0, 1.2], 0)],
        strike: [pose('Visual', [0, 0.26], -2)] },
      { name: 'Dash', strikeTime: 0.1, recover: 1.2, shake: 2,
        windup: [pose('Visual', [-1.3, 0.5], 8), pose('leftLimb', [-2, 1], -20), pose('rightLimb', [-2, 1], -20), pose('lower')],
        strike: [] },
      { name: 'DashLoop', strikeTime: 0.1, recover: 0.35, shake: 0,
        windup: [pose('Visual', [0.8, -0.5], -22), pose('leftLimb', [-3, 1], -25), pose('rightLimb', [-3, 1], -25),
          pose('lower', [0, 0], -25), pose('crown', [-1, 0], 10)],
        strike: [] },
      { name: 'Move', strikeTime: 0.1, recover: 0.3, shake: 0,
        windup: [pose('Visual', [0, 0], -6), pose('leftLimb', [-1.2, 0.3], -15), pose('rightLimb', [-1.2, 0.3], -15),
          pose('lower', [0, 0], -12), pose('crown', [-0.6, 0], 4)],
        strike: [] },
      { name: 'GroundSlam', strikeTime: 0.07, recover: 0.5, shake: 2.5,
        windup: [pose('Visual', [0, 2.1], 0, [-0.04, 0.05]), pose('crown', [0, 2], 0),
          pose('leftLimb', [1, 5], -25), pose('rightLimb', [-1, 5], 25)],
        strike: [pose('Visual', [0, -1.6], 0, [0.08, -0.08]), pose('crown', [0, -1], 0),
          pose('leftLimb', [1, -3], 10), pose('rightLimb', [-1, -3], -10)] },
      { name: 'PickUP', strikeTime: 0.12, recover: 0.35, shake: 1,
        windup: [pose('Visual', [0, -0.8], -6), pose('rightLimb', [2, -3], 20)],
        strike: [pose('Visual', [0, 0], 4), pose('rightLimb', [0, 5], 30)] },
      { name: 'Throw', strikeTime: 0.08, recover: 0.4, shake: 1.5,
        windup: [pose('Visual', [0, 0], 8), pose('rightLimb', [-2.5, 4], -40)],
        strike: [pose('Visual', [0.5, 0], -10), pose('rightLimb', [4, 1], 40)] },
    ],
    behaviour: {
      summary: 'Jefe con la baraja de ataques de partes: lanzar puño, mazazos, tenaza y picado de orbitales (gesto «Brace»). Móntala en 👑 Boss Creator (grupo "De partes") y enlázala abajo.',
    },
  },
];

export function templateById(id) {
  return RIG_TEMPLATES.find((t) => t.id === id) || null;
}

// ============================================================ template → document

const xy = (a, fallback = [0, 0]) => ({ x: (a || fallback)[0], y: (a || fallback)[1] });

/** A new composer document with every slot of `template` as an empty part, roles and defaults set. */
export function instantiateTemplate(template) {
  const state = createDefaultRigState();
  state.kind = template.kind;
  // `height`: the body height the document's lengths are written for right now (see fitLengths).
  state.template = { id: template.id, height: template.height, fitted: false };
  Object.assign(state.rig, JSON.parse(JSON.stringify(template.rig || {})));
  ['corpse', 'collider', 'bossLimbs', 'bossBody'].forEach((k) => {
    if (template[k]) Object.assign(state[k], JSON.parse(JSON.stringify(template[k])));
  });

  const uidOf = new Map();
  template.slots.forEach((slot, index) => {
    const part = createPart({ name: slot.name, index });
    uidOf.set(slot.id, part.uid);
    part.position = xy(slot.at);
    if (slot.rigPart === false) part.rigPart = false;
    if (slot.sway) {
      const s = slot.sway;
      if (s.move) part.sway.move = xy(s.move);
      if (s.scale) part.sway.scale = xy(s.scale);
      ['rotate', 'speed', 'phase'].forEach((k) => { if (s[k] !== undefined) part.sway[k] = s[k]; });
    }
    ['leanFollow', 'pointAngle', 'breaksOnDeath'].forEach((k) => { if (slot[k] !== undefined) part[k] = slot[k]; });
    if (!slot.node) {
      part.slot = {
        id: slot.id, label: slot.label, hint: slot.hint || '', optional: !!slot.optional,
        pivot: slot.pivot || [0.5, 0.5], size: [...(slot.size || [1, 1])], match: slot.match || [],
        states: !!slot.states, stage: !!slot.stage,
      };
      ['muzzle', 'grip', 'center', 'barrel'].forEach((k) => { if (slot[k] !== undefined) part.slot[k] = slot[k]; });
    }
    state.parts.push(part);
  });

  template.slots.forEach((slot) => {
    const uid = uidOf.get(slot.id);
    if (slot.parent) byUid(state, uid).parent = uidOf.get(slot.parent) || null;
    (slot.roles || []).forEach((role) => {
      const r = state.roles;
      if (role === 'aim') r.aim = uid;
      else if (role === 'stretch') r.stretch = uid;
      else if (role === 'rider') r.riders.push(uid);
      else if (role === 'limb') r.limbs.push(uid);
      else if (role === 'orbiter') r.orbiters.push(uid);
      else if (role === 'body') state.bossBody.bodyRenderer = uid;
    });
  });

  const poses = (list) => (list || []).map((p) => ({
    part: p.part === 'Visual' ? 'Visual' : uidOf.get(p.part), move: xy(p.move), rotate: p.rotate || 0, scale: xy(p.scale),
  })).filter((p) => p.part);
  state.gestures = (template.gestures || []).map((g) => ({
    name: g.name, strikeTime: g.strikeTime, recover: g.recover, shake: g.shake,
    windup: poses(g.windup), strike: poses(g.strike),
  }));
  return state;
}

// ============================================================ filling slots

/** Slots still empty: `required` only = the ones the shape cannot do without. */
export function openSlots(state, requiredOnly = false) {
  return state.parts.filter((p) => isOpenSlot(p) && !(requiredOnly && p.slot.optional));
}

/** The open slot a dropped file names (longest keyword in its file name wins), or null. */
export function matchSlot(state, fileName) {
  const name = fileName.toLowerCase().normalize('NFD').replace(/[^a-z0-9]/g, '');
  let best = null, bestScore = 0;
  openSlots(state).forEach((part) => {
    const score = Math.max(0, ...part.slot.match.filter((k) => name.includes(k)).map((k) => k.length));
    if (score > bestScore) { best = part; bestScore = score; }
  });
  return best;
}

/** A normalized point of the trimmed image ([x from left, y from top]) → original-canvas pixels. */
function slotPixel(image, n) {
  return { x: image.cropX + n[0] * image.w, y: image.cropY + n[1] * image.h };
}

/**
 * Puts `image` into an open slot with the template's guesses: pivot, muzzle / grip / centre.
 * Placement: a layer cut from the same canvas as an already placed part lands where that canvas
 * puts it (so a set of layers assembles itself); anything else stays at the slot's schematic spot.
 * The very first image sizes the whole schematic to the art. Children never move on screen.
 */
export function fillSlot(state, part, image) {
  const slot = part.slot;
  const ppu = state.pixelsPerUnit;

  if (!state.parts.some((p) => p.image) && slot.size[1] > 0) {
    const k = image.h / ppu / slot.size[1];
    state.parts.forEach((p) => {
      p.position = { x: p.position.x * k, y: p.position.y * k };
      if (p.slot) p.slot.size = [p.slot.size[0] * k, p.slot.size[1] * k];
    });
  }

  const layered = image.w < image.origW || image.h < image.origH;
  const sibling = layered && state.parts.find((p) => p !== part && p.image
    && p.image.origW === image.origW && p.image.origH === image.origH);
  const kids = childrenOf(state, part.uid).map((c) => [c, worldMatrix(state, c)]);

  part.image = image;
  part.pivot = slotPixel(image, slot.pivot);
  if (sibling) {
    const world = apply(worldMatrix(state, sibling), pixelToLocal(state, sibling, part.pivot));
    const parent = byUid(state, part.parent);
    part.position = parent ? apply(invert(worldMatrix(state, parent)), world) : world;
  }

  const inverse = invert(worldMatrix(state, part));
  kids.forEach(([child, world]) => {
    const d = decompose(multiply(inverse, world));
    child.position = d.position;
    child.rotation = d.rotation;
    child.scale = d.scale;
  });

  if (slot.center) part.center = slotPixel(image, slot.center);
  if (slot.muzzle && state.roles.aim === part.uid) {
    state.roles.muzzle = slotPixel(image, slot.muzzle);
    if (slot.barrel !== undefined) state.rig.aimArtAngle = slot.barrel;
    else {
      const local = pixelToLocal(state, part, state.roles.muzzle);
      state.rig.aimArtAngle = Math.round((Math.atan2(local.y, local.x) * 180) / Math.PI * 10) / 10;
    }
  }
  if (slot.grip && state.roles.limbs.includes(part.uid)) state.roles.grips[part.uid] = slotPixel(image, slot.grip);
}

// ============================================================ lengths follow the art's size

const RIG_LENGTHS = ['hoverBob', 'followMaxOffset', 'recoilDistance', 'bodyRecoil', 'foldedRiderOffset', 'muzzleFlashSize'];
const r4 = (v) => Math.round(v * 1e4) / 1e4;
const scaled = (v, k) => (typeof v === 'number' ? r4(v * k) : { x: r4(v.x * k), y: r4(v.y * k) });

/**
 * Rescales every length of the document (part sway moves, the rig's distances, gesture moves) so a
 * template written for a body `state.template.height` units tall matches one `bodyHeight` tall.
 * Angles, speeds and times are size-independent and stay. Returns the factor applied.
 */
export function fitLengths(state, bodyHeight) {
  if (!state.template || !(bodyHeight > 0) || !(state.template.height > 0)) return 1;
  const k = bodyHeight / state.template.height;
  state.parts.forEach((p) => { p.sway.move = scaled(p.sway.move, k); });
  RIG_LENGTHS.forEach((key) => { if (state.rig[key] !== undefined) state.rig[key] = scaled(state.rig[key], k); });
  (state.gestures || []).forEach((g) => [...g.windup, ...g.strike].forEach((p) => { p.move = scaled(p.move, k); }));
  state.template.height = bodyHeight;
  state.template.fitted = true;
  return k;
}

// ============================================================ suggested behaviour

/**
 * The EnemyConfig a template suggests (same shape Enemy Creator saves on the rig record), or null
 * (bosses: their behaviour is a Boss Creator entry). The projectile has no prefab: it is the
 * code-built placeholder until the user picks one in Enemy Creator.
 */
export function suggestedEnemyConfig(state) {
  const b = templateById(state.template?.id)?.behaviour;
  if (!b?.tuning || state.kind !== 'enemy') return null;
  const cfg = createDefaultEnemyConfig();
  cfg.enemyName = state.name.trim();
  Object.assign(cfg.tuning, JSON.parse(JSON.stringify(b.tuning)));
  if (b.projectile) {
    cfg.tuning.projectileMode = 'inline';
    Object.assign(cfg.tuning.projectileSpec, JSON.parse(JSON.stringify(b.projectile)));
  }
  return buildExportObject(cfg);
}
