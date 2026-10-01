// modules/vfx-stage.js
// -----------------------------------------------------------------------------
// "▶ Probar" of the ✨ VFX Lab: the effect played the way the game will play it, with its sounds.
//
//   projectile  a caster on the left shoots it: (Awake) → Move while it flies across → it hits the
//               dummy on the right and plays Impact there. Sounds: `cast` as it leaves, `Impact` on the hit.
//   fx          in the middle of the stage: Spawn once → Loop for a couple of seconds → End once
//               (whichever of the three the preset has). Each lane starts its sound; a Loop's repeats.
//
// The frames come from the BAKED sheet (what will be saved), not from a redraw, so what plays here is
// exactly what the library entry holds. Sounds are the synthesised examples of vfx-sounds.js, played
// through Web Audio; each can be auditioned alone and downloaded as .wav.

import { SOUNDS, SAMPLE_RATE, soundsOf, renderSound, encodeWav } from './vfx-sounds.js';

const W = 720, H = 240;
const FLOOR = 196;            // y of the ground line
const FRAME = 150;            // on-screen side of one effect frame
const FLIGHT = 0.85;          // seconds a projectile takes to cross
const HEAD = 0.21;            // a projectile's head sits this fraction of a frame ahead of its centre
const LOOP_SECONDS = 2.2;     // how long a Loop lane is held in the test

const MOMENT_LABELS = {
  cast: 'Al lanzar',
  Awake: 'Al aparecer (Awake)',
  Move: 'En vuelo (Move)',
  Impact: 'Al chocar (Impact)',
  Spawn: 'Al aparecer (Spawn)',
  Loop: 'Mientras dura (Loop)',
  End: 'Al acabar (End)',
};

const el = (tag, cls, text) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
};

// ============================================================ audio

let audio = null;
const buffers = new Map();

function audioContext() {
  if (audio) return audio;
  const Ctor = typeof window !== 'undefined' ? window.AudioContext || window.webkitAudioContext : null;
  if (!Ctor) return null;
  audio = new Ctor();
  return audio;
}

/** Plays a sound now. Returns a function that stops it (needed for loops). No Web Audio = no sound, no error. */
function playSound(id, { loop = false, gain = 0.8 } = {}) {
  const ctx = audioContext();
  const samples = ctx ? renderSound(id) : null;
  if (!samples) return () => {};
  if (ctx.state === 'suspended') ctx.resume();
  if (!buffers.has(id)) {
    const buffer = ctx.createBuffer(1, samples.length, SAMPLE_RATE);
    buffer.copyToChannel(samples, 0);
    buffers.set(id, buffer);
  }
  const source = ctx.createBufferSource();
  source.buffer = buffers.get(id);
  source.loop = loop;
  const volume = ctx.createGain();
  volume.gain.value = gain;
  source.connect(volume).connect(ctx.destination);
  source.start();
  return () => {
    // A short fade instead of a cut: stopping a loop mid-wave clicks.
    try {
      volume.gain.setTargetAtTime(0, ctx.currentTime, 0.03);
      source.stop(ctx.currentTime + 0.15);
    } catch { /* already stopped */ }
  };
}

function downloadWav(id) {
  const samples = renderSound(id);
  if (!samples) return;
  const url = URL.createObjectURL(new Blob([encodeWav(samples)], { type: 'audio/wav' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `${id}.wav`;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}

// ============================================================ timeline

/**
 * What "Probar" plays, as segments in order:
 * `{ lane, seconds, loop, sound, from: {x, y}, to: {x, y}, hit }` — positions are the frame's centre.
 */
export function buildTimeline(preset, baked) {
  const lane = (name) => baked.lanes.find((l) => l.name === name && l.frameBoxIndices.length);
  const once = (l) => l.frameBoxIndices.length / l.fps;
  const sounds = soundsOf(preset.id);
  const segments = [];

  if (preset.kind === 'projectile') {
    const y = FLOOR - 62;
    const start = { x: 96 + FRAME * (0.5 - HEAD), y };
    const end = { x: W - 104 - FRAME * HEAD, y };
    const awake = lane('Awake'), move = lane('Move') || baked.lanes[0], impact = lane('Impact');
    if (awake) segments.push({ lane: awake, seconds: once(awake), loop: false, sound: sounds.Awake, from: start, to: start });
    segments.push({ lane: move, seconds: FLIGHT, loop: true, sound: sounds.cast, from: start, to: end });
    // The impact bursts where the HEAD was, not where the frame's centre was.
    const at = { x: end.x + FRAME * HEAD, y };
    if (impact) segments.push({ lane: impact, seconds: once(impact), loop: false, sound: sounds.Impact, from: at, to: at, hit: true });
    return segments;
  }

  const centre = { x: W / 2, y: H / 2 - 4 };
  const spawn = lane('Spawn'), loop = lane('Loop'), end = lane('End');
  if (spawn) segments.push({ lane: spawn, seconds: once(spawn), loop: false, sound: sounds.Spawn, from: centre, to: centre });
  if (loop) segments.push({ lane: loop, seconds: Math.max(LOOP_SECONDS, once(loop) * 2), loop: true, sound: sounds.Loop, loopSound: true, from: centre, to: centre });
  if (end) segments.push({ lane: end, seconds: once(end), loop: false, sound: sounds.End, from: centre, to: centre });
  if (!segments.length && baked.lanes[0]) {
    const first = baked.lanes[0];
    segments.push({ lane: first, seconds: first.loop ? LOOP_SECONDS : once(first), loop: first.loop, sound: sounds[first.name], loopSound: first.loop, from: centre, to: centre });
  }
  return segments;
}

// ============================================================ stage

export function createStage({ createCanvas }) {
  const root = el('div', 'vfxStage');
  const head = el('div', 'vfxBar');
  const playBtn = el('button', 'primary', '▶ Probar');
  playBtn.dataset.action = 'stagePlay';
  playBtn.title = 'Reproduce el efecto como en el juego: el proyectil vuela y choca; un efecto aparece, dura y se apaga. Con sus sonidos.';
  const soundLabel = el('label', 'vfxSoundToggle');
  const soundBox = el('input');
  soundBox.type = 'checkbox';
  soundBox.checked = true;
  soundLabel.append(soundBox, document.createTextNode(' 🔊 Sonido'));
  const info = el('span', 'efHint', '');
  head.append(playBtn, soundLabel, info);

  const canvas = createCanvas(W, H);
  canvas.className = 'vfxStageCanvas';
  const soundList = el('div', 'vfxSounds');
  root.append(head, canvas, soundList);

  let preset = null, baked = null, backdrop = { color: '#0d0f12', id: 'dark' };
  let segments = [];
  let playing = false, started = 0, raf = 0, hitAt = -1;
  let stopSounds = [];
  let fired = new Set();

  const light = () => backdrop.id !== 'dark';

  function drawScene(ctx, shake) {
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.globalAlpha = 1;
    ctx.fillStyle = backdrop.color;
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = light() ? 'rgba(20,30,40,0.22)' : 'rgba(255,255,255,0.10)';
    ctx.fillRect(0, FLOOR, W, 2);
    if (preset?.kind !== 'projectile') return;
    const body = light() ? 'rgba(30,45,60,0.55)' : 'rgba(150,165,190,0.38)';
    // The caster (left) and the dummy the shot hits (right).
    ctx.fillStyle = body;
    roundRect(ctx, 44, FLOOR - 78, 40, 78, 10);
    const flash = shake > 0;
    ctx.fillStyle = flash ? (light() ? 'rgba(255,255,255,0.95)' : 'rgba(255,255,255,0.85)') : body;
    roundRect(ctx, W - 104 + (flash ? Math.sin(shake * 60) * 4 : 0), FLOOR - 96, 52, 96, 10);
  }

  function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, 0);
    ctx.arcTo(x, y + h, x, y, 0); ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath(); ctx.fill();
  }

  function drawFrame(ctx, lane, index, x, y) {
    const box = baked.boxes[lane.frameBoxIndices[Math.min(lane.frameBoxIndices.length - 1, index)]];
    if (box) ctx.drawImage(baked.canvas, box.x, box.y, box.w, box.h, x - FRAME / 2, y - FRAME / 2, FRAME, FRAME);
  }

  function idle() {
    const ctx = canvas.getContext('2d');
    drawScene(ctx, 0);
    // At rest: the first thing "Probar" will show, so the stage is never an empty box.
    const first = segments[0];
    if (first) { ctx.globalAlpha = 0.55; drawFrame(ctx, first.lane, 0, first.from.x, first.from.y); ctx.globalAlpha = 1; }
  }

  function stop() {
    playing = false;
    if (raf && typeof cancelAnimationFrame === 'function') cancelAnimationFrame(raf);
    raf = 0;
    stopSounds.forEach((s) => s());
    stopSounds = [];
    playBtn.textContent = '▶ Probar';
    if (baked) idle();
  }

  function tick(now) {
    if (!playing) return;
    const t = (now - started) / 1000;
    const ctx = canvas.getContext('2d');
    let acc = 0, current = null, local = 0;
    for (let i = 0; i < segments.length; i++) {
      if (t < acc + segments[i].seconds) { current = i; local = t - acc; break; }
      acc += segments[i].seconds;
    }
    if (current === null) {
      // Past the last segment: a short empty beat so the end reads as an end, then rest.
      drawScene(ctx, hitAt >= 0 && t - hitAt < 0.16 ? t - hitAt : 0);
      if (t > acc + 0.45) { stop(); return; }
      raf = requestAnimationFrame(tick);
      return;
    }

    const seg = segments[current];
    if (!fired.has(current)) {
      fired.add(current);
      // A loop's sound belongs to its segment: the next segment silences it.
      stopSounds.forEach((s) => s());
      stopSounds = [];
      if (seg.hit) hitAt = t;
      if (seg.sound && soundBox.checked) {
        const stopIt = playSound(seg.sound, { loop: !!seg.loopSound, gain: seg.loopSound ? 0.45 : 0.8 });
        if (seg.loopSound) stopSounds.push(stopIt);
      }
    }

    drawScene(ctx, hitAt >= 0 && t - hitAt < 0.16 ? t - hitAt : 0);
    const k = Math.min(1, local / seg.seconds);
    const n = seg.lane.frameBoxIndices.length;
    const frame = Math.floor(local * seg.lane.fps);
    drawFrame(ctx, seg.lane, seg.loop ? frame % n : Math.min(n - 1, frame),
      seg.from.x + (seg.to.x - seg.from.x) * k, seg.from.y + (seg.to.y - seg.from.y) * k);
    raf = requestAnimationFrame(tick);
  }

  function play() {
    if (!baked || !segments.length) return;
    stop();
    playing = true;
    fired = new Set();
    hitAt = -1;
    playBtn.textContent = '⏹ Parar';
    started = typeof performance !== 'undefined' ? performance.now() : 0;
    raf = requestAnimationFrame(tick);
  }

  playBtn.onclick = () => (playing ? stop() : play());

  function renderSounds() {
    soundList.innerHTML = '';
    const map = preset ? soundsOf(preset.id) : {};
    const moments = Object.keys(map);
    if (!moments.length) {
      soundList.appendChild(el('p', 'efHint', 'Este efecto no tiene sonidos de ejemplo.'));
      return;
    }
    soundList.appendChild(el('span', 'efHint', 'Sonidos de ejemplo (sintetizados; para sustituir por grabaciones de verdad):'));
    moments.forEach((moment) => {
      const id = map[moment];
      const def = SOUNDS[id];
      if (!def) return;
      const row = el('div', 'vfxSound');
      row.dataset.sound = id;
      row.appendChild(el('span', 'vfxSoundMoment', MOMENT_LABELS[moment] || moment));
      row.appendChild(el('span', 'vfxSoundName', `${def.label} · ${def.seconds.toFixed(2)} s${def.loop ? ' · bucle' : ''}`));
      const hear = el('button', 'small', '▶');
      hear.title = 'Oír sólo este sonido';
      hear.onclick = () => {
        const stopIt = playSound(id, { loop: false });
        setTimeout(stopIt, def.seconds * 1000 + 200);
      };
      const save = el('button', 'small', '⬇ .wav');
      save.title = 'Descargar este sonido';
      save.onclick = () => downloadWav(id);
      row.append(hear, save);
      soundList.appendChild(row);
    });
  }

  return {
    el: root,
    /** The effect to test: stops whatever was playing and shows it at rest. */
    setEffect(nextPreset, nextBaked, nextBackdrop) {
      stop();
      preset = nextPreset;
      baked = nextBaked;
      backdrop = nextBackdrop || backdrop;
      segments = buildTimeline(preset, baked);
      const total = segments.reduce((a, s) => a + s.seconds, 0);
      info.textContent = `${segments.map((s) => s.lane.name).join(' → ')} · ${total.toFixed(1)} s`;
      renderSounds();
      idle();
    },
    play,
    stop,
    isPlaying: () => playing,
  };
}
