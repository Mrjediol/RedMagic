// modules/game-sounds.js
// -----------------------------------------------------------------------------
// The GAME's sounds, synthesised — the palette that fills the slots of Biblioteca Jere ▸ Sounds.
// Same data shape and same synthesiser as the VFX Lab's examples (modules/vfx-sounds.js): one
// entry = a few layers. DOM-free: the web auditions them ("🔊 Sonidos del juego"), and
// tools/bake-sounds.mjs writes them to Assets/Audio/Sfx/Game/<family>/<id>.wav + palette.json.
//
//   GAME_SOUNDS[id] = { family, label, seconds, loop?, layers: [...] }
//   GAME_FAMILIES   = family → { label, max }     `max` = the family's length budget (seconds), checked by the bake
//   GAME_ASSIGN     = ordered rules "which slot gets which clip", applied in Unity by
//                     Tools ▸ RedMagic ▸ Audio ▸ 4 (SoundPaletteAssigner). First rule that matches wins.
//
// A palette, not one clip per slot: a sound is shared by every slot of its kind and the per-owner
// difference is the cue's pitch range (rule.pitch), never a new clip.
//
// OPTIONS — a sound that repeats all run long (jump, dash, step, shot, click, coin) has several
// takes: `<id>` is the one the rules assign, `<id>_2`, `<id>_3`… are alternatives that are assigned
// to nothing; pick one in the Sounds tab to try it. Where the slot already had a clip of its own
// (player_jump, player_dash, footsteps, ui_hover) only the numbered options exist here.
//
// Adding a sound = one entry here (+ a rule if it should be assigned) → `npm run bake-sounds`.

import { T, N, P, C, renderSound } from './vfx-sounds.js';
import { ADDED_SOUNDS } from './game-sounds-added.js';

export const GAME_FAMILIES = {
  player: { label: 'Jugador', max: 1.3 },
  weapon: { label: 'Armas del jugador', max: 1.2 },
  projectile: { label: 'Proyectiles enemigos', max: 0.6 },
  enemy: { label: 'Enemigos', max: 0.9 },
  gesture: { label: 'Gestos del rig', max: 0.6 },
  boss: { label: 'Jefes', max: 1.8 },
  attack: { label: 'Ataques (jefes y enemigos)', max: 1.3 },
  world: { label: 'Interactuables', max: 0.9 },
  economy: { label: 'Economía', max: 0.5 },
  run: { label: 'Flujo de la run', max: 1.8 },
  passive: { label: 'Pasivas legendarias', max: 1.3 },
  status: { label: 'Build y estados', max: 0.8 },
  ui: { label: 'UI y tienda', max: 0.4 },
};

const S = (family, label, seconds, layers, extra = {}) => ({ family, label, seconds, layers, ...extra });

export const GAME_SOUNDS = {
  // ================================================================ player
  player_jump_2: S('player', 'Salto · opción 2 (soplo)', 0.22, [
    N('bp', [500, 1900], { q: 0.8, dur: 0.2, a: 0.01, curve: 1.8, gain: 0.9 }),
    T('sine', [260, 520], { dur: 0.14, a: 0.005, curve: 2.2, gain: 0.35 }) ]),
  player_jump_3: S('player', 'Salto · opción 3 (muelle)', 0.2, [
    T('sine', [230, 640], { dur: 0.18, a: 0.004, curve: 1.6, gain: 0.8 }),
    T('tri', [460, 1280], { dur: 0.13, a: 0.004, curve: 2.2, gain: 0.2 }) ]),
  player_jump_4: S('player', 'Salto · opción 4 (tela)', 0.18, [
    N('lp', [1000, 420], { dur: 0.16, a: 0.004, curve: 2.2, gain: 0.9 }),
    N('hp', [3000, 5000], { q: 0.9, dur: 0.08, a: 0.002, curve: 2.5, gain: 0.25 }),
    T('sine', [180, 300], { dur: 0.1, a: 0.003, curve: 2.4, gain: 0.3 }) ]),
  player_air_jump: S('player', 'Salto en el aire', 0.24, [
    T('sine', [420, 980], { dur: 0.2, a: 0.005, curve: 1.8, gain: 0.6 }),
    N('hp', [3200, 5600], { q: 0.8, dur: 0.18, a: 0.01, curve: 2, gain: 0.35 }) ]),
  player_air_jump_2: S('player', 'Salto en el aire · opción 2 (chispa)', 0.28, [
    N('bp', [900, 2800], { q: 0.7, dur: 0.2, a: 0.008, curve: 1.8, gain: 0.7 }),
    P(5, [1800, 3600], { t0: 0, spread: 0.14, dur: 0.07, gain: 0.3, rise: 1.3 }) ]),
  player_dash_2: S('player', 'Dash · opción 2 (ráfaga)', 0.3, [
    N('bp', [2800, 700], { q: 0.5, dur: 0.28, a: 0.01, curve: 1.6, gain: 0.9 }),
    T('sine', [300, 110], { dur: 0.16, a: 0.003, curve: 2.2, gain: 0.4 }) ]),
  player_dash_3: S('player', 'Dash · opción 3 (corte)', 0.24, [
    N('hp', [5200, 2200], { q: 0.7, dur: 0.16, a: 0.004, curve: 2, gain: 0.7 }),
    N('bp', [1200, 3200], { q: 0.6, dur: 0.22, a: 0.03, curve: 1.6, gain: 0.5 }) ]),
  player_dash_4: S('player', 'Dash · opción 4 (arcano)', 0.32, [
    N('bp', [800, 2600], { q: 0.6, dur: 0.28, a: 0.02, curve: 1.6, gain: 0.7 }),
    T('saw', [900, 220], { dur: 0.2, a: 0.004, curve: 2, gain: 0.18 }),
    P(4, [2200, 4200], { t0: 0.04, spread: 0.2, dur: 0.06, gain: 0.16 }) ]),
  player_footstep_2: S('player', 'Pisada · opción 2 (piedra)', 0.12, [
    N('lp', [900, 300], { dur: 0.1, a: 0.002, curve: 2.4, gain: 0.8 }),
    C(2, { t0: 0, spread: 0.03, gain: 0.25 }) ]),
  player_footstep_3: S('player', 'Pisada · opción 3 (blanda)', 0.14, [
    N('lp', [420, 200], { dur: 0.13, a: 0.006, curve: 2, gain: 0.9 }),
    T('sine', [100, 62], { dur: 0.06, a: 0.002, curve: 2.6, gain: 0.35 }) ]),
  player_footstep_4: S('player', 'Pisada · opción 4 (seca)', 0.09, [
    N('bp', [1400, 600], { q: 0.9, dur: 0.07, a: 0.001, curve: 2.6, gain: 0.8 }),
    T('sine', [140, 80], { dur: 0.05, a: 0.001, curve: 2.6, gain: 0.3 }) ]),
  player_land: S('player', 'Aterrizar', 0.2, [
    N('lp', [700, 220], { dur: 0.18, a: 0.002, curve: 2, gain: 0.9 }),
    T('sine', [110, 55], { dur: 0.12, a: 0.002, curve: 2.2, gain: 0.7 }) ]),
  player_land_2: S('player', 'Aterrizar · opción 2 (polvo)', 0.26, [
    N('lp', [520, 240], { dur: 0.24, a: 0.01, curve: 1.8, gain: 0.9 }),
    T('sine', [90, 50], { dur: 0.1, a: 0.002, curve: 2.4, gain: 0.5 }),
    C(3, { t0: 0.03, spread: 0.12, gain: 0.15 }) ]),
  player_land_3: S('player', 'Aterrizar · opción 3 (golpe seco)', 0.14, [
    N('bp', [900, 350], { q: 0.9, dur: 0.1, a: 0.001, curve: 2.4, gain: 0.8 }),
    T('sine', [150, 60], { dur: 0.12, a: 0.001, curve: 2.4, gain: 0.8 }) ]),
  player_melee: S('player', 'Ataque cuerpo a cuerpo', 0.22, [
    N('bp', [4200, 1200], { q: 0.5, dur: 0.18, a: 0.012, curve: 1.5, gain: 0.9 }),
    N('hp', [6000, 4200], { q: 0.9, dur: 0.08, t0: 0.015, a: 0.008, curve: 2.2, gain: 0.3 }) ]),
  player_melee_2: S('player', 'Ataque cuerpo a cuerpo · opción 2 (pesado)', 0.28, [
    N('bp', [1800, 500], { q: 0.6, dur: 0.24, a: 0.02, curve: 1.5, gain: 0.9 }),
    T('sine', [200, 80], { dur: 0.14, t0: 0.03, a: 0.004, curve: 2.2, gain: 0.4 }) ]),
  player_melee_3: S('player', 'Ataque cuerpo a cuerpo · opción 3 (filo)', 0.2, [
    N('hp', [6800, 3000], { q: 0.6, dur: 0.16, a: 0.008, curve: 1.6, gain: 0.8 }),
    T('saw', [2600, 1100], { dur: 0.1, t0: 0.01, a: 0.006, curve: 2.4, gain: 0.12 }) ]),
  player_shoot: S('player', 'Disparo', 0.2, [
    T('sine', [900, 300], { dur: 0.14, a: 0.002, curve: 2, gain: 0.7 }),
    N('bp', [2400, 900], { q: 0.7, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.5 }) ]),
  player_shoot_2: S('player', 'Disparo · opción 2 (seco)', 0.14, [
    N('bp', [3000, 800], { q: 0.6, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.8 }),
    T('sine', [420, 140], { dur: 0.1, a: 0.001, curve: 2.4, gain: 0.6 }) ]),
  player_shoot_3: S('player', 'Disparo · opción 3 (mágico)', 0.24, [
    T('sine', [620, 1240], { dur: 0.18, a: 0.004, curve: 2, gain: 0.5 }),
    T('tri', [1240, 2480], { dur: 0.14, a: 0.004, curve: 2.4, gain: 0.2 }),
    N('hp', [4000, 2600], { q: 0.8, dur: 0.08, a: 0.001, curve: 2.4, gain: 0.3 }) ]),
  player_hurt: S('player', 'Daño recibido', 0.3, [
    T('square', [330, 120], { dur: 0.22, a: 0.002, curve: 1.8, gain: 0.3 }),
    T('sine', [220, 70], { dur: 0.26, a: 0.002, curve: 1.8, gain: 0.8 }),
    N('bp', [1500, 500], { q: 0.7, dur: 0.12, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  player_hurt_2: S('player', 'Daño recibido · opción 2 (golpe)', 0.22, [
    N('lp', [1400, 260], { dur: 0.18, a: 0.001, curve: 2, gain: 0.9 }),
    T('sine', [180, 60], { dur: 0.2, a: 0.001, curve: 2, gain: 0.9 }) ]),
  player_death: S('player', 'Muerte', 1.2, [
    T('sine', [392, 98], { dur: 1.1, a: 0.01, curve: 1.3, gain: 0.7, vib: [6, 0.03] }),
    T('saw', [196, 49], { dur: 1, a: 0.02, curve: 1.6, gain: 0.2 }),
    N('lp', [1200, 150], { dur: 0.9, a: 0.01, curve: 1.6, gain: 0.5 }),
    T('sine', [80, 36], { dur: 0.5, a: 0.002, curve: 2, gain: 0.8 }) ]),
  player_heal: S('player', 'Cura', 0.6, [
    T('sine', [523, 523], { dur: 0.3, a: 0.01, curve: 2.2, gain: 0.45 }),
    T('sine', [659, 659], { dur: 0.3, t0: 0.08, a: 0.01, curve: 2.2, gain: 0.45 }),
    T('sine', [784, 1047], { dur: 0.4, t0: 0.16, a: 0.01, curve: 2, gain: 0.5 }),
    P(5, [2400, 4200], { t0: 0.15, spread: 0.35, dur: 0.07, gain: 0.12 }) ]),
  player_low_health: S('player', 'Vida baja', 0.5, [
    T('sine', [70, 48], { dur: 0.16, a: 0.004, curve: 2, gain: 1 }),
    T('sine', [70, 48], { dur: 0.2, t0: 0.2, a: 0.004, curve: 2, gain: 0.8 }),
    T('square', [440, 440], { dur: 0.08, a: 0.004, curve: 1.2, gain: 0.08 }) ]),
  pickup_item: S('player', 'Recoger objeto', 0.3, [
    T('sine', [784, 784], { dur: 0.1, a: 0.003, curve: 2, gain: 0.5 }),
    T('sine', [1175, 1175], { dur: 0.2, t0: 0.07, a: 0.003, curve: 2.2, gain: 0.5 }),
    T('tri', [2350, 2350], { dur: 0.12, t0: 0.07, a: 0.003, curve: 2.6, gain: 0.12 }) ]),
  pickup_item_2: S('player', 'Recoger objeto · opción 2 (pop)', 0.18, [
    T('sine', [500, 1300], { dur: 0.12, a: 0.003, curve: 1.8, gain: 0.7 }),
    N('hp', [3600, 5200], { q: 0.9, dur: 0.06, t0: 0.05, a: 0.002, curve: 2.4, gain: 0.25 }) ]),

  // ================================================================ player weapons
  weapon_fire_bolt: S('weapon', 'Disparo · proyectil recto', 0.2, [
    T('sine', [1100, 380], { dur: 0.15, a: 0.002, curve: 2, gain: 0.6 }),
    N('bp', [2600, 1100], { q: 0.7, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.5 }) ]),
  weapon_fire_bolt_2: S('weapon', 'Disparo · proyectil recto · opción 2 (pew)', 0.18, [
    T('square', [1500, 420], { dur: 0.14, a: 0.002, curve: 2, gain: 0.25 }),
    T('sine', [1500, 420], { dur: 0.16, a: 0.002, curve: 2, gain: 0.5 }) ]),
  weapon_fire_bolt_3: S('weapon', 'Disparo · proyectil recto · opción 3 (soplo)', 0.2, [
    N('bp', [1600, 3400], { q: 0.6, dur: 0.16, a: 0.006, curve: 1.8, gain: 0.8 }),
    T('sine', [520, 300], { dur: 0.1, a: 0.002, curve: 2.4, gain: 0.35 }) ]),
  weapon_fire_burst: S('weapon', 'Disparo · ráfaga arcana', 0.16, [
    T('sine', [1400, 700], { dur: 0.1, a: 0.002, curve: 2.2, gain: 0.55 }),
    T('tri', [2800, 1400], { dur: 0.08, a: 0.002, curve: 2.6, gain: 0.2 }),
    N('hp', [4800, 3000], { q: 0.8, dur: 0.05, a: 0.001, curve: 2.6, gain: 0.3 }) ]),
  weapon_fire_burst_2: S('weapon', 'Disparo · ráfaga arcana · opción 2 (tic)', 0.1, [
    T('sine', [1900, 900], { dur: 0.07, a: 0.001, curve: 2.4, gain: 0.6 }),
    N('hp', [5600, 3600], { q: 0.8, dur: 0.04, a: 0.001, curve: 2.6, gain: 0.35 }) ]),
  weapon_fire_fireball: S('weapon', 'Disparo · bola de fuego', 0.42, [
    N('bp', [500, 1700], { q: 0.9, dur: 0.38, a: 0.04, curve: 1.6, gain: 0.9 }),
    N('lp', [320, 140], { dur: 0.4, a: 0.02, curve: 2, gain: 0.6 }),
    T('sine', [150, 70], { dur: 0.28, a: 0.005, curve: 2.5, gain: 0.5 }) ]),
  weapon_fire_shotgun: S('weapon', 'Disparo · escopeta espectral', 0.4, [
    N('lp', [2600, 300], { dur: 0.3, a: 0.001, curve: 1.8, gain: 1 }),
    T('sine', [160, 50], { dur: 0.25, a: 0.001, curve: 2, gain: 0.9 }),
    N('hp', [3600, 5200], { q: 0.8, dur: 0.3, t0: 0.03, a: 0.05, curve: 1.6, gain: 0.25, trem: [22, 0.6] }) ]),
  weapon_fire_grenade: S('weapon', 'Lanzar · granada rúnica', 0.3, [
    N('bp', [700, 2000], { q: 0.7, dur: 0.26, a: 0.03, curve: 1.6, gain: 0.8 }),
    T('sine', [300, 180], { dur: 0.12, a: 0.004, curve: 2.2, gain: 0.3 }) ]),
  weapon_fire_lance: S('weapon', 'Disparo · lanza astral', 0.38, [
    N('hp', [3000, 6400], { q: 0.6, dur: 0.3, a: 0.02, curve: 1.6, gain: 0.6 }),
    T('sine', [700, 2100], { dur: 0.3, a: 0.01, curve: 1.8, gain: 0.45 }),
    T('tri', [1400, 4200], { dur: 0.22, a: 0.01, curve: 2.2, gain: 0.15 }) ]),
  weapon_charge: S('weapon', 'Cargar · rayo arcano', 0.8, [
    T('sine', [180, 760], { dur: 0.78, a: 0.3, curve: 0.7, gain: 0.55 }),
    T('saw', [90, 380], { dur: 0.78, a: 0.4, curve: 0.8, gain: 0.14 }),
    N('bp', [500, 3200], { q: 0.7, dur: 0.78, a: 0.4, curve: 0.8, gain: 0.35 }) ]),
  weapon_fire_beam: S('weapon', 'Disparo · rayo arcano', 0.4, [
    T('saw', [240, 110], { dur: 0.34, a: 0.006, curve: 1.6, gain: 0.4 }),
    T('sine', [1900, 880], { dur: 0.28, a: 0.004, curve: 2, gain: 0.4 }),
    N('bp', [3200, 1200], { q: 0.6, dur: 0.28, a: 0.004, curve: 1.8, gain: 0.5 }) ]),
  weapon_beam_loop: S('weapon', 'Rayo arcano · bucle', 1, [
    T('saw', [110, 110], { gain: 0.3, trem: [28, 0.3] }),
    T('saw', [112, 112], { gain: 0.3 }),
    T('sine', [880, 880], { gain: 0.16, trem: [14, 0.5] }),
    N('bp', [2600, 2600], { q: 0.5, gain: 0.4, trem: [28, 0.4] }) ], { loop: true }),
  weapon_hit_arcane: S('weapon', 'Impacto · arcano', 0.22, [
    T('sine', [880, 660], { dur: 0.18, a: 0.002, curve: 2.4, gain: 0.5 }),
    T('sine', [1320, 990], { dur: 0.14, a: 0.002, curve: 2.6, gain: 0.3 }),
    N('bp', [1800, 700], { q: 0.8, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.5 }) ]),
  weapon_hit_arcane_2: S('weapon', 'Impacto · arcano · opción 2 (golpe)', 0.18, [
    N('lp', [1800, 300], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.9 }),
    T('sine', [240, 90], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.7 }),
    T('sine', [1500, 900], { dur: 0.06, a: 0.001, curve: 2.6, gain: 0.25 }) ]),
  weapon_hit_arcane_3: S('weapon', 'Impacto · arcano · opción 3 (chispa)', 0.16, [
    N('hp', [5200, 2800], { q: 0.7, dur: 0.06, a: 0.001, curve: 2.4, gain: 0.8 }),
    T('sine', [1900, 620], { dur: 0.1, a: 0.001, curve: 2.4, gain: 0.5 }),
    T('sine', [240, 120], { dur: 0.08, a: 0.001, curve: 2.5, gain: 0.4 }) ]),
  weapon_hit_fire: S('weapon', 'Impacto · fuego', 0.5, [
    N('lp', [1500, 180], { dur: 0.42, a: 0.002, curve: 1.8, gain: 1 }),
    T('sine', [130, 42], { dur: 0.34, a: 0.002, curve: 2, gain: 0.9 }),
    C(8, { t0: 0.06, spread: 0.3, gain: 0.22 }) ]),
  weapon_hit_spectral: S('weapon', 'Impacto · espectral', 0.3, [
    T('sine', [520, 260], { dur: 0.26, a: 0.004, curve: 1.8, gain: 0.5, vib: [18, 0.06] }),
    N('bp', [1200, 400], { q: 0.7, dur: 0.2, a: 0.002, curve: 2, gain: 0.7 }) ]),
  weapon_hit_lance: S('weapon', 'Impacto · lanza', 0.3, [
    N('hp', [6000, 2600], { q: 0.6, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.7 }),
    T('sine', [1600, 500], { dur: 0.2, a: 0.001, curve: 2.2, gain: 0.5 }),
    T('sine', [200, 70], { dur: 0.18, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  weapon_hit_beam: S('weapon', 'Impacto · rayo (tic)', 0.12, [
    N('hp', [4200, 3000], { q: 0.7, dur: 0.07, a: 0.001, curve: 2.2, gain: 0.6 }),
    T('saw', [900, 400], { dur: 0.09, a: 0.001, curve: 2.2, gain: 0.25 }) ]),
  weapon_grenade_bounce: S('weapon', 'Granada · rebote', 0.14, [
    T('sine', [520, 260], { dur: 0.1, a: 0.001, curve: 2.4, gain: 0.7, fm: [2.76, 0.3] }),
    N('bp', [1800, 900], { q: 0.9, dur: 0.05, a: 0.001, curve: 2.6, gain: 0.4 }) ]),
  weapon_explosion: S('weapon', 'Explosión', 0.95, [
    N('lp', [2200, 110], { dur: 0.9, a: 0.002, curve: 1.6, gain: 1 }),
    T('sine', [110, 30], { dur: 0.65, a: 0.002, curve: 1.8, gain: 1 }),
    N('bp', [700, 250], { q: 0.8, dur: 0.5, t0: 0.03, a: 0.01, curve: 1.5, gain: 0.5 }),
    C(14, { t0: 0.12, spread: 0.65, gain: 0.2 }) ]),
  weapon_hit_terrain: S('weapon', 'Impacto en terreno', 0.16, [
    N('lp', [1200, 320], { dur: 0.12, a: 0.001, curve: 2.2, gain: 0.9 }),
    C(3, { t0: 0.01, spread: 0.08, gain: 0.3 }) ]),
  weapon_hit_terrain_2: S('weapon', 'Impacto en terreno · opción 2 (tac)', 0.1, [
    N('bp', [2200, 900], { q: 0.9, dur: 0.06, a: 0.001, curve: 2.6, gain: 0.8 }),
    T('sine', [300, 140], { dur: 0.06, a: 0.001, curve: 2.6, gain: 0.4 }) ]),
  weapon_expire: S('weapon', 'Proyectil se apaga', 0.16, [
    N('hp', [3200, 5400], { q: 0.9, dur: 0.14, a: 0.01, curve: 2, gain: 0.5 }),
    T('sine', [900, 500], { dur: 0.1, a: 0.004, curve: 2.4, gain: 0.25 }) ]),
  weapon_expire_2: S('weapon', 'Proyectil se apaga · opción 2 (pof)', 0.14, [
    N('bp', [700, 1300], { q: 0.9, dur: 0.12, a: 0.008, curve: 2.2, gain: 0.6 }) ]),

  // ================================================================ enemy projectiles
  proj_launch_energy: S('projectile', 'Lanzar · energía', 0.22, [
    T('sine', [520, 260], { dur: 0.18, a: 0.004, curve: 2, gain: 0.6 }),
    N('bp', [1400, 2400], { q: 0.7, dur: 0.16, a: 0.01, curve: 1.8, gain: 0.5 }) ]),
  proj_launch_energy_2: S('projectile', 'Lanzar · energía · opción 2 (zap)', 0.18, [
    T('saw', [1200, 300], { dur: 0.14, a: 0.002, curve: 2, gain: 0.3 }),
    N('hp', [4000, 2600], { q: 0.7, dur: 0.08, a: 0.001, curve: 2.4, gain: 0.4 }) ]),
  proj_launch_ice: S('projectile', 'Lanzar · hielo', 0.26, [
    N('bp', [5200, 3200], { q: 0.5, dur: 0.22, a: 0.01, curve: 2, gain: 0.5 }),
    T('sine', [3200, 1700], { dur: 0.16, a: 0.003, curve: 2.2, gain: 0.4 }) ]),
  proj_launch_rock: S('projectile', 'Lanzar · roca', 0.34, [
    N('lp', [420, 180], { dur: 0.3, a: 0.03, curve: 1.6, gain: 0.9 }),
    T('sine', [110, 60], { dur: 0.22, a: 0.005, curve: 2.2, gain: 0.7 }) ]),
  proj_launch_laser: S('projectile', 'Lanzar · láser', 0.24, [
    T('saw', [2200, 500], { dur: 0.2, a: 0.002, curve: 1.8, gain: 0.35 }),
    T('sine', [2200, 500], { dur: 0.2, a: 0.002, curve: 1.8, gain: 0.4 }) ]),
  proj_launch_leaf: S('projectile', 'Lanzar · hoja', 0.2, [
    N('bp', [2600, 3800], { q: 0.4, dur: 0.18, a: 0.02, curve: 1.6, gain: 0.7, trem: [30, 0.6] }) ]),
  proj_launch_spore: S('projectile', 'Lanzar · espora / veneno', 0.22, [
    T('sine', [300, 760], { dur: 0.12, a: 0.004, curve: 1.8, gain: 0.7 }),
    N('lp', [900, 380], { dur: 0.18, a: 0.01, curve: 2, gain: 0.5 }),
    P(2, [600, 1200], { t0: 0.04, spread: 0.1, dur: 0.05, gain: 0.25, rise: 1.5 }) ]),
  proj_launch_wave: S('projectile', 'Lanzar · onda', 0.4, [
    T('sine', [160, 60], { dur: 0.36, a: 0.004, curve: 1.6, gain: 0.9 }),
    N('bp', [400, 1400], { q: 0.7, dur: 0.34, a: 0.04, curve: 1.6, gain: 0.5 }) ]),
  proj_hit_energy: S('projectile', 'Impacto en el jugador · energía', 0.24, [
    T('sine', [700, 180], { dur: 0.2, a: 0.001, curve: 2, gain: 0.7 }),
    N('bp', [2000, 600], { q: 0.7, dur: 0.12, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  proj_hit_ice: S('projectile', 'Impacto en el jugador · hielo', 0.34, [
    N('hp', [6000, 3000], { q: 0.7, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.7 }),
    P(7, [2200, 6200], { t0: 0, spread: 0.18, dur: 0.09, gain: 0.45 }),
    T('sine', [260, 100], { dur: 0.14, a: 0.001, curve: 2.4, gain: 0.5 }) ]),
  proj_hit_rock: S('projectile', 'Impacto en el jugador · roca', 0.4, [
    N('lp', [1100, 220], { dur: 0.22, a: 0.001, curve: 2, gain: 1 }),
    T('sine', [95, 48], { dur: 0.28, a: 0.001, curve: 2.2, gain: 0.9 }),
    C(8, { t0: 0.05, spread: 0.3, gain: 0.4 }) ]),
  proj_hit_leaf: S('projectile', 'Impacto en el jugador · hoja', 0.16, [
    N('hp', [5200, 2400], { q: 0.6, dur: 0.1, a: 0.002, curve: 2, gain: 0.8 }),
    T('sine', [300, 140], { dur: 0.08, a: 0.001, curve: 2.4, gain: 0.4 }) ]),
  proj_hit_spore: S('projectile', 'Impacto en el jugador · espora', 0.3, [
    N('bp', [1100, 320], { q: 0.7, dur: 0.22, a: 0.003, curve: 1.6, gain: 0.9 }),
    T('sine', [240, 100], { dur: 0.16, a: 0.002, curve: 2.2, gain: 0.5 }),
    P(5, [350, 1200], { t0: 0.04, spread: 0.22, dur: 0.06, gain: 0.35, rise: 1.8 }) ]),
  proj_terrain_spore: S('projectile', 'Impacto en terreno · espora', 0.2, [
    N('bp', [900, 300], { q: 0.8, dur: 0.16, a: 0.004, curve: 1.8, gain: 0.8 }),
    P(3, [400, 1000], { t0: 0.02, spread: 0.12, dur: 0.05, gain: 0.3, rise: 1.7 }) ]),
  proj_terrain_soft: S('projectile', 'Impacto en terreno · energía', 0.16, [
    N('bp', [1200, 500], { q: 0.8, dur: 0.12, a: 0.002, curve: 2.2, gain: 0.8 }),
    T('sine', [400, 200], { dur: 0.08, a: 0.002, curve: 2.4, gain: 0.3 }) ]),
  proj_terrain_ice: S('projectile', 'Impacto en terreno · hielo', 0.24, [
    N('hp', [5600, 3400], { q: 0.7, dur: 0.08, a: 0.001, curve: 2.4, gain: 0.6 }),
    P(5, [2600, 5600], { t0: 0, spread: 0.12, dur: 0.07, gain: 0.4 }) ]),
  proj_terrain_rock: S('projectile', 'Impacto en terreno · roca', 0.36, [
    N('lp', [900, 240], { dur: 0.2, a: 0.001, curve: 2, gain: 1 }),
    T('sine', [80, 45], { dur: 0.2, a: 0.001, curve: 2.2, gain: 0.7 }),
    C(8, { t0: 0.04, spread: 0.28, gain: 0.4 }) ]),
  proj_expire: S('projectile', 'Proyectil se apaga', 0.14, [
    N('bp', [1600, 2800], { q: 0.9, dur: 0.12, a: 0.01, curve: 2.2, gain: 0.5 }) ]),

  // ================================================================ enemies
  enemy_hurt_2: S('enemy', 'Enemigo herido · opción 2 (chasquido)', 0.16, [
    N('bp', [2200, 700], { q: 0.7, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.8 }),
    T('square', [300, 150], { dur: 0.12, a: 0.001, curve: 2.2, gain: 0.2 }),
    T('sine', [300, 150], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.5 }) ]),
  enemy_hurt_3: S('enemy', 'Enemigo herido · opción 3 (carne)', 0.18, [
    N('lp', [1100, 300], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.9 }),
    T('sine', [210, 90], { dur: 0.16, a: 0.001, curve: 2, gain: 0.7 }) ]),
  enemy_hurt_4: S('enemy', 'Enemigo herido · opción 4 (quejido)', 0.2, [
    T('saw', [420, 230], { dur: 0.16, a: 0.006, curve: 1.8, gain: 0.3, vib: [30, 0.05] }),
    N('bp', [1500, 800], { q: 0.8, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  enemy_death_2: S('enemy', 'Enemigo muere · opción 2 (estallido)', 0.4, [
    N('lp', [1800, 200], { dur: 0.34, a: 0.001, curve: 1.8, gain: 0.9 }),
    T('sine', [240, 50], { dur: 0.3, a: 0.001, curve: 1.8, gain: 0.8 }),
    C(8, { t0: 0.05, spread: 0.28, gain: 0.25 }) ]),
  enemy_death_3: S('enemy', 'Enemigo muere · opción 3 (desvanece)', 0.5, [
    T('sine', [500, 110], { dur: 0.45, a: 0.004, curve: 1.6, gain: 0.6, vib: [14, 0.05] }),
    N('bp', [1600, 400], { q: 0.7, dur: 0.4, a: 0.01, curve: 1.6, gain: 0.6 }) ]),
  enemy_hurt_ice: S('enemy', 'Herido · hielo / piedra', 0.2, [
    N('hp', [5000, 2600], { q: 0.7, dur: 0.07, a: 0.001, curve: 2.4, gain: 0.6 }),
    N('lp', [1000, 300], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.8 }),
    P(3, [2400, 5000], { t0: 0, spread: 0.08, dur: 0.06, gain: 0.3 }) ]),
  enemy_death_ice: S('enemy', 'Muere · hielo / piedra (se rompe)', 0.6, [
    N('hp', [6000, 3000], { q: 0.7, dur: 0.12, a: 0.001, curve: 2.2, gain: 0.7 }),
    P(12, [2000, 6000], { t0: 0, spread: 0.36, dur: 0.1, gain: 0.45 }),
    N('lp', [900, 200], { dur: 0.4, a: 0.002, curve: 1.8, gain: 0.8 }),
    C(10, { t0: 0.05, spread: 0.4, gain: 0.35 }) ]),
  enemy_hurt_metal: S('enemy', 'Herido · metal', 0.24, [
    T('sine', [620, 600], { dur: 0.22, a: 0.001, curve: 2.4, gain: 0.6, fm: [3.53, 0.6] }),
    N('bp', [3000, 1500], { q: 0.8, dur: 0.05, a: 0.001, curve: 2.6, gain: 0.6 }) ]),
  enemy_death_metal: S('enemy', 'Muere · metal (se apaga)', 0.75, [
    T('saw', [420, 50], { dur: 0.65, a: 0.004, curve: 1.4, gain: 0.3 }),
    T('sine', [520, 500], { dur: 0.3, a: 0.001, curve: 2.2, gain: 0.5, fm: [2.41, 0.8] }),
    N('lp', [1600, 160], { dur: 0.5, a: 0.002, curve: 1.8, gain: 0.7 }),
    C(12, { t0: 0.05, spread: 0.5, gain: 0.4 }) ]),
  enemy_hurt_spirit: S('enemy', 'Herido · espíritu', 0.26, [
    T('sine', [900, 520], { dur: 0.22, a: 0.006, curve: 1.8, gain: 0.6, vib: [24, 0.07] }),
    N('hp', [3600, 5200], { q: 0.9, dur: 0.16, a: 0.01, curve: 2, gain: 0.3 }) ]),
  enemy_death_spirit: S('enemy', 'Muere · espíritu (se disipa)', 0.8, [
    T('sine', [760, 1500], { dur: 0.7, a: 0.03, curve: 1.5, gain: 0.5, vib: [9, 0.05] }),
    T('sine', [380, 190], { dur: 0.5, a: 0.01, curve: 1.8, gain: 0.35 }),
    N('hp', [3200, 6000], { q: 0.8, dur: 0.7, a: 0.1, curve: 1.6, gain: 0.3 }),
    P(6, [2200, 4400], { t0: 0.15, spread: 0.5, dur: 0.08, gain: 0.14 }) ]),
  enemy_hurt_plant: S('enemy', 'Herido · planta / seta (blando)', 0.2, [
    N('lp', [900, 260], { dur: 0.16, a: 0.002, curve: 2, gain: 0.9 }),
    T('sine', [340, 150], { dur: 0.14, a: 0.002, curve: 2.2, gain: 0.5, vib: [38, 0.08] }),
    P(2, [500, 1000], { t0: 0.02, spread: 0.08, dur: 0.05, gain: 0.25, rise: 1.6 }) ]),
  enemy_death_plant: S('enemy', 'Muere · planta / seta (nube de esporas)', 0.7, [
    N('bp', [500, 1400], { q: 0.7, dur: 0.6, a: 0.02, curve: 1.5, gain: 0.9 }),
    N('lp', [700, 180], { dur: 0.3, a: 0.002, curve: 2, gain: 0.7 }),
    T('sine', [300, 90], { dur: 0.35, a: 0.004, curve: 1.8, gain: 0.5, vib: [26, 0.07] }),
    P(9, [400, 1300], { t0: 0.05, spread: 0.5, dur: 0.06, gain: 0.3, rise: 1.7 }) ]),
  enemy_move_air: S('enemy', 'Se mueve · volador (aire suave)', 0.3, [
    N('bp', [500, 900], { q: 0.9, dur: 0.28, a: 0.1, curve: 1.4, gain: 0.8 }),
    N('lp', [300, 220], { dur: 0.26, a: 0.1, curve: 1.6, gain: 0.4 }) ]),
  enemy_move_air_2: S('enemy', 'Se mueve · volador · opción 2 (aleteo)', 0.24, [
    N('bp', [700, 1300], { q: 0.8, dur: 0.22, a: 0.05, curve: 1.6, gain: 0.8, trem: [16, 0.6] }) ]),
  enemy_wake: S('enemy', 'Despierta', 0.45, [
    T('sine', [140, 300], { dur: 0.4, a: 0.08, curve: 1.6, gain: 0.7 }),
    N('bp', [500, 1600], { q: 0.7, dur: 0.38, a: 0.1, curve: 1.6, gain: 0.5 }) ]),
  enemy_wake_metal: S('enemy', 'Despierta · máquina', 0.5, [
    T('saw', [80, 320], { dur: 0.42, a: 0.06, curve: 1.2, gain: 0.3 }),
    T('square', [880, 880], { dur: 0.06, t0: 0.36, a: 0.002, curve: 1.2, gain: 0.18 }),
    T('sine', [1320, 1320], { dur: 0.1, t0: 0.4, a: 0.002, curve: 2, gain: 0.4 }),
    N('bp', [800, 2400], { q: 0.7, dur: 0.4, a: 0.1, curve: 1.4, gain: 0.35 }) ]),
  enemy_evade: S('enemy', 'Esquiva', 0.22, [
    N('bp', [1400, 3600], { q: 0.6, dur: 0.2, a: 0.01, curve: 1.8, gain: 0.8 }) ]),

  // ================================================================ rig gestures (body foley)
  gesture_charge: S('gesture', 'Gesto · preparar', 0.4, [
    T('sine', [160, 420], { dur: 0.38, a: 0.15, curve: 1, gain: 0.5 }),
    N('bp', [500, 2200], { q: 0.7, dur: 0.38, a: 0.2, curve: 1, gain: 0.5 }) ]),
  gesture_flick: S('gesture', 'Gesto · latigazo corto', 0.14, [
    N('bp', [2600, 4400], { q: 0.6, dur: 0.12, a: 0.008, curve: 1.8, gain: 0.8 }) ]),
  gesture_sweep: S('gesture', 'Gesto · barrido', 0.3, [
    N('bp', [900, 2800], { q: 0.5, dur: 0.14, a: 0.03, curve: 1, gain: 0.8 }),
    N('bp', [2800, 800], { q: 0.5, dur: 0.16, t0: 0.12, a: 0.01, curve: 1.6, gain: 0.8 }) ]),
  gesture_shoot: S('gesture', 'Gesto · disparo', 0.18, [
    N('bp', [2400, 700], { q: 0.6, dur: 0.12, a: 0.001, curve: 2, gain: 0.8 }),
    T('sine', [380, 110], { dur: 0.14, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  gesture_hop: S('gesture', 'Gesto · saltito', 0.18, [
    T('sine', [200, 480], { dur: 0.14, a: 0.004, curve: 1.8, gain: 0.6 }),
    N('lp', [800, 400], { dur: 0.12, a: 0.004, curve: 2.2, gain: 0.5 }) ]),
  gesture_dash: S('gesture', 'Gesto · embestir', 0.4, [
    N('bp', [600, 2200], { q: 0.6, dur: 0.36, a: 0.03, curve: 1.5, gain: 0.9 }),
    T('sine', [120, 70], { dur: 0.3, a: 0.01, curve: 1.8, gain: 0.5 }) ]),
  gesture_slam: S('gesture', 'Gesto · golpe al suelo', 0.5, [
    T('sine', [120, 36], { dur: 0.42, a: 0.002, curve: 1.8, gain: 1 }),
    N('lp', [1200, 160], { dur: 0.42, a: 0.002, curve: 1.8, gain: 0.9 }),
    C(8, { t0: 0.05, spread: 0.3, gain: 0.3 }) ]),
  gesture_step: S('gesture', 'Gesto · paso pesado', 0.26, [
    N('lp', [500, 180], { dur: 0.22, a: 0.004, curve: 2, gain: 0.9 }),
    T('sine', [75, 45], { dur: 0.18, a: 0.002, curve: 2.2, gain: 0.8 }) ]),
  gesture_pickup: S('gesture', 'Gesto · coger (arrancar del suelo)', 0.4, [
    N('lp', [300, 900], { dur: 0.36, a: 0.1, curve: 1.2, gain: 0.8 }),
    C(8, { t0: 0.05, spread: 0.3, gain: 0.4 }),
    T('sine', [70, 110], { dur: 0.3, a: 0.08, curve: 1.4, gain: 0.5 }) ]),
  gesture_throw: S('gesture', 'Gesto · lanzar', 0.3, [
    N('bp', [500, 2400], { q: 0.6, dur: 0.26, a: 0.04, curve: 1.6, gain: 0.9 }),
    T('sine', [180, 90], { dur: 0.16, a: 0.006, curve: 2.2, gain: 0.4 }) ]),

  // ================================================================ bosses
  boss_activate: S('boss', 'Jefe · aparece', 1.4, [
    T('saw', [70, 110], { dur: 1.3, a: 0.2, curve: 1.2, gain: 0.35, vib: [11, 0.04], trem: [13, 0.4] }),
    T('sine', [55, 82], { dur: 1.3, a: 0.15, curve: 1.2, gain: 0.9 }),
    N('bp', [300, 1400], { q: 0.6, dur: 1.2, a: 0.3, curve: 1.2, gain: 0.6, trem: [13, 0.4] }),
    T('sine', [110, 30], { dur: 0.5, a: 0.002, curve: 1.8, gain: 0.8 }) ]),
  boss_hurt: S('boss', 'Jefe · herido', 0.26, [
    N('lp', [1400, 260], { dur: 0.2, a: 0.001, curve: 2, gain: 0.9 }),
    T('sine', [150, 60], { dur: 0.22, a: 0.001, curve: 2, gain: 0.9 }),
    T('saw', [300, 160], { dur: 0.12, a: 0.002, curve: 2.2, gain: 0.15 }) ]),
  boss_hurt_2: S('boss', 'Jefe · herido · opción 2 (crujido)', 0.22, [
    N('bp', [1800, 500], { q: 0.7, dur: 0.14, a: 0.001, curve: 2.2, gain: 0.9 }),
    T('sine', [110, 55], { dur: 0.18, a: 0.001, curve: 2.2, gain: 0.8 }),
    C(4, { t0: 0.01, spread: 0.1, gain: 0.3 }) ]),
  boss_death: S('boss', 'Jefe · muere', 1.8, [
    N('lp', [2400, 90], { dur: 1.7, a: 0.002, curve: 1.4, gain: 1 }),
    T('sine', [120, 26], { dur: 1.4, a: 0.002, curve: 1.5, gain: 1 }),
    T('saw', [220, 40], { dur: 1.5, a: 0.02, curve: 1.4, gain: 0.25, trem: [9, 0.5] }),
    C(26, { t0: 0.1, spread: 1.4, gain: 0.3 }),
    N('bp', [900, 300], { q: 0.8, dur: 0.8, t0: 0.4, a: 0.05, curve: 1.5, gain: 0.4 }) ]),
  boss_phase: S('boss', 'Jefe · cambio de fase', 1.2, [
    T('sine', [90, 360], { dur: 0.7, a: 0.3, curve: 0.8, gain: 0.6 }),
    N('bp', [400, 3000], { q: 0.6, dur: 0.7, a: 0.4, curve: 0.8, gain: 0.5 }),
    T('sine', [130, 30], { dur: 0.5, t0: 0.68, a: 0.002, curve: 1.6, gain: 1 }),
    N('lp', [1800, 140], { dur: 0.5, t0: 0.68, a: 0.002, curve: 1.6, gain: 0.9 }) ]),
  boss_vulnerable: S('boss', 'Jefe · ventana vulnerable', 0.7, [
    T('sine', [880, 440], { dur: 0.5, a: 0.004, curve: 1.6, gain: 0.5 }),
    T('sine', [1320, 660], { dur: 0.5, a: 0.004, curve: 1.8, gain: 0.3 }),
    N('hp', [5000, 2400], { q: 0.7, dur: 0.1, a: 0.001, curve: 2.2, gain: 0.5 }),
    P(8, [1800, 4200], { t0: 0.05, spread: 0.45, dur: 0.08, gain: 0.2 }) ]),

  // ================================================================ attacks (bosses and enemies, by attack type)
  attack_cast: S('attack', 'Ataque · conjuro (genérico)', 0.5, [
    T('sine', [260, 620], { dur: 0.44, a: 0.08, curve: 1.4, gain: 0.5 }),
    N('bp', [600, 2400], { q: 0.7, dur: 0.44, a: 0.1, curve: 1.4, gain: 0.5 }),
    P(5, [1400, 2800], { t0: 0.1, spread: 0.3, dur: 0.07, gain: 0.15 }) ]),
  attack_dash: S('attack', 'Ataque · embestida', 0.6, [
    N('bp', [500, 2600], { q: 0.5, dur: 0.55, a: 0.05, curve: 1.3, gain: 0.9 }),
    T('sine', [90, 150], { dur: 0.5, a: 0.05, curve: 1.4, gain: 0.7 }),
    T('saw', [90, 150], { dur: 0.45, a: 0.06, curve: 1.6, gain: 0.15 }) ]),
  attack_shockwave: S('attack', 'Ataque · onda de choque', 0.9, [
    T('sine', [140, 30], { dur: 0.8, a: 0.003, curve: 1.5, gain: 1 }),
    N('lp', [1200, 120], { dur: 0.85, a: 0.004, curve: 1.5, gain: 0.8 }),
    N('bp', [300, 1200], { q: 0.7, dur: 0.5, t0: 0.05, a: 0.1, curve: 1.6, gain: 0.3 }) ]),
  attack_throw: S('attack', 'Ataque · lanzar', 0.4, [
    N('bp', [400, 2200], { q: 0.6, dur: 0.34, a: 0.05, curve: 1.5, gain: 0.9 }),
    T('sine', [160, 80], { dur: 0.22, a: 0.01, curve: 2, gain: 0.5 }) ]),
  attack_pickup: S('attack', 'Ataque · arrancar la roca', 0.5, [
    N('lp', [300, 1000], { dur: 0.45, a: 0.12, curve: 1.2, gain: 0.9 }),
    C(12, { t0: 0.05, spread: 0.4, gain: 0.45 }),
    T('sine', [60, 100], { dur: 0.4, a: 0.1, curve: 1.4, gain: 0.6 }) ]),
  attack_melee_heavy: S('attack', 'Ataque · puñetazo', 0.4, [
    N('bp', [1600, 500], { q: 0.6, dur: 0.16, a: 0.02, curve: 1.5, gain: 0.8 }),
    T('sine', [140, 45], { dur: 0.26, t0: 0.1, a: 0.002, curve: 2, gain: 1 }),
    N('lp', [1200, 240], { dur: 0.22, t0: 0.1, a: 0.002, curve: 2, gain: 0.8 }) ]),
  attack_slam: S('attack', 'Ataque · mazazo', 0.7, [
    T('sine', [130, 32], { dur: 0.6, a: 0.002, curve: 1.7, gain: 1 }),
    N('lp', [1500, 150], { dur: 0.6, a: 0.002, curve: 1.7, gain: 0.9 }),
    C(12, { t0: 0.06, spread: 0.45, gain: 0.35 }) ]),
  attack_pincer: S('attack', 'Ataque · tenaza', 0.5, [
    N('bp', [800, 2600], { q: 0.5, dur: 0.26, a: 0.06, curve: 1, gain: 0.8 }),
    T('sine', [200, 60], { dur: 0.22, t0: 0.26, a: 0.001, curve: 2, gain: 1 }),
    N('hp', [5000, 2600], { q: 0.7, dur: 0.1, t0: 0.26, a: 0.001, curve: 2.2, gain: 0.6 }) ]),
  attack_shard_dive: S('attack', 'Ataque · esquirlas en picado', 0.55, [
    N('bp', [5200, 1800], { q: 0.5, dur: 0.5, a: 0.02, curve: 1.4, gain: 0.6 }),
    T('sine', [3200, 900], { dur: 0.45, a: 0.01, curve: 1.6, gain: 0.35 }),
    P(6, [2600, 5600], { t0: 0.05, spread: 0.4, dur: 0.07, gain: 0.25 }) ]),
  attack_spikes: S('attack', 'Ataque · espinas / raíces brotan', 0.5, [
    N('lp', [500, 1600], { dur: 0.3, a: 0.02, curve: 1.4, gain: 0.8 }),
    C(12, { t0: 0, spread: 0.3, gain: 0.45 }),
    T('sine', [110, 60], { dur: 0.25, a: 0.004, curve: 2, gain: 0.7 }),
    N('hp', [3000, 5600], { q: 0.7, dur: 0.16, t0: 0.08, a: 0.01, curve: 1.8, gain: 0.35 }) ]),
  attack_quake: S('attack', 'Ataque · temblor', 1.2, [
    T('sine', [60, 34], { dur: 1.1, a: 0.02, curve: 1.3, gain: 1, trem: [12, 0.5] }),
    N('lp', [500, 120], { dur: 1.1, a: 0.02, curve: 1.3, gain: 0.9, trem: [12, 0.4] }),
    C(22, { t0: 0.05, spread: 0.95, gain: 0.35 }) ]),
  attack_ignite: S('attack', 'Ataque · prender', 0.5, [
    N('bp', [500, 2200], { q: 0.8, dur: 0.44, a: 0.03, curve: 1.6, gain: 0.9 }),
    N('lp', [400, 160], { dur: 0.4, a: 0.02, curve: 2, gain: 0.6 }),
    C(8, { t0: 0.05, spread: 0.35, gain: 0.3 }) ]),
  attack_fire_loop: S('attack', 'Ataque · fuego · bucle', 1.2, [
    N('lp', [420, 420], { gain: 0.7, trem: [5, 0.3] }),
    N('bp', [1500, 1500], { q: 0.9, gain: 0.3, trem: [11, 0.5] }),
    C(26, { spread: 1.2, gain: 0.55 }) ], { loop: true }),
  attack_orb_ring: S('attack', 'Ataque · anillo de orbes', 0.6, [
    T('sine', [330, 660], { dur: 0.5, a: 0.04, curve: 1.6, gain: 0.5 }),
    T('sine', [495, 990], { dur: 0.5, t0: 0.03, a: 0.04, curve: 1.8, gain: 0.3 }),
    N('bp', [900, 3000], { q: 0.6, dur: 0.45, a: 0.08, curve: 1.6, gain: 0.4 }),
    P(8, [1600, 3200], { t0: 0.1, spread: 0.4, dur: 0.06, gain: 0.18 }) ]),
  attack_bullet_burst: S('attack', 'Ataque · lluvia de balas', 0.5, [
    N('bp', [1800, 3600], { q: 0.4, dur: 0.45, a: 0.03, curve: 1.4, gain: 0.7, trem: [24, 0.7] }),
    T('saw', [700, 1100], { dur: 0.4, a: 0.03, curve: 1.6, gain: 0.15, trem: [24, 0.7] }) ]),

  // ================================================================ interactables
  interact_use: S('world', 'Usar (genérico)', 0.22, [
    T('sine', [440, 660], { dur: 0.18, a: 0.004, curve: 2, gain: 0.6 }),
    N('bp', [1500, 2400], { q: 0.9, dur: 0.08, a: 0.002, curve: 2.4, gain: 0.3 }) ]),
  anvil_use: S('world', 'Yunque', 0.6, [
    T('sine', [1180, 1160], { dur: 0.55, a: 0.001, curve: 2.2, gain: 0.6, fm: [2.76, 0.9] }),
    T('sine', [2950, 2930], { dur: 0.3, a: 0.001, curve: 2.6, gain: 0.25 }),
    N('bp', [3000, 1500], { q: 0.8, dur: 0.04, a: 0.001, curve: 2.6, gain: 0.7 }) ]),
  book_open: S('world', 'Libro · abrir', 0.4, [
    N('bp', [1800, 3400], { q: 0.5, dur: 0.3, a: 0.04, curve: 1.6, gain: 0.7, trem: [18, 0.5] }),
    N('lp', [500, 240], { dur: 0.08, t0: 0.3, a: 0.002, curve: 2.4, gain: 0.7 }) ]),
  chest_open: S('world', 'Cofre · abrir', 0.6, [
    T('saw', [140, 260], { dur: 0.3, a: 0.03, curve: 1.2, gain: 0.2, vib: [34, 0.08] }),
    N('lp', [700, 260], { dur: 0.1, t0: 0.28, a: 0.002, curve: 2.2, gain: 0.8 }),
    T('sine', [1047, 1047], { dur: 0.25, t0: 0.3, a: 0.004, curve: 2.2, gain: 0.3 }),
    P(5, [2000, 4000], { t0: 0.3, spread: 0.25, dur: 0.07, gain: 0.14 }) ]),
  wardrobe_open: S('world', 'Armario · abrir', 0.5, [
    T('saw', [110, 190], { dur: 0.4, a: 0.05, curve: 1.2, gain: 0.25, vib: [28, 0.1] }),
    N('lp', [600, 240], { dur: 0.1, t0: 0.38, a: 0.002, curve: 2.2, gain: 0.8 }) ]),
  mirror_use: S('world', 'Espejo', 0.8, [
    T('sine', [1319, 1319], { dur: 0.7, a: 0.01, curve: 2, gain: 0.4, vib: [5, 0.01] }),
    T('sine', [1976, 1976], { dur: 0.6, t0: 0.05, a: 0.01, curve: 2.2, gain: 0.25 }),
    N('hp', [4200, 6000], { q: 0.8, dur: 0.6, a: 0.15, curve: 1.6, gain: 0.15 }),
    P(6, [2600, 5200], { t0: 0.1, spread: 0.5, dur: 0.09, gain: 0.14 }) ]),
  loot_take: S('world', 'Coger el botín', 0.35, [
    T('sine', [659, 659], { dur: 0.1, a: 0.003, curve: 2, gain: 0.45 }),
    T('sine', [988, 988], { dur: 0.1, t0: 0.07, a: 0.003, curve: 2, gain: 0.45 }),
    T('sine', [1319, 1319], { dur: 0.2, t0: 0.14, a: 0.003, curve: 2.2, gain: 0.5 }) ]),
  prop_hit: S('world', 'Objeto golpeado', 0.18, [
    N('bp', [1600, 600], { q: 0.8, dur: 0.1, a: 0.001, curve: 2.4, gain: 0.8 }),
    T('sine', [260, 130], { dur: 0.14, a: 0.001, curve: 2.4, gain: 0.6 }) ]),
  prop_break: S('world', 'Objeto destruido', 0.55, [
    N('lp', [2000, 240], { dur: 0.4, a: 0.001, curve: 1.8, gain: 0.9 }),
    T('sine', [180, 50], { dur: 0.3, a: 0.001, curve: 2, gain: 0.8 }),
    C(14, { t0: 0.04, spread: 0.42, gain: 0.4 }) ]),

  // ================================================================ economy
  coin_gold: S('economy', 'Oro', 0.2, [
    T('sine', [1568, 1568], { dur: 0.06, a: 0.001, curve: 2, gain: 0.5, fm: [3.01, 0.4] }),
    T('sine', [2093, 2093], { dur: 0.15, t0: 0.045, a: 0.001, curve: 2.4, gain: 0.5, fm: [3.01, 0.4] }) ]),
  coin_gold_2: S('economy', 'Oro · opción 2 (tintineo)', 0.16, [
    T('sine', [2400, 2380], { dur: 0.14, a: 0.001, curve: 2.6, gain: 0.5, fm: [2.4, 0.7] }),
    N('hp', [6000, 4000], { q: 0.8, dur: 0.03, a: 0.001, curve: 2.6, gain: 0.4 }) ]),
  coin_gold_3: S('economy', 'Oro · opción 3 (blip)', 0.12, [
    T('square', [988, 988], { dur: 0.04, a: 0.001, curve: 1.2, gain: 0.16 }),
    T('square', [1319, 1319], { dur: 0.07, t0: 0.04, a: 0.001, curve: 1.6, gain: 0.16 }),
    T('sine', [1319, 1319], { dur: 0.08, t0: 0.04, a: 0.001, curve: 2, gain: 0.4 }) ]),
  coin_diamond: S('economy', 'Diamante', 0.4, [
    T('sine', [2093, 2093], { dur: 0.3, a: 0.002, curve: 2.4, gain: 0.45 }),
    T('sine', [3136, 3136], { dur: 0.3, t0: 0.05, a: 0.002, curve: 2.4, gain: 0.35 }),
    P(5, [3000, 6000], { t0: 0.05, spread: 0.25, dur: 0.07, gain: 0.2 }) ]),
  coin_skull: S('economy', 'Calavera', 0.3, [
    T('sine', [300, 180], { dur: 0.1, a: 0.001, curve: 2.2, gain: 0.7, fm: [1.41, 0.5] }),
    T('sine', [380, 230], { dur: 0.14, t0: 0.08, a: 0.001, curve: 2.2, gain: 0.7, fm: [1.41, 0.5] }),
    N('bp', [1400, 800], { q: 0.9, dur: 0.05, a: 0.001, curve: 2.6, gain: 0.4 }) ]),
  coin_soul: S('economy', 'Fragmento de alma', 0.45, [
    T('sine', [660, 1320], { dur: 0.4, a: 0.03, curve: 1.6, gain: 0.5, vib: [8, 0.03] }),
    T('sine', [990, 1980], { dur: 0.35, a: 0.04, curve: 2, gain: 0.2 }),
    N('hp', [3600, 6000], { q: 0.8, dur: 0.35, a: 0.08, curve: 1.8, gain: 0.2 }) ]),

  // ================================================================ run flow
  run_start: S('run', 'Empieza la run', 1.1, [
    T('sine', [98, 98], { dur: 1, a: 0.02, curve: 1.6, gain: 0.8 }),
    T('sine', [294, 294], { dur: 0.5, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [392, 392], { dur: 0.5, t0: 0.18, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [587, 587], { dur: 0.6, t0: 0.36, a: 0.01, curve: 1.8, gain: 0.45 }),
    N('bp', [500, 2400], { q: 0.7, dur: 0.9, a: 0.3, curve: 1.4, gain: 0.3 }) ]),
  run_over: S('run', 'Fin de la run (muerte)', 1.6, [
    T('sine', [392, 392], { dur: 0.5, a: 0.01, curve: 1.8, gain: 0.45 }),
    T('sine', [311, 311], { dur: 0.5, t0: 0.3, a: 0.01, curve: 1.8, gain: 0.45 }),
    T('sine', [233, 196], { dur: 0.9, t0: 0.6, a: 0.01, curve: 1.5, gain: 0.5 }),
    T('sine', [98, 49], { dur: 1.4, t0: 0.1, a: 0.05, curve: 1.3, gain: 0.7 }),
    N('lp', [600, 120], { dur: 1.4, a: 0.1, curve: 1.4, gain: 0.4 }) ]),
  run_complete: S('run', 'Run completada', 1.6, [
    T('sine', [523, 523], { dur: 0.3, a: 0.006, curve: 2, gain: 0.4 }),
    T('sine', [659, 659], { dur: 0.3, t0: 0.14, a: 0.006, curve: 2, gain: 0.4 }),
    T('sine', [784, 784], { dur: 0.3, t0: 0.28, a: 0.006, curve: 2, gain: 0.4 }),
    T('sine', [1047, 1047], { dur: 1, t0: 0.42, a: 0.006, curve: 1.8, gain: 0.5 }),
    T('sine', [784, 784], { dur: 1, t0: 0.42, a: 0.006, curve: 1.8, gain: 0.3 }),
    T('tri', [523, 523], { dur: 1, t0: 0.42, a: 0.006, curve: 2, gain: 0.2 }),
    P(10, [2400, 4800], { t0: 0.45, spread: 0.9, dur: 0.09, gain: 0.12 }) ]),
  section_enter: S('run', 'Entrar en una sección', 0.6, [
    N('bp', [300, 1400], { q: 0.6, dur: 0.55, a: 0.2, curve: 1.4, gain: 0.7 }),
    T('sine', [110, 165], { dur: 0.5, a: 0.15, curve: 1.6, gain: 0.5 }) ]),
  section_clear: S('run', 'Sección superada', 0.8, [
    T('sine', [587, 587], { dur: 0.2, a: 0.004, curve: 2, gain: 0.45 }),
    T('sine', [880, 880], { dur: 0.2, t0: 0.12, a: 0.004, curve: 2, gain: 0.45 }),
    T('sine', [1175, 1175], { dur: 0.5, t0: 0.24, a: 0.004, curve: 2, gain: 0.5 }),
    T('tri', [2350, 2350], { dur: 0.3, t0: 0.24, a: 0.004, curve: 2.6, gain: 0.1 }) ]),
  wave_start: S('run', 'Empieza una oleada', 0.6, [
    T('saw', [196, 196], { dur: 0.5, a: 0.02, curve: 1.6, gain: 0.2 }),
    T('sine', [196, 196], { dur: 0.55, a: 0.02, curve: 1.6, gain: 0.6 }),
    T('sine', [294, 294], { dur: 0.5, a: 0.02, curve: 1.8, gain: 0.35 }),
    N('lp', [500, 200], { dur: 0.15, a: 0.002, curve: 2.2, gain: 0.5 }) ]),
  enemy_spawn: S('run', 'Aparece un enemigo', 0.35, [
    T('sine', [180, 520], { dur: 0.3, a: 0.05, curve: 1.6, gain: 0.5 }),
    N('bp', [500, 2000], { q: 0.7, dur: 0.3, a: 0.08, curve: 1.6, gain: 0.6 }) ]),
  enemy_spawn_2: S('run', 'Aparece un enemigo · opción 2 (pof)', 0.26, [
    N('bp', [500, 1200], { q: 0.8, dur: 0.24, a: 0.02, curve: 1.8, gain: 0.8 }),
    T('sine', [150, 90], { dur: 0.12, a: 0.004, curve: 2.2, gain: 0.5 }) ]),
  enemy_spawn_3: S('run', 'Aparece un enemigo · opción 3 (portal)', 0.4, [
    T('sine', [900, 240], { dur: 0.34, a: 0.01, curve: 1.6, gain: 0.5, vib: [20, 0.06] }),
    N('bp', [2400, 600], { q: 0.6, dur: 0.3, a: 0.01, curve: 1.6, gain: 0.5 }) ]),
  door_locked: S('run', 'Puerta cerrada', 0.3, [
    T('sine', [200, 190], { dur: 0.1, a: 0.001, curve: 2.2, gain: 0.7, fm: [2.2, 0.6] }),
    T('sine', [170, 160], { dur: 0.14, t0: 0.12, a: 0.001, curve: 2.2, gain: 0.7, fm: [2.2, 0.6] }),
    N('bp', [1400, 700], { q: 0.9, dur: 0.04, a: 0.001, curve: 2.6, gain: 0.5 }) ]),
  reward_appear: S('run', 'Aparece la recompensa del jefe', 1, [
    T('sine', [392, 784], { dur: 0.5, a: 0.1, curve: 1.2, gain: 0.4 }),
    T('sine', [784, 784], { dur: 0.6, t0: 0.4, a: 0.01, curve: 1.8, gain: 0.45 }),
    T('sine', [1175, 1175], { dur: 0.6, t0: 0.4, a: 0.01, curve: 2, gain: 0.3 }),
    N('hp', [3000, 6000], { q: 0.8, dur: 0.8, a: 0.3, curve: 1.4, gain: 0.2 }),
    P(10, [2400, 5200], { t0: 0.35, spread: 0.6, dur: 0.09, gain: 0.14 }) ]),

  // ================================================================ legendary passives
  passive_land: S('passive', 'Pasiva · cae al suelo', 0.6, [
    T('sine', [140, 40], { dur: 0.4, a: 0.002, curve: 1.8, gain: 0.9 }),
    N('lp', [1200, 200], { dur: 0.3, a: 0.002, curve: 2, gain: 0.7 }),
    T('sine', [1047, 1047], { dur: 0.5, t0: 0.04, a: 0.004, curve: 2.2, gain: 0.3 }),
    P(6, [2200, 4400], { t0: 0.05, spread: 0.4, dur: 0.08, gain: 0.14 }) ]),
  passive_drop: S('passive', 'Pasiva · suelta un objeto', 0.5, [
    T('sine', [880, 440], { dur: 0.3, a: 0.004, curve: 1.8, gain: 0.5 }),
    N('bp', [2400, 900], { q: 0.7, dur: 0.24, a: 0.01, curve: 1.8, gain: 0.4 }),
    P(5, [1800, 3600], { t0: 0.1, spread: 0.3, dur: 0.07, gain: 0.18 }) ]),
  passive_gold_drop: S('passive', 'Pasiva · lluvia de oro', 0.6, [
    P(12, [1500, 3200], { t0: 0, spread: 0.45, dur: 0.1, gain: 0.5 }),
    T('sine', [1568, 2093], { dur: 0.3, a: 0.004, curve: 2, gain: 0.3, fm: [3.01, 0.4] }) ]),
  passive_choice: S('passive', 'Pasiva · elegir', 0.7, [
    T('sine', [440, 440], { dur: 0.5, a: 0.02, curve: 1.8, gain: 0.4 }),
    T('sine', [554, 554], { dur: 0.5, t0: 0.06, a: 0.02, curve: 1.8, gain: 0.35 }),
    T('sine', [659, 659], { dur: 0.55, t0: 0.12, a: 0.02, curve: 1.8, gain: 0.35 }),
    N('hp', [3600, 6000], { q: 0.8, dur: 0.5, a: 0.15, curve: 1.6, gain: 0.15 }) ]),
  passive_revive: S('passive', 'Pasiva · revivir', 1.3, [
    T('sine', [98, 392], { dur: 0.8, a: 0.3, curve: 0.9, gain: 0.6 }),
    N('bp', [300, 3000], { q: 0.6, dur: 0.8, a: 0.4, curve: 0.9, gain: 0.45 }),
    T('sine', [523, 523], { dur: 0.5, t0: 0.75, a: 0.006, curve: 1.8, gain: 0.4 }),
    T('sine', [784, 784], { dur: 0.5, t0: 0.75, a: 0.006, curve: 1.8, gain: 0.35 }),
    T('sine', [1047, 1047], { dur: 0.5, t0: 0.75, a: 0.006, curve: 1.8, gain: 0.35 }),
    P(8, [2400, 4800], { t0: 0.75, spread: 0.45, dur: 0.08, gain: 0.14 }) ]),
  passive_shield: S('passive', 'Pasiva · escudo', 0.55, [
    T('sine', [220, 440], { dur: 0.45, a: 0.05, curve: 1.6, gain: 0.55 }),
    T('sine', [330, 660], { dur: 0.45, a: 0.05, curve: 1.8, gain: 0.35 }),
    N('bp', [700, 2600], { q: 0.7, dur: 0.4, a: 0.1, curve: 1.6, gain: 0.35 }) ]),
  passive_slow: S('passive', 'Pasiva · ralentizar', 0.6, [
    T('sine', [700, 180], { dur: 0.55, a: 0.01, curve: 1.3, gain: 0.6 }),
    T('tri', [1400, 360], { dur: 0.5, a: 0.01, curve: 1.6, gain: 0.15 }),
    N('bp', [2600, 500], { q: 0.6, dur: 0.5, a: 0.02, curve: 1.4, gain: 0.4 }) ]),
  passive_upgrade: S('passive', 'Pasiva · mejora', 0.7, [
    T('sine', [392, 392], { dur: 0.4, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [587, 587], { dur: 0.4, t0: 0.08, a: 0.01, curve: 2, gain: 0.4 }),
    T('sine', [784, 1176], { dur: 0.5, t0: 0.16, a: 0.02, curve: 1.8, gain: 0.4 }),
    N('hp', [3000, 6000], { q: 0.9, dur: 0.5, t0: 0.1, a: 0.15, curve: 1.6, gain: 0.22 }) ]),

  // ================================================================ build & status
  status_slow: S('status', 'Estado · ralentizado', 0.35, [
    T('sine', [600, 220], { dur: 0.3, a: 0.006, curve: 1.5, gain: 0.5 }),
    N('bp', [4200, 2000], { q: 0.5, dur: 0.25, a: 0.01, curve: 1.8, gain: 0.35 }) ]),
  status_gold_mark: S('status', 'Estado · marca de oro', 0.35, [
    T('square', [1175, 1175], { dur: 0.05, a: 0.002, curve: 1.2, gain: 0.14 }),
    T('sine', [1760, 1760], { dur: 0.26, t0: 0.07, a: 0.002, curve: 2.4, gain: 0.5, fm: [3.01, 0.3] }) ]),
  synergy_tier: S('status', 'Sinergia · nuevo nivel', 0.8, [
    T('sine', [440, 440], { dur: 0.25, a: 0.006, curve: 2, gain: 0.4 }),
    T('sine', [660, 660], { dur: 0.25, t0: 0.1, a: 0.006, curve: 2, gain: 0.4 }),
    T('sine', [880, 880], { dur: 0.5, t0: 0.2, a: 0.006, curve: 1.8, gain: 0.45 }),
    T('sine', [1320, 1320], { dur: 0.5, t0: 0.2, a: 0.006, curve: 2, gain: 0.25 }),
    P(8, [2400, 4800], { t0: 0.2, spread: 0.5, dur: 0.08, gain: 0.12 }) ]),

  // ================================================================ UI and shop
  ui_click: S('ui', 'Click', 0.08, [
    T('sine', [1100, 700], { dur: 0.06, a: 0.001, curve: 2.2, gain: 0.7 }),
    N('hp', [4000, 3000], { q: 0.9, dur: 0.02, a: 0.001, curve: 2.6, gain: 0.3 }) ]),
  ui_click_2: S('ui', 'Click · opción 2 (madera)', 0.07, [
    T('sine', [520, 300], { dur: 0.05, a: 0.001, curve: 2.4, gain: 0.8 }),
    N('bp', [1800, 1200], { q: 0.9, dur: 0.02, a: 0.001, curve: 2.6, gain: 0.4 }) ]),
  ui_click_3: S('ui', 'Click · opción 3 (cristal)', 0.1, [
    T('sine', [1760, 1760], { dur: 0.09, a: 0.001, curve: 2.6, gain: 0.6 }),
    T('sine', [2640, 2640], { dur: 0.06, a: 0.001, curve: 2.8, gain: 0.25 }) ]),
  ui_hover_2: S('ui', 'Hover · opción 2 (tic)', 0.05, [
    T('sine', [1400, 1200], { dur: 0.04, a: 0.001, curve: 2.4, gain: 0.5 }) ]),
  ui_hover_3: S('ui', 'Hover · opción 3 (soplo)', 0.07, [
    N('bp', [2600, 3400], { q: 0.8, dur: 0.06, a: 0.006, curve: 2.2, gain: 0.5 }) ]),
  ui_focus: S('ui', 'Foco (navegación)', 0.06, [
    T('sine', [880, 990], { dur: 0.05, a: 0.002, curve: 2.2, gain: 0.5 }) ]),
  ui_focus_2: S('ui', 'Foco · opción 2 (grave)', 0.06, [
    T('tri', [520, 600], { dur: 0.05, a: 0.002, curve: 2.2, gain: 0.5 }) ]),
  ui_back: S('ui', 'Atrás', 0.12, [
    T('sine', [700, 400], { dur: 0.1, a: 0.002, curve: 2, gain: 0.6 }),
    N('bp', [1500, 800], { q: 0.9, dur: 0.05, a: 0.002, curve: 2.4, gain: 0.25 }) ]),
  ui_deny: S('ui', 'Denegado', 0.2, [
    T('square', [180, 180], { dur: 0.07, a: 0.002, curve: 1, gain: 0.2 }),
    T('sine', [180, 180], { dur: 0.07, a: 0.002, curve: 1, gain: 0.5 }),
    T('square', [150, 150], { dur: 0.09, t0: 0.09, a: 0.002, curve: 1.2, gain: 0.2 }),
    T('sine', [150, 150], { dur: 0.09, t0: 0.09, a: 0.002, curve: 1.2, gain: 0.5 }) ]),
  ui_menu_open: S('ui', 'Menú · abrir', 0.2, [
    N('bp', [700, 2600], { q: 0.7, dur: 0.18, a: 0.02, curve: 1.6, gain: 0.6 }),
    T('sine', [440, 660], { dur: 0.14, a: 0.01, curve: 2, gain: 0.35 }) ]),
  ui_menu_open_2: S('ui', 'Menú · abrir · opción 2 (dos notas)', 0.18, [
    T('sine', [587, 587], { dur: 0.07, a: 0.002, curve: 2, gain: 0.45 }),
    T('sine', [880, 880], { dur: 0.1, t0: 0.06, a: 0.002, curve: 2.2, gain: 0.45 }) ]),
  ui_menu_close: S('ui', 'Menú · cerrar', 0.18, [
    N('bp', [2600, 700], { q: 0.7, dur: 0.16, a: 0.01, curve: 1.6, gain: 0.6 }),
    T('sine', [660, 440], { dur: 0.12, a: 0.006, curve: 2, gain: 0.35 }) ]),
  ui_menu_close_2: S('ui', 'Menú · cerrar · opción 2 (dos notas)', 0.18, [
    T('sine', [880, 880], { dur: 0.07, a: 0.002, curve: 2, gain: 0.45 }),
    T('sine', [587, 587], { dur: 0.1, t0: 0.06, a: 0.002, curve: 2.2, gain: 0.45 }) ]),
  ui_upgrade: S('ui', 'Mejora comprada', 0.4, [
    T('sine', [659, 659], { dur: 0.12, a: 0.003, curve: 2, gain: 0.45 }),
    T('sine', [988, 988], { dur: 0.26, t0: 0.09, a: 0.003, curve: 2, gain: 0.5 }),
    T('tri', [1976, 1976], { dur: 0.2, t0: 0.09, a: 0.003, curve: 2.6, gain: 0.12 }),
    N('hp', [4000, 6000], { q: 0.9, dur: 0.2, t0: 0.09, a: 0.02, curve: 2, gain: 0.15 }) ]),
  ui_level_up: S('ui', 'Arma sube de nivel', 0.4, [
    T('sine', [523, 523], { dur: 0.09, a: 0.003, curve: 1.8, gain: 0.45 }),
    T('sine', [659, 659], { dur: 0.09, t0: 0.07, a: 0.003, curve: 1.8, gain: 0.45 }),
    T('sine', [784, 784], { dur: 0.09, t0: 0.14, a: 0.003, curve: 1.8, gain: 0.45 }),
    T('sine', [1047, 1047], { dur: 0.18, t0: 0.21, a: 0.003, curve: 2, gain: 0.5 }) ]),
  shop_enter: S('ui', 'Tienda · entrar', 0.4, [
    T('sine', [1568, 1548], { dur: 0.36, a: 0.001, curve: 2.2, gain: 0.45, fm: [2.76, 0.6] }),
    T('sine', [2093, 2073], { dur: 0.26, t0: 0.1, a: 0.001, curve: 2.4, gain: 0.4, fm: [2.76, 0.6] }) ]),
  shop_reroll: S('ui', 'Tienda · reroll', 0.35, [
    C(9, { t0: 0, spread: 0.26, gain: 0.5 }),
    N('bp', [1200, 2800], { q: 0.6, dur: 0.3, a: 0.02, curve: 1.4, gain: 0.5, trem: [28, 0.7] }),
    T('sine', [880, 880], { dur: 0.08, t0: 0.26, a: 0.002, curve: 2.2, gain: 0.4 }) ]),
};

// ============================================================ which slot gets which sound
//
// A rule matches a slot of the sound registry by regular expressions (case-insensitive, all the
// ones present must match): `category` and `name` are the Sounds tab's folder and row, `component`
// the owner's type, `member` the field or trigger, `asset` the owner's file name, `archetype` the
// enemy's archetype (EnemyStats — "Flying…" for the ones that have no steps).
// `clip` = the sound; `pitch: [min, max]` and `volume` are written on the cue with it.
// Ordered: the first match wins, so the specific goes before the general.
// The assigner never touches a slot that holds a real clip, a silenced one or a removed one.

const R = (match, clip, o = {}) => ({ ...match, clip, ...o });
const vary = { pitch: [0.94, 1.06] };
const wide = { pitch: [0.88, 1.12] };

export const GAME_ASSIGN = [
  // ---- player
  R({ category: '^Player/Movement$', name: '^Land$' }, 'player_land', { ...wide, volume: 0.8 }),
  R({ category: '^Player/Attack$', name: '^Ranged' }, 'player_shoot', vary),
  R({ category: '^Player/Health$', name: '^Death$' }, 'player_death'),
  R({ category: '^Player/Health$', name: '^Heal$' }, 'player_heal'),
  R({ category: '^Player/Health$', name: '^Low health$' }, 'player_low_health'),
  R({ category: '^Player/Pickup$' }, 'pickup_item', vary),

  // ---- player weapons
  R({ component: '^WeaponDefinition$', member: '^chargeSound$' }, 'weapon_charge'),
  R({ component: '^WeaponDefinition$', member: '^beamLoopSound$' }, 'weapon_beam_loop', { volume: 0.6 }),
  R({ component: '^WeaponDefinition$', member: '^explosionSound$' }, 'weapon_explosion', vary),
  R({ component: '^WeaponDefinition$', member: '^terrainImpactSound$' }, 'weapon_hit_terrain', { ...wide, volume: 0.7 }),
  R({ component: '^WeaponDefinition$', member: '^expireSound$' }, 'weapon_expire', { ...wide, volume: 0.5 }),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Fuego|Fire' }, 'weapon_fire_fireball', vary),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Escopeta|Shotgun' }, 'weapon_fire_shotgun', vary),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Granada|Grenade' }, 'weapon_fire_grenade', vary),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Lanza|Lance' }, 'weapon_fire_lance', vary),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Rafaga|Burst' }, 'weapon_fire_burst', wide),
  R({ component: '^WeaponDefinition$', member: '^fireSound$', asset: 'Rayo|Beam' }, 'weapon_fire_beam'),
  R({ component: '^WeaponDefinition$', member: '^fireSound$' }, 'weapon_fire_bolt', vary),
  R({ component: '^WeaponDefinition$', member: '^impactSound$', asset: 'Fuego|Fire' }, 'weapon_hit_fire', vary),
  R({ component: '^WeaponDefinition$', member: '^impactSound$', asset: 'Escopeta|Espectral' }, 'weapon_hit_spectral', wide),
  R({ component: '^WeaponDefinition$', member: '^impactSound$', asset: 'Granada|Grenade' }, 'weapon_grenade_bounce', wide),
  R({ component: '^WeaponDefinition$', member: '^impactSound$', asset: 'Lanza|Lance' }, 'weapon_hit_lance', vary),
  R({ component: '^WeaponDefinition$', member: '^impactSound$', asset: 'Rayo|Beam' }, 'weapon_hit_beam', { ...wide, volume: 0.6 }),
  R({ component: '^WeaponDefinition$', member: '^impactSound$' }, 'weapon_hit_arcane', wide),

  // ---- enemy projectiles (by what the projectile is made of, read from its name)
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Boulder|Rock' }, 'proj_launch_rock', { ...vary, volume: 0.7 }),
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Laser' }, 'proj_launch_laser', { ...vary, volume: 0.6 }),
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Leaf' }, 'proj_launch_leaf', { ...wide, volume: 0.5 }),
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Wave' }, 'proj_launch_wave', { ...vary, volume: 0.7 }),
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Spore|Mote|Glob|Poison|Veneno|Espora' }, 'proj_launch_spore', { ...wide, volume: 0.6 }),
  R({ category: '^Projectiles/', name: '^Launch$', asset: 'Ice|Hielo' }, 'proj_launch_ice', { ...wide, volume: 0.6 }),
  R({ category: '^Projectiles/', name: '^Launch$' }, 'proj_launch_energy', { ...wide, volume: 0.5 }),
  R({ category: '^Projectiles/', name: '^Hit target$', asset: 'Boulder|Rock|Wave' }, 'proj_hit_rock', vary),
  R({ category: '^Projectiles/', name: '^Hit target$', asset: 'Leaf' }, 'proj_hit_leaf', vary),
  R({ category: '^Projectiles/', name: '^Hit target$', asset: 'Laser' }, 'proj_hit_energy', vary),
  R({ category: '^Projectiles/', name: '^Hit target$', asset: 'Spore|Mote|Glob|Poison|Veneno|Espora' }, 'proj_hit_spore', vary),
  R({ category: '^Projectiles/', name: '^Hit target$', asset: 'Ice|Hielo' }, 'proj_hit_ice', vary),
  R({ category: '^Projectiles/', name: '^Hit target$' }, 'proj_hit_energy', vary),
  R({ category: '^Projectiles/', name: '^Hit terrain$', asset: 'Boulder|Rock|Wave' }, 'proj_terrain_rock', { ...wide, volume: 0.7 }),
  R({ category: '^Projectiles/', name: '^Hit terrain$', asset: 'Laser|Leaf' }, 'proj_terrain_soft', { ...wide, volume: 0.5 }),
  R({ category: '^Projectiles/', name: '^Hit terrain$', asset: 'Spore|Mote|Glob|Poison|Veneno|Espora' }, 'proj_terrain_spore', { ...wide, volume: 0.5 }),
  R({ category: '^Projectiles/', name: '^Hit terrain$', asset: 'Ice|Hielo' }, 'proj_terrain_ice', { ...wide, volume: 0.6 }),
  R({ category: '^Projectiles/', name: '^Hit terrain$' }, 'proj_terrain_soft', { ...wide, volume: 0.5 }),
  R({ category: '^Projectiles/', name: '^Expire$' }, 'proj_expire', { ...wide, volume: 0.4 }),

  // ---- rig gestures (enemies and bosses): body foley, under the attack's own sound
  R({ component: '^ProceduralRig$', name: 'DashLoop|Loop$' }, null),
  R({ component: '^ProceduralRig$', name: 'Charge|Brace|Windup' }, 'gesture_charge', { ...vary, volume: 0.6 }),
  R({ component: '^ProceduralRig$', name: 'Disparo|Shoot|Fire' }, 'gesture_shoot', { ...vary, volume: 0.7 }),
  R({ component: '^ProceduralRig$', name: 'Flick|Finger' }, 'gesture_flick', { ...wide, volume: 0.6 }),
  R({ component: '^ProceduralRig$', name: 'Sweep|Swipe|Twirl|Spin|Giro' }, 'gesture_sweep', { ...vary, volume: 0.7 }),
  R({ component: '^ProceduralRig$', name: 'Saltito|Jump|Hop' }, 'gesture_hop', { ...wide, volume: 0.6 }),
  R({ component: '^ProceduralRig$', name: 'Slam' }, 'gesture_slam', { ...vary, volume: 0.8 }),
  R({ component: '^ProceduralRig$', name: 'Dash' }, 'gesture_dash', { ...vary, volume: 0.7 }),
  R({ component: '^ProceduralRig$', name: 'Move|Walk|Step' }, 'gesture_step', { ...wide, volume: 0.6 }),
  R({ component: '^ProceduralRig$', name: 'Pick' }, 'gesture_pickup', { ...vary, volume: 0.7 }),
  R({ component: '^ProceduralRig$', name: 'Throw' }, 'gesture_throw', { ...vary, volume: 0.7 }),

  // ---- attacks, by attack type (a new boss or enemy deck gets them with no new rule)
  R({ component: '^ChargeDashAttack$' }, 'attack_dash', vary),
  R({ component: '^ShockwaveAttack$' }, 'attack_shockwave', vary),
  R({ component: '^ThrowProjectileAttack$', member: '^pickupSound$' }, 'attack_pickup', vary),
  R({ component: '^ThrowProjectileAttack$' }, 'attack_throw', vary),
  R({ component: '^FistThrowAttack$' }, 'attack_throw', { pitch: [0.78, 0.88] }),
  R({ component: '^MeleeStrikeAttack$' }, 'attack_melee_heavy', wide),
  R({ component: '^LimbSlamAttack$' }, 'attack_slam', wide),
  R({ component: '^LimbPincerAttack$' }, 'attack_pincer', vary),
  R({ component: '^OrbiterDiveAttack$' }, 'attack_shard_dive', vary),
  R({ component: '^GroundSlamAttack$' }, 'attack_spikes', vary),
  R({ component: '^QuakeSlamAttack$' }, 'attack_quake', vary),
  R({ component: '^PlatformDenialAttack$', member: '^igniteSound$' }, 'attack_ignite', vary),
  R({ component: '^PlatformDenialAttack$', member: '^fireLoopSound$' }, 'attack_fire_loop', { volume: 0.6 }),
  R({ component: '^OrbRingAttack$' }, 'attack_orb_ring', vary),
  R({ component: '^BulletHellAttack$' }, 'attack_bullet_burst', vary),
  R({ component: 'Attack$', member: '^sound$' }, 'attack_cast', vary),

  // ---- bosses
  R({ category: '^Bosses/[^/]+$', name: '^Activation$' }, 'boss_activate'),
  R({ category: '^Bosses/[^/]+$', name: '^Death$' }, 'boss_death'),
  R({ category: '^Bosses/[^/]+$', name: '^Hurt$' }, 'boss_hurt', { ...wide, volume: 0.8 }),
  R({ category: '^Bosses/[^/]+$', name: 'transition$' }, 'boss_phase'),
  R({ category: '^Bosses/[^/]+$', name: '^Vulnerable' }, 'boss_vulnerable'),

  // ---- enemies: only what the generic fallback (SystemSounds) does not already cover well
  R({ category: '^Enemies/', name: '^Hurt$', asset: 'Golem|Stone|Rock' }, 'enemy_hurt_ice', wide),
  R({ category: '^Enemies/', name: '^Death$', asset: 'Golem|Stone|Rock' }, 'enemy_death_ice', vary),
  R({ category: '^Enemies/', name: '^Hurt$', asset: 'Robot|Turret|Mech' }, 'enemy_hurt_metal', wide),
  R({ category: '^Enemies/', name: '^Death$', asset: 'Robot|Turret|Mech' }, 'enemy_death_metal', vary),
  R({ category: '^Enemies/', name: '^Hurt$', asset: 'Spirit|Ghost|Wisp' }, 'enemy_hurt_spirit', wide),
  R({ category: '^Enemies/', name: '^Death$', asset: 'Spirit|Ghost|Wisp' }, 'enemy_death_spirit', vary),
  R({ category: '^Enemies/', name: '^Hurt$', asset: 'Shroom|Mushroom|Seta|Plant|Bloom|Vine|Root' }, 'enemy_hurt_plant', wide),
  R({ category: '^Enemies/', name: '^Death$', asset: 'Shroom|Mushroom|Seta|Plant|Bloom|Vine|Root' }, 'enemy_death_plant', vary),
  // A flyer does not step: a soft, quiet breath of air instead of the generic footsteps. With many
  // enemies on screen, movement has to stay under everything else.
  R({ category: '^Enemies/', name: '^Move$', archetype: '^Flying' }, 'enemy_move_air', { ...wide, volume: 0.22 }),
  R({ category: '^Enemies/', name: '^Wake$', asset: 'Robot|Turret|Mech' }, 'enemy_wake_metal', vary),
  R({ category: '^Enemies/', name: '^Wake$' }, 'enemy_wake', vary),
  R({ category: '^Enemies/', name: '^Evade$' }, 'enemy_evade', wide),

  // ---- interactables
  R({ category: '^Interactables/', name: '^Take$' }, 'loot_take'),
  R({ category: '^Interactables/', name: '^Hurt$' }, 'prop_hit', wide),
  R({ category: '^Interactables/', name: '^Death$' }, 'prop_break', vary),
  R({ category: '^Interactables/', asset: 'Anvil' }, 'anvil_use', vary),
  R({ category: '^Interactables/', asset: 'Book|Lectern' }, 'book_open'),
  R({ category: '^Interactables/', asset: 'Chest' }, 'chest_open'),
  R({ category: '^Interactables/', asset: 'Wardrobe' }, 'wardrobe_open'),
  R({ category: '^Interactables/', asset: 'Mirror' }, 'mirror_use'),
  R({ category: '^Interactables/', name: '^(Use|Open)$' }, 'interact_use'),

  // ---- legendary passives
  R({ component: '^PassiveDropCinematicSettings$' }, 'passive_land'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Slow' }, 'passive_slow'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Aurum' }, 'passive_gold_drop'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Choice' }, 'passive_choice'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Revive' }, 'passive_revive'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Shield' }, 'passive_shield'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Upgrade' }, 'passive_upgrade'),
  R({ component: '^LegendaryPassiveTuning$', member: 'Drop' }, 'passive_drop'),

  // ---- the game's own sounds (SystemSounds)
  R({ component: '^SystemSounds$', member: '^goldMarkApplied$' }, 'status_gold_mark'),
  R({ component: '^SystemSounds$', member: '^slowApplied$' }, 'status_slow', vary),
  R({ component: '^SystemSounds$', member: '^synergyTierReached$' }, 'synergy_tier'),
  R({ component: '^SystemSounds$', member: '^goldGained$' }, 'coin_gold', wide),
  R({ component: '^SystemSounds$', member: '^diamondGained$' }, 'coin_diamond', vary),
  R({ component: '^SystemSounds$', member: '^skullGained$' }, 'coin_skull', vary),
  R({ component: '^SystemSounds$', member: '^soulFragmentGained$' }, 'coin_soul', vary),
  R({ component: '^SystemSounds$', member: '^runStart$' }, 'run_start'),
  R({ component: '^SystemSounds$', member: '^runOver$' }, 'run_over'),
  R({ component: '^SystemSounds$', member: '^runComplete$' }, 'run_complete'),
  R({ component: '^SystemSounds$', member: '^sectionEnter$' }, 'section_enter'),
  R({ component: '^SystemSounds$', member: '^sectionClear$' }, 'section_clear'),
  R({ component: '^SystemSounds$', member: '^waveStart$' }, 'wave_start'),
  R({ component: '^SystemSounds$', member: '^enemySpawn$' }, 'enemy_spawn', { ...wide, volume: 0.6 }),
  R({ component: '^SystemSounds$', member: '^doorLocked$' }, 'door_locked'),
  R({ component: '^SystemSounds$', member: '^bossRewardAppear$' }, 'reward_appear'),
  R({ component: '^SystemSounds$', member: '^shopEnter$' }, 'shop_enter'),
  R({ component: '^SystemSounds$', member: '^shopReroll$' }, 'shop_reroll'),
  R({ component: '^SystemSounds$', member: '^(shopDeny|uiDeny)$' }, 'ui_deny'),
  R({ component: '^SystemSounds$', member: '^uiClick$' }, 'ui_click'),
  R({ component: '^SystemSounds$', member: '^uiFocus$' }, 'ui_focus', { volume: 0.6 }),
  R({ component: '^SystemSounds$', member: '^uiBack$' }, 'ui_back'),
  R({ component: '^SystemSounds$', member: '^upgradeBought$' }, 'ui_upgrade'),
  R({ component: '^SystemSounds$', member: '^mirrorPassiveUpgraded$' }, 'ui_upgrade', { pitch: [1.12, 1.12] }),
  R({ component: '^SystemSounds$', member: '^weaponLevelUp$' }, 'ui_level_up'),
  R({ component: '^SystemSounds$', member: '\\.open$' }, 'ui_menu_open'),
  R({ component: '^SystemSounds$', member: '\\.close$' }, 'ui_menu_close'),
];

// Sounds accepted in 🎨 Designs (game-sounds-added.js, written by the taller server); an id already here wins.
for (const [id, def] of Object.entries(ADDED_SOUNDS)) if (!GAME_SOUNDS[id]) GAME_SOUNDS[id] = def;

/** The samples of a game sound (same synthesiser and cache as the Lab's). Null for an unknown id. */
export function renderGameSound(id) {
  return GAME_SOUNDS[id] ? renderSound(id, GAME_SOUNDS[id]) : null;
}

/** Ids the rules assign to some slot; the rest are options to try by hand. */
export function assignedSoundIds() {
  return new Set(GAME_ASSIGN.map((rule) => rule.clip).filter(Boolean));
}
