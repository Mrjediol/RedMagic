// modules/vfx-sounds.js
// -----------------------------------------------------------------------------
// EXAMPLE SOUNDS for the ✨ VFX Lab effects, synthesised in code — the audio twin of the presets:
// each sound is DATA (a few layers: tones that glide, filtered noise, pings, clicks) rendered to
// samples by one small synthesiser. No files, no dependencies, DOM-free: the Lab plays them through
// Web Audio ("▶ Probar"), and tools/bake-sounds.mjs writes the same samples as .wav for Unity.
//
// They are PLACEHOLDERS with the right shape (a fire whoosh sounds like fire, an ice hit like
// glass), meant to be judged next to the effect and replaced by real recordings when there are any.
//
//   SOUNDS[id]            = { label, seconds, loop?, layers: [...] }
//   PRESET_SOUNDS[preset] = { cast?, <Lane name>: soundId }     which sound each moment of a preset plays
//     `cast` = the instant a projectile leaves; a lane name = when that lane starts. A looping lane's
//     sound must be a `loop` sound: it repeats while the lane does.
//   renderSound(id)       → Float32Array (mono, SAMPLE_RATE), cached
//   encodeWav(samples)    → Uint8Array (16-bit PCM)
//
// Layers (times in seconds; `f: [from, to]` glides exponentially over the layer):
//   tone   { wave: 'sine' | 'tri' | 'saw' | 'square', f, t0, dur, a (attack), curve (decay shape), gain, vib: [Hz, depth], trem: [Hz, depth] }
//          + fm: [ratio, depth]   a modulator at f × ratio bends the phase (metal, bells, coins); it fades with the layer
//   noise  { filter: 'lp' | 'bp' | 'hp', f, q, t0, dur, a, curve, gain, trem }
//   pings  { count, f: [low, high], t0, spread, dur, gain, rise }   short sine pings at random times / pitches
//   clicks { count, t0, spread, gain }                              tiny noise impulses (crackle, debris)
// In a `loop` sound every layer is sustained (no envelope) and the end is cross-faded into the start.
//
// Adding a sound = one entry in SOUNDS; giving it to an effect = one entry in PRESET_SOUNDS.
// The GAME's sounds (modules/game-sounds.js) are the same data rendered by this same synthesiser:
// `renderSound(id, def)` takes a definition that lives outside SOUNDS.

export const SAMPLE_RATE = 44100;

export const T = (wave, f, o = {}) => ({ type: 'tone', wave, f, ...o });
export const N = (filter, f, o = {}) => ({ type: 'noise', filter, f, ...o });
export const P = (count, f, o = {}) => ({ type: 'pings', count, f, ...o });
export const C = (count, o = {}) => ({ type: 'clicks', count, ...o });

// ============================================================ the sounds

export const SOUNDS = {
  // ---------------------------------------------------------------- a projectile leaves
  castFire: { label: 'Fuego · lanzar', seconds: 0.45, layers: [
    N('bp', [500, 1700], { q: 0.9, dur: 0.4, a: 0.05, curve: 1.6, gain: 0.9 }),
    N('lp', [320, 140], { dur: 0.45, a: 0.02, curve: 2, gain: 0.7 }),
    T('sine', [150, 70], { dur: 0.3, a: 0.005, curve: 2.5, gain: 0.5 }) ] },
  castIce: { label: 'Hielo · lanzar', seconds: 0.4, layers: [
    N('bp', [5200, 3200], { q: 0.5, dur: 0.35, a: 0.01, curve: 2, gain: 0.5 }),
    T('sine', [3400, 1700], { dur: 0.22, a: 0.003, curve: 2.2, gain: 0.45 }),
    T('tri', [2300, 2900], { dur: 0.3, t0: 0.03, a: 0.01, curve: 3, gain: 0.25 }) ] },
  castPoison: { label: 'Veneno · lanzar', seconds: 0.4, layers: [
    T('sine', [340, 120], { dur: 0.3, a: 0.01, curve: 1.6, gain: 0.8, vib: [22, 0.12] }),
    N('lp', [900, 300], { dur: 0.35, a: 0.02, curve: 2, gain: 0.5 }),
    P(4, [500, 1100], { t0: 0.05, spread: 0.25, dur: 0.05, gain: 0.3, rise: 1.5 }) ] },
  castZap: { label: 'Rayo · lanzar', seconds: 0.3, layers: [
    T('saw', [2100, 240], { dur: 0.22, a: 0.002, curve: 1.8, gain: 0.55 }),
    N('hp', [4200, 2400], { q: 0.7, dur: 0.14, a: 0.001, curve: 2.5, gain: 0.5 }),
    C(14, { t0: 0.02, spread: 0.24, gain: 0.5 }) ] },
  castArcane: { label: 'Arcano · lanzar', seconds: 0.5, layers: [
    T('sine', [520, 1040], { dur: 0.4, a: 0.02, curve: 1.8, gain: 0.5 }),
    T('sine', [780, 1560], { dur: 0.4, t0: 0.02, a: 0.02, curve: 2, gain: 0.35 }),
    T('tri', [1300, 2600], { dur: 0.3, t0: 0.05, a: 0.02, curve: 2.5, gain: 0.2 }),
    P(6, [2400, 4800], { t0: 0.08, spread: 0.3, dur: 0.07, gain: 0.18 }) ] },
  castShadow: { label: 'Sombra · lanzar', seconds: 0.55, layers: [
    T('sine', [70, 130], { dur: 0.5, a: 0.25, curve: 1.2, gain: 0.8 }),
    N('lp', [200, 700], { dur: 0.5, a: 0.3, curve: 1.5, gain: 0.7 }),
    T('saw', [55, 82], { dur: 0.45, a: 0.2, curve: 1.5, gain: 0.25, trem: [14, 0.6] }) ] },
  castRock: { label: 'Roca · lanzar', seconds: 0.4, layers: [
    N('lp', [420, 180], { dur: 0.35, a: 0.03, curve: 1.6, gain: 0.9 }),
    T('sine', [110, 60], { dur: 0.25, a: 0.005, curve: 2.2, gain: 0.7 }),
    C(5, { t0: 0.02, spread: 0.2, gain: 0.3 }) ] },
  castNeedle: { label: 'Aguja · lanzar', seconds: 0.2, layers: [
    T('sine', [2600, 900], { dur: 0.13, a: 0.002, curve: 2.2, gain: 0.6 }),
    N('hp', [6000, 3500], { q: 0.8, dur: 0.07, a: 0.001, curve: 3, gain: 0.4 }) ] },
  castBlade: { label: 'Cuchilla · lanzar', seconds: 0.45, layers: [
    N('bp', [2600, 3600], { q: 0.35, dur: 0.42, a: 0.04, curve: 1.4, gain: 0.7, trem: [26, 0.75] }),
    T('saw', [1400, 1750], { dur: 0.4, a: 0.03, curve: 1.6, gain: 0.2, trem: [26, 0.75] }) ] },
  castHeavy: { label: 'Orbe pesado · lanzar', seconds: 0.7, layers: [
    T('sine', [95, 140], { dur: 0.65, a: 0.15, curve: 1.3, gain: 0.8, trem: [9, 0.35] }),
    T('saw', [140, 210], { dur: 0.6, a: 0.2, curve: 1.6, gain: 0.25 }),
    N('lp', [260, 520], { dur: 0.6, a: 0.2, curve: 1.6, gain: 0.5 }) ] },
  castWisp: { label: 'Fuego fatuo · lanzar', seconds: 0.5, layers: [
    T('sine', [760, 1250], { dur: 0.45, a: 0.08, curve: 1.6, gain: 0.5, vib: [7, 0.04] }),
    T('sine', [1520, 2500], { dur: 0.4, a: 0.1, curve: 2, gain: 0.2, vib: [7, 0.04] }),
    N('hp', [3800, 5200], { q: 0.9, dur: 0.4, a: 0.1, curve: 2, gain: 0.2 }) ] },

  // ---------------------------------------------------------------- something is hit
  hitFire: { label: 'Fuego · impacto', seconds: 0.6, layers: [
    N('lp', [1500, 180], { dur: 0.5, a: 0.002, curve: 1.8, gain: 1 }),
    T('sine', [130, 42], { dur: 0.4, a: 0.002, curve: 2, gain: 0.9 }),
    C(10, { t0: 0.08, spread: 0.4, gain: 0.25 }) ] },
  explosion: { label: 'Explosión', seconds: 1.1, layers: [
    N('lp', [2200, 110], { dur: 1, a: 0.002, curve: 1.6, gain: 1 }),
    T('sine', [110, 30], { dur: 0.7, a: 0.002, curve: 1.8, gain: 1 }),
    N('bp', [700, 250], { q: 0.8, dur: 0.6, t0: 0.03, a: 0.01, curve: 1.5, gain: 0.5 }),
    C(18, { t0: 0.15, spread: 0.8, gain: 0.22 }) ] },
  hitIce: { label: 'Hielo · impacto', seconds: 0.55, layers: [
    N('hp', [6000, 3000], { q: 0.7, dur: 0.12, a: 0.001, curve: 2.2, gain: 0.7 }),
    P(11, [2200, 6200], { t0: 0, spread: 0.3, dur: 0.11, gain: 0.5 }),
    T('sine', [1900, 1400], { dur: 0.18, a: 0.001, curve: 2.5, gain: 0.3 }) ] },
  hitSplash: { label: 'Salpicadura', seconds: 0.5, layers: [
    N('bp', [1100, 320], { q: 0.7, dur: 0.3, a: 0.003, curve: 1.6, gain: 0.9 }),
    N('lp', [500, 200], { dur: 0.4, a: 0.01, curve: 2, gain: 0.5 }),
    P(7, [350, 1200], { t0: 0.05, spread: 0.38, dur: 0.06, gain: 0.4, rise: 1.8 }) ] },
  hitZap: { label: 'Rayo · impacto', seconds: 0.45, layers: [
    N('hp', [3000, 5000], { q: 0.6, dur: 0.1, a: 0.001, curve: 2, gain: 0.9 }),
    T('saw', [1300, 110], { dur: 0.3, a: 0.001, curve: 1.6, gain: 0.5 }),
    C(26, { t0: 0.03, spread: 0.38, gain: 0.5 }) ] },
  hitArcane: { label: 'Arcano · impacto', seconds: 0.8, layers: [
    T('sine', [880, 880], { dur: 0.7, a: 0.003, curve: 2.4, gain: 0.5 }),
    T('sine', [1320, 1320], { dur: 0.6, a: 0.003, curve: 2.6, gain: 0.35 }),
    T('sine', [2217, 2217], { dur: 0.45, a: 0.003, curve: 3, gain: 0.22 }),
    N('bp', [1800, 700], { q: 0.8, dur: 0.18, a: 0.002, curve: 2, gain: 0.45 }),
    P(8, [2600, 5200], { t0: 0.04, spread: 0.45, dur: 0.08, gain: 0.16 }) ] },
  hitShadow: { label: 'Sombra · impacto', seconds: 0.7, layers: [
    T('sine', [120, 38], { dur: 0.55, a: 0.003, curve: 1.6, gain: 1 }),
    N('lp', [900, 150], { dur: 0.6, a: 0.01, curve: 1.5, gain: 0.8 }),
    T('saw', [220, 60], { dur: 0.4, a: 0.01, curve: 2, gain: 0.2, trem: [18, 0.6] }) ] },
  hitRock: { label: 'Roca · impacto', seconds: 0.55, layers: [
    N('lp', [1100, 220], { dur: 0.25, a: 0.001, curve: 2, gain: 1 }),
    T('sine', [95, 48], { dur: 0.3, a: 0.001, curve: 2.2, gain: 0.9 }),
    C(12, { t0: 0.06, spread: 0.42, gain: 0.45 }),
    N('bp', [1700, 900], { q: 0.9, dur: 0.3, t0: 0.05, a: 0.02, curve: 1.6, gain: 0.25 }) ] },
  hitHeavy: { label: 'Orbe pesado · impacto', seconds: 0.9, layers: [
    T('sine', [150, 34], { dur: 0.75, a: 0.002, curve: 1.6, gain: 1 }),
    N('lp', [1600, 140], { dur: 0.8, a: 0.003, curve: 1.6, gain: 0.8 }),
    T('sine', [620, 310], { dur: 0.5, a: 0.003, curve: 2.4, gain: 0.25 }) ] },
  hitWisp: { label: 'Fuego fatuo · impacto', seconds: 0.5, layers: [
    T('sine', [1250, 620], { dur: 0.4, a: 0.005, curve: 2, gain: 0.5 }),
    N('hp', [4200, 2600], { q: 0.9, dur: 0.3, a: 0.01, curve: 2, gain: 0.3 }),
    P(6, [1800, 3600], { t0: 0.03, spread: 0.35, dur: 0.07, gain: 0.25 }) ] },
  hitSpark: { label: 'Chispazo', seconds: 0.18, layers: [
    N('hp', [5200, 2800], { q: 0.7, dur: 0.06, a: 0.001, curve: 2.4, gain: 0.8 }),
    T('sine', [1900, 620], { dur: 0.1, a: 0.001, curve: 2.4, gain: 0.5 }),
    T('sine', [240, 120], { dur: 0.08, a: 0.001, curve: 2.5, gain: 0.4 }) ] },
  hitSlash: { label: 'Tajo', seconds: 0.28, layers: [
    N('bp', [6200, 1500], { q: 0.5, dur: 0.2, a: 0.012, curve: 1.4, gain: 0.9 }),
    N('hp', [7000, 5000], { q: 0.9, dur: 0.1, t0: 0.02, a: 0.01, curve: 2, gain: 0.35 }),
    T('saw', [2400, 900], { dur: 0.12, t0: 0.02, a: 0.01, curve: 2.4, gain: 0.12 }) ] },
  thunder: { label: 'Rayo caído', seconds: 1.3, layers: [
    N('hp', [2600, 5200], { q: 0.6, dur: 0.09, a: 0.001, curve: 1.6, gain: 1 }),
    C(30, { t0: 0, spread: 0.3, gain: 0.6 }),
    N('lp', [900, 90], { dur: 1.2, t0: 0.04, a: 0.01, curve: 1.5, gain: 0.9 }),
    T('sine', [85, 32], { dur: 0.9, t0: 0.04, a: 0.005, curve: 1.6, gain: 0.9 }) ] },
  dust: { label: 'Polvareda', seconds: 0.5, layers: [
    N('lp', [700, 260], { dur: 0.45, a: 0.02, curve: 1.6, gain: 0.9 }),
    T('sine', [80, 50], { dur: 0.18, a: 0.002, curve: 2.4, gain: 0.7 }),
    C(6, { t0: 0.05, spread: 0.3, gain: 0.2 }) ] },
  step: { label: 'Pisada', seconds: 0.2, layers: [
    N('lp', [600, 280], { dur: 0.16, a: 0.005, curve: 2, gain: 0.8 }),
    T('sine', [90, 60], { dur: 0.07, a: 0.002, curve: 2.5, gain: 0.4 }) ] },
  heal: { label: 'Cura', seconds: 0.9, layers: [
    T('sine', [523, 523], { dur: 0.5, t0: 0, a: 0.01, curve: 2.2, gain: 0.45 }),
    T('sine', [659, 659], { dur: 0.5, t0: 0.09, a: 0.01, curve: 2.2, gain: 0.45 }),
    T('sine', [784, 784], { dur: 0.5, t0: 0.18, a: 0.01, curve: 2.2, gain: 0.45 }),
    T('sine', [1047, 1047], { dur: 0.6, t0: 0.27, a: 0.01, curve: 2, gain: 0.5 }),
    T('tri', [2093, 2093], { dur: 0.45, t0: 0.27, a: 0.01, curve: 2.6, gain: 0.12 }),
    P(8, [2400, 4200], { t0: 0.2, spread: 0.55, dur: 0.08, gain: 0.12 }) ] },
  shieldBreak: { label: 'Escudo roto', seconds: 0.7, layers: [
    N('hp', [4500, 6500], { q: 0.6, dur: 0.07, a: 0.001, curve: 2, gain: 0.9 }),
    P(14, [1600, 5200], { t0: 0.01, spread: 0.42, dur: 0.1, gain: 0.45 }),
    T('sine', [900, 180], { dur: 0.5, a: 0.003, curve: 1.8, gain: 0.45 }),
    T('square', [450, 110], { dur: 0.35, a: 0.003, curve: 2.2, gain: 0.12 }) ] },
  shockwave: { label: 'Onda de choque', seconds: 0.9, layers: [
    T('sine', [140, 30], { dur: 0.8, a: 0.003, curve: 1.5, gain: 1 }),
    N('lp', [1200, 120], { dur: 0.85, a: 0.004, curve: 1.5, gain: 0.8 }),
    N('bp', [300, 1200], { q: 0.7, dur: 0.5, t0: 0.05, a: 0.1, curve: 1.6, gain: 0.3 }) ] },
  muzzle: { label: 'Fogonazo', seconds: 0.22, layers: [
    N('bp', [2400, 600], { q: 0.6, dur: 0.14, a: 0.001, curve: 2, gain: 0.9 }),
    T('sine', [420, 90], { dur: 0.16, a: 0.001, curve: 2.2, gain: 0.7 }) ] },
  puff: { label: 'Nube', seconds: 0.5, layers: [
    N('bp', [500, 1100], { q: 0.8, dur: 0.45, a: 0.04, curve: 1.6, gain: 0.8 }),
    N('lp', [380, 200], { dur: 0.4, a: 0.03, curve: 2, gain: 0.5 }),
    T('sine', [200, 320], { dur: 0.2, a: 0.02, curve: 2, gain: 0.2 }) ] },
  teleport: { label: 'Teletransporte', seconds: 0.75, layers: [
    T('sine', [280, 2600], { dur: 0.55, a: 0.04, curve: 1.2, gain: 0.5 }),
    T('tri', [560, 5200], { dur: 0.55, a: 0.06, curve: 1.4, gain: 0.18 }),
    N('bp', [900, 4800], { q: 0.6, dur: 0.55, a: 0.1, curve: 1.3, gain: 0.45 }),
    N('hp', [5000, 3000], { q: 0.8, dur: 0.12, t0: 0.55, a: 0.002, curve: 2.4, gain: 0.5 }),
    P(8, [2600, 5400], { t0: 0.3, spread: 0.4, dur: 0.07, gain: 0.16 }) ] },

  // ---------------------------------------------------------------- something starts
  chargeUp: { label: 'Carga · empezar', seconds: 0.85, layers: [
    T('sine', [180, 760], { dur: 0.8, a: 0.3, curve: 0.7, gain: 0.55 }),
    T('saw', [90, 380], { dur: 0.8, a: 0.4, curve: 0.8, gain: 0.16 }),
    N('bp', [500, 3200], { q: 0.7, dur: 0.8, a: 0.4, curve: 0.8, gain: 0.4 }),
    P(8, [1800, 3800], { t0: 0.25, spread: 0.55, dur: 0.06, gain: 0.14 }) ] },
  buffUp: { label: 'Mejora · empezar', seconds: 0.7, layers: [
    T('sine', [392, 392], { dur: 0.5, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [587, 587], { dur: 0.5, t0: 0.08, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [784, 1176], { dur: 0.5, t0: 0.16, a: 0.02, curve: 1.8, gain: 0.4 }),
    N('hp', [3000, 6000], { q: 0.9, dur: 0.5, t0: 0.1, a: 0.15, curve: 1.6, gain: 0.25 }) ] },
  shieldUp: { label: 'Escudo · levantar', seconds: 0.55, layers: [
    T('sine', [220, 440], { dur: 0.45, a: 0.05, curve: 1.6, gain: 0.55 }),
    T('sine', [330, 660], { dur: 0.45, a: 0.05, curve: 1.8, gain: 0.35 }),
    N('bp', [700, 2600], { q: 0.7, dur: 0.4, a: 0.1, curve: 1.6, gain: 0.35 }) ] },
  markLock: { label: 'Marca · fijar', seconds: 0.4, layers: [
    T('square', [880, 880], { dur: 0.07, a: 0.002, curve: 1.2, gain: 0.22 }),
    T('square', [1320, 1320], { dur: 0.07, t0: 0.11, a: 0.002, curve: 1.2, gain: 0.22 }),
    T('sine', [1760, 1760], { dur: 0.22, t0: 0.11, a: 0.002, curve: 2.4, gain: 0.5 }) ] },
  warn: { label: 'Aviso', seconds: 0.5, layers: [
    T('square', [620, 620], { dur: 0.12, a: 0.004, curve: 0.8, gain: 0.2 }),
    T('sine', [620, 620], { dur: 0.14, a: 0.004, curve: 1, gain: 0.5 }),
    T('square', [620, 620], { dur: 0.12, t0: 0.2, a: 0.004, curve: 0.8, gain: 0.2 }),
    T('sine', [620, 620], { dur: 0.2, t0: 0.2, a: 0.004, curve: 1.4, gain: 0.5 }) ] },
  summon: { label: 'Invocación · abrir', seconds: 0.9, layers: [
    T('sine', [65, 98], { dur: 0.85, a: 0.3, curve: 1.2, gain: 0.8 }),
    T('saw', [98, 147], { dur: 0.8, a: 0.35, curve: 1.4, gain: 0.2, trem: [7, 0.5] }),
    N('bp', [300, 1500], { q: 0.6, dur: 0.8, a: 0.4, curve: 1.3, gain: 0.5 }),
    P(6, [900, 1800], { t0: 0.3, spread: 0.5, dur: 0.12, gain: 0.16 }) ] },
  spikes: { label: 'Púas · brotar', seconds: 0.45, layers: [
    N('hp', [3000, 6500], { q: 0.6, dur: 0.2, a: 0.004, curve: 1.6, gain: 0.7 }),
    P(9, [1500, 5200], { t0: 0, spread: 0.25, dur: 0.09, gain: 0.45, rise: 1.3 }),
    T('sine', [160, 90], { dur: 0.15, a: 0.002, curve: 2.2, gain: 0.5 }) ] },
  beamStart: { label: 'Rayo · abrir', seconds: 0.35, layers: [
    T('saw', [220, 110], { dur: 0.3, a: 0.01, curve: 1.6, gain: 0.4 }),
    T('sine', [1800, 880], { dur: 0.25, a: 0.004, curve: 2, gain: 0.4 }),
    N('bp', [3000, 1200], { q: 0.6, dur: 0.25, a: 0.004, curve: 1.8, gain: 0.5 }) ] },
  beamEnd: { label: 'Rayo · cerrar', seconds: 0.3, layers: [
    T('saw', [110, 55], { dur: 0.28, a: 0.005, curve: 1.6, gain: 0.4 }),
    T('sine', [880, 220], { dur: 0.25, a: 0.004, curve: 1.8, gain: 0.35 }),
    N('lp', [1200, 300], { dur: 0.25, a: 0.004, curve: 1.8, gain: 0.4 }) ] },

  // ---------------------------------------------------------------- sustained (loops)
  loopFire: { label: 'Fuego · bucle', seconds: 1.2, loop: true, layers: [
    N('lp', [420, 420], { gain: 0.7, trem: [5, 0.3] }),
    N('bp', [1500, 1500], { q: 0.9, gain: 0.3, trem: [11, 0.5] }),
    C(26, { spread: 1.2, gain: 0.55 }) ] },
  loopFrost: { label: 'Hielo · bucle', seconds: 1.5, loop: true, layers: [
    N('bp', [4800, 4800], { q: 0.4, gain: 0.16, trem: [2, 0.5] }),
    T('sine', [1760, 1760], { gain: 0.1, trem: [2, 0.7] }),
    P(7, [2600, 5600], { spread: 1.4, dur: 0.12, gain: 0.3 }) ] },
  loopPoison: { label: 'Veneno · bucle', seconds: 1.5, loop: true, layers: [
    N('lp', [360, 360], { gain: 0.45, trem: [3, 0.4] }),
    P(11, [260, 900], { spread: 1.4, dur: 0.07, gain: 0.6, rise: 1.9 }) ] },
  loopCharge: { label: 'Carga · bucle', seconds: 1, loop: true, layers: [
    T('sine', [220, 220], { gain: 0.45, trem: [8, 0.45] }),
    T('saw', [110, 110], { gain: 0.14, trem: [8, 0.45] }),
    T('sine', [660, 660], { gain: 0.18, trem: [16, 0.5] }),
    N('bp', [2200, 2200], { q: 0.6, gain: 0.3, trem: [8, 0.6] }) ] },
  loopAura: { label: 'Aura · bucle', seconds: 1.5, loop: true, layers: [
    T('sine', [392, 392], { gain: 0.3, trem: [2, 0.4] }),
    T('sine', [588, 588], { gain: 0.22, trem: [3, 0.4] }),
    N('bp', [4200, 4200], { q: 0.4, gain: 0.08, trem: [4, 0.6] }),
    P(6, [2000, 3600], { spread: 1.4, dur: 0.1, gain: 0.16 }) ] },
  loopShield: { label: 'Escudo · bucle', seconds: 1, loop: true, layers: [
    T('sine', [220, 220], { gain: 0.45 }),
    T('sine', [223, 223], { gain: 0.45 }),
    T('sine', [440, 440], { gain: 0.16, trem: [3, 0.4] }),
    N('bp', [1400, 1400], { q: 0.9, gain: 0.12 }) ] },
  loopBeam: { label: 'Rayo · bucle', seconds: 1, loop: true, layers: [
    T('saw', [110, 110], { gain: 0.3, trem: [28, 0.3] }),
    T('saw', [112, 112], { gain: 0.3 }),
    T('sine', [880, 880], { gain: 0.16, trem: [14, 0.5] }),
    N('bp', [2600, 2600], { q: 0.5, gain: 0.4, trem: [28, 0.4] }) ] },
  loopStun: { label: 'Aturdido · bucle', seconds: 1, loop: true, layers: [
    T('sine', [1320, 1320], { gain: 0.14, trem: [3, 0.9] }),
    P(6, [1500, 2600], { spread: 0.95, dur: 0.09, gain: 0.5 }) ] },
  loopMark: { label: 'Marca · bucle', seconds: 1, loop: true, layers: [
    T('sine', [1175, 1175], { gain: 0.22, trem: [2, 0.95] }),
    T('sine', [1762, 1762], { gain: 0.1, trem: [2, 0.95] }) ] },
  loopDash: { label: 'Dash · bucle', seconds: 1, loop: true, layers: [
    N('bp', [1300, 1300], { q: 0.6, gain: 0.7, trem: [6, 0.3] }),
    N('bp', [3800, 3800], { q: 0.5, gain: 0.12, trem: [9, 0.5] }) ] },
  loopZone: { label: 'Zona de aviso · bucle', seconds: 1, loop: true, layers: [
    T('sine', [98, 98], { gain: 0.6, trem: [2, 0.7] }),
    T('square', [392, 392], { gain: 0.06, trem: [4, 0.95] }),
    N('lp', [260, 260], { gain: 0.3, trem: [2, 0.6] }) ] },
  loopSummon: { label: 'Invocación · bucle', seconds: 1.5, loop: true, layers: [
    T('sine', [65, 65], { gain: 0.55, trem: [2, 0.3] }),
    T('saw', [98, 98], { gain: 0.12, trem: [4, 0.6] }),
    N('bp', [700, 700], { q: 0.8, gain: 0.25, trem: [2, 0.6] }),
    P(5, [800, 1600], { spread: 1.4, dur: 0.14, gain: 0.16 }) ] },
};

// ============================================================ which effect plays which sound

export const PRESET_SOUNDS = {
  orb: { cast: 'castArcane', Impact: 'hitArcane' },
  bolt: { cast: 'castNeedle', Impact: 'hitSpark' },
  burst: { Spawn: 'hitArcane' },
  charge: { Spawn: 'chargeUp', Loop: 'loopCharge', End: 'hitArcane' },
  aura: { Spawn: 'warn', Loop: 'loopZone' },

  fireball: { cast: 'castFire', Impact: 'hitFire' },
  iceShard: { cast: 'castIce', Impact: 'hitIce' },
  poisonGlob: { cast: 'castPoison', Impact: 'hitSplash' },
  sparkBolt: { cast: 'castZap', Impact: 'hitZap' },
  arcaneMissile: { cast: 'castArcane', Impact: 'hitArcane' },
  shadowOrb: { cast: 'castShadow', Impact: 'hitShadow' },
  rock: { cast: 'castRock', Impact: 'hitRock' },
  needle: { cast: 'castNeedle', Impact: 'hitSpark' },
  blade: { cast: 'castBlade', Impact: 'hitSlash' },
  heavyOrb: { cast: 'castHeavy', Impact: 'hitHeavy' },
  wisp: { cast: 'castWisp', Impact: 'hitWisp' },

  hitSpark: { Spawn: 'hitSpark' },
  explosion: { Spawn: 'explosion' },
  iceShatter: { Spawn: 'hitIce' },
  poisonSplash: { Spawn: 'hitSplash' },
  lightningStrike: { Spawn: 'thunder' },
  dustBurst: { Spawn: 'dust' },
  healBurst: { Spawn: 'heal' },
  shieldBreak: { Spawn: 'shieldBreak' },
  slash: { Spawn: 'hitSlash' },

  chargeVortex: { Spawn: 'chargeUp', Loop: 'loopCharge', End: 'hitArcane' },
  buffAura: { Spawn: 'buffUp', Loop: 'loopAura' },
  shield: { Spawn: 'shieldUp', Loop: 'loopShield', End: 'puff' },
  burning: { Loop: 'loopFire' },
  frozen: { Loop: 'loopFrost' },
  poisoned: { Loop: 'loopPoison' },
  stunned: { Loop: 'loopStun' },
  mark: { Spawn: 'markLock', Loop: 'loopMark' },

  dashTrail: { Loop: 'loopDash' },
  footDust: { Spawn: 'step' },

  warnZone: { Spawn: 'warn', Loop: 'loopZone', End: 'shockwave' },
  shockwave: { Spawn: 'shockwave' },
  summonCircle: { Spawn: 'summon', Loop: 'loopSummon', End: 'puff' },
  hazardFire: { Loop: 'loopFire' },
  hazardPoison: { Loop: 'loopPoison' },
  hazardIce: { Spawn: 'spikes', Loop: 'loopFrost', End: 'hitIce' },
  beam: { Spawn: 'beamStart', Loop: 'loopBeam', End: 'beamEnd' },

  muzzleFlash: { Spawn: 'muzzle' },
  smokePuff: { Spawn: 'puff' },
  teleport: { Spawn: 'teleport' },
};

/** The sounds of a preset, by moment (`cast`, lane names). Empty when it has none. */
export function soundsOf(presetId) {
  return PRESET_SOUNDS[presetId] || {};
}

// ============================================================ synthesiser

const TAU = Math.PI * 2;

/** A small deterministic generator −1..1 (the same sound renders the same samples every time). */
function noiseSource(seed) {
  let s = (seed * 2654435761) >>> 0 || 1;
  return () => {
    s ^= s << 13; s >>>= 0;
    s ^= s >>> 17;
    s ^= s << 5; s >>>= 0;
    return (s / 4294967295) * 2 - 1;
  };
}

function seedOf(text) {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) h = Math.imul(h ^ text.charCodeAt(i), 16777619);
  return h >>> 0;
}

const WAVES = {
  sine: (ph) => Math.sin(ph * TAU),
  tri: (ph) => 1 - 4 * Math.abs(((ph + 0.25) % 1) - 0.5),
  saw: (ph) => 2 * (ph % 1) - 1,
  square: (ph) => ((ph % 1) < 0.5 ? 1 : -1),
};

/** Envelope of a one-shot layer at progress x (0..1): a linear attack of `a` seconds, then (1 − x)^curve. */
function envelope(x, seconds, a, curve) {
  const attack = a > 0 ? Math.min(1, (x * seconds) / a) : 1;
  return attack * Math.pow(Math.max(0, 1 - x), curve);
}

function renderLayer(layer, out, total, sr, sustained, rnd) {
  const t0 = sustained ? 0 : layer.t0 || 0;
  const dur = sustained ? total / sr : layer.dur ?? total / sr - t0;
  const start = Math.floor(t0 * sr);
  const n = Math.min(total - start, Math.floor(dur * sr));
  if (n <= 0) return;
  const gain = layer.gain ?? 1;
  const curve = layer.curve ?? 2;
  const amp = (i) => {
    const x = i / n;
    let e = sustained ? 1 : envelope(x, dur, layer.a ?? 0.005, curve);
    if (layer.trem) e *= 1 - layer.trem[1] * (0.5 + 0.5 * Math.sin((i / sr) * layer.trem[0] * TAU));
    return e * gain;
  };
  const glide = (i) => layer.f[0] * Math.pow(layer.f[1] / layer.f[0], i / n);

  if (layer.type === 'tone') {
    const wave = WAVES[layer.wave] || WAVES.sine;
    let phase = 0, mod = 0;
    for (let i = 0; i < n; i++) {
      let f = glide(i);
      if (layer.vib) f *= 1 + layer.vib[1] * Math.sin((i / sr) * layer.vib[0] * TAU);
      phase += f / sr;
      let at = phase;
      if (layer.fm) {
        mod += (f * layer.fm[0]) / sr;
        at += 8 + layer.fm[1] * (sustained ? 1 : 1 - i / n) * Math.sin(mod * TAU);   // + 8: the phase stays positive
      }
      out[start + i] += wave(at) * amp(i);
    }
  } else if (layer.type === 'noise') {
    // Chamberlin state-variable filter: one pass gives low / band / high at a cutoff that can glide.
    let low = 0, band = 0;
    const damp = layer.q ?? 1;
    for (let i = 0; i < n; i++) {
      const fc = Math.min(7000, glide(i));
      const f = 2 * Math.sin((Math.PI * fc) / sr);
      const x = rnd();
      low += f * band;
      const high = x - low - damp * band;
      band += f * high;
      const y = layer.filter === 'lp' ? low : layer.filter === 'hp' ? high : band;
      out[start + i] += y * amp(i);
    }
  } else if (layer.type === 'pings') {
    const spread = layer.spread ?? dur;
    const each = layer.dur ?? 0.08;
    for (let p = 0; p < layer.count; p++) {
      const at = Math.floor((t0 + (rnd() * 0.5 + 0.5) * spread) * sr);
      const f0 = layer.f[0] * Math.pow(layer.f[1] / layer.f[0], rnd() * 0.5 + 0.5);
      const len = Math.floor(each * sr * (0.6 + 0.4 * (rnd() * 0.5 + 0.5)));
      const level = gain * (0.5 + 0.5 * (rnd() * 0.5 + 0.5));
      let phase = 0;
      for (let i = 0; i < len; i++) {
        const x = i / len;
        const f = f0 * (layer.rise ? Math.pow(layer.rise, x) : 1);
        phase += f / sr;
        const idx = (at + i) % total;                    // a loop's late pings wrap round: the loop closes
        if (!sustained && at + i >= total) break;
        out[idx] += Math.sin(phase * TAU) * Math.min(1, x * 40) * Math.pow(1 - x, 2) * level;
      }
    }
  } else if (layer.type === 'clicks') {
    const spread = layer.spread ?? dur;
    for (let c = 0; c < layer.count; c++) {
      const at = Math.floor((t0 + (rnd() * 0.5 + 0.5) * spread) * sr);
      const len = 30 + Math.floor((rnd() * 0.5 + 0.5) * 120);
      const level = gain * (0.4 + 0.6 * (rnd() * 0.5 + 0.5));
      for (let i = 0; i < len; i++) {
        if (!sustained && at + i >= total) break;
        out[(at + i) % total] += rnd() * Math.pow(1 - i / len, 3) * level;
      }
    }
  }
}

const cache = new Map();

/**
 * The samples of a sound (mono, SAMPLE_RATE, peak ≈ 0.85). Cached by id. Null for an unknown id.
 * `def` = a definition that lives outside SOUNDS (the game's sounds, modules/game-sounds.js).
 */
export function renderSound(id, def = SOUNDS[id]) {
  if (cache.has(id)) return cache.get(id);
  if (!def) return null;

  const sr = SAMPLE_RATE;
  const length = Math.floor(def.seconds * sr);
  const fade = def.loop ? Math.floor(0.12 * sr) : 0;
  const total = length + fade;
  const mix = new Float32Array(total);
  const rnd = noiseSource(seedOf(id));
  // Pings and clicks of a loop are laid out over the loop itself (they wrap), not over the extra tail.
  def.layers.forEach((layer) => {
    const wraps = def.loop && (layer.type === 'pings' || layer.type === 'clicks');
    if (wraps) {
      const own = new Float32Array(length);
      renderLayer(layer, own, length, sr, true, rnd);
      for (let i = 0; i < total; i++) mix[i] += own[i % length];
    } else {
      renderLayer(layer, mix, total, sr, !!def.loop, rnd);
    }
  });

  const out = new Float32Array(length);
  if (def.loop) {
    // The tail past the end is cross-faded over the start: sample `length` continues into sample 0.
    for (let i = 0; i < length; i++) out[i] = mix[i];
    for (let i = 0; i < fade; i++) {
      const k = i / fade;
      out[i] = mix[i] * k + mix[length + i] * (1 - k);
    }
  } else {
    for (let i = 0; i < length; i++) out[i] = mix[i];
    const tail = Math.min(length, Math.floor(0.012 * sr));          // never end on a click
    for (let i = 0; i < tail; i++) out[length - 1 - i] *= i / tail;
  }

  let peak = 0;
  for (let i = 0; i < length; i++) { out[i] = Math.tanh(out[i] * 1.2); peak = Math.max(peak, Math.abs(out[i])); }
  const norm = peak > 0 ? 0.85 / peak : 1;
  for (let i = 0; i < length; i++) out[i] *= norm;

  cache.set(id, out);
  return out;
}

/** 16-bit PCM mono .wav of `samples`. */
export function encodeWav(samples, sampleRate = SAMPLE_RATE) {
  const bytes = new Uint8Array(44 + samples.length * 2);
  const view = new DataView(bytes.buffer);
  const text = (at, s) => { for (let i = 0; i < s.length; i++) view.setUint8(at + i, s.charCodeAt(i)); };
  text(0, 'RIFF'); view.setUint32(4, 36 + samples.length * 2, true); text(8, 'WAVE');
  text(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true);
  view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * 2, true);
  view.setUint16(32, 2, true); view.setUint16(34, 16, true);
  text(36, 'data'); view.setUint32(40, samples.length * 2, true);
  for (let i = 0; i < samples.length; i++) {
    const v = Math.max(-1, Math.min(1, samples[i]));
    view.setInt16(44 + i * 2, Math.round(v * 32767), true);
  }
  return bytes;
}
