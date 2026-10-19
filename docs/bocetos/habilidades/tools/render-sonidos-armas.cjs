// tools/render-sonidos-armas.cjs — pasa a WAV los sonidos de las armas del arsenal (Unity) con el MISMO código de síntesis de la
// web (sound.js + el bloque sound(...) de cada a*-*.js), en un OfflineAudioContext de node-web-audio-api (como el cometa).
// Uso: WEB_AUDIO_API=<ruta a node_modules/node-web-audio-api> node docs/bocetos/habilidades/tools/render-sonidos-armas.cjs <salida>
//   (instalar una vez en una carpeta temporal: npm i node-web-audio-api@1.0.4)
// Los nombres de archivo son los ids que usa Unity (ArsenalSounds.Play("fan_hit")…); los bucles (ray_loop, moon_spin) se cierran
// con un fundido cruzado para que no hagan clic. Tabla id → pieza de la web: SOUNDS, abajo.
const fs = require('fs'), path = require('path'), vm = require('vm');
const wa = require(process.env.WEB_AUDIO_API || 'node-web-audio-api');   // o la ruta de la instalación en WEB_AUDIO_API
const dir = path.join(__dirname, '..'), out = process.argv[2];
if (!out) { console.log('falta la carpeta de salida'); process.exit(1); }
fs.mkdirSync(out, { recursive: true });

const html = fs.readFileSync(path.join(dir, 'index.html'), 'utf8');
const files = [...html.matchAll(/<script src="([^"]+)"/g)].map(m => m[1]).filter(f => f !== 'ui.js' && f !== 'systems.js');
const sb = { console, Math, performance: { now: () => 0 }, window: {}, document: { getElementById: () => ({}) }, setTimeout: () => 0, wa, fs, path, Buffer, out };
vm.createContext(sb);
for (const f of files) vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sb, { filename: f });

// id de Unity → [segundos de render, función(k), opciones]
const driver = `
const seedTickFn = (k, i) => { const tn = nowT(); vNoise(tn, 0.02 * k, 0.25, 'bandpass', 2600 + i * 500, 2600 + i * 500, 6, { att: 0.001 }); vTone(tn, 220 * Math.pow(1.26, i), 200 * Math.pow(1.26, i), 0.12 * k, 0.12, 'sine', 0.1); };
const SOUNDS = {
  fan_release: [1.2, k => SND.fan_release.fn(k)], fan_hit: [1, k => SND.fan_hit.fn(k)], fan_ricochet: [1, k => SND.fan_ricochet.fn(k)],
  fan_stick: [1, k => SND.fan_stick.fn(k)], fan_ground: [1, k => SND.fan_ground.fn(k)], fan_detonate: [1.2, k => SFXLIB.hitEnergy(k, 0.6)],
  fan_rip: [1, k => SND.fan_rip.fn(k)], fan_absorb: [1, k => SND.fan_absorb.fn(k)],
  sing_launch: [1.4, k => SND.sing_launch.fn(k)], sing_tear: [1.6, k => SND.sing_tear.fn(k)], sing_capture: [1, k => SND.sing_capture.fn(k)],
  sing_collapse: [1.6, k => SND.sing_collapse.fn(k)], sing_burst: [2.2, k => SND.sing_burst.fn(k)], sing_burst_fire: [2.2, k => SND.sing_burst_fire.fn(k)],
  sing_eject: [1.2, k => SND.sing_eject.fn(k)],
  sing_feed: [1.2, k => { const tn = nowT(); vThump(tn, 60, 30, 0.35 * k, 0.55, 2, 0.3); vNoise(tn, 0.25 * k, 0.2, 'lowpass', 1200, 200, 1, { wet: 0.3 }); }],
  sing_merge: [1.4, k => { const tn = nowT(); vThump(tn, 70, 35, 0.4 * k, 0.6, 2, 0.3); vNoise(tn, 0.3 * k, 0.25, 'bandpass', 300, 2500, 1, { rising: true, wet: 0.3 }); }],
  sing_bolt: [1, k => { const tn = nowT(); vThump(tn, 110, 45, 0.25 * k, 0.4, 2, 0.2); vNoise(tn, 0.2 * k, 0.2, 'bandpass', 800, 3000, 1.2, { att: 0.005, wet: 0.2 }); }],
  ray_ignite: [1, k => SND.ray_ignite.fn(k)], ray_off: [1, k => SND.ray_off.fn(k)],
  ray_tick: [0.5, k => { const tn = nowT(); vThump(tn, 130, 70, 0.07 * k, 0.16, 1.5, 0.05); }],
  ray_sweet: [1.6, k => { SFXLIB.hitEnergy(k, 1.3); SND.ray_split.fn(k); }], ray_discharge: [1.4, k => SFXLIB.hitEnergy(k, 1.2)],
  ray_wave: [2, k => SFXLIB.boom(k, 0.4)], ray_plant: [1.2, k => SND.ray_split.fn(k)], ray_nova: [2, k => SFXLIB.boom(k, 0.6)],
  ray_loop: [3.2, k => SND.ray_sustain.fn(3.2), { loop: [0.5, 2.5] }],
  moon_throw: [1, k => SND.moon_throw.fn(k)], moon_hit: [1, k => SND.moon_hit.fn(k)], moon_catch: [1, k => SND.moon_catch.fn(k)],
  moon_spin: [2.6, k => { const tn = nowT(); vNoise(tn, 2.5, 0.14, 'bandpass', 1200, 1250, 2.2, { hold: true, att: 0.05, lfo: [15, 0.7], wet: 0.1 }); }, { loop: [0.4, 2.2] }],
  moon_perfect: [1.4, k => { SND.moon_catch.fn(k); SFXLIB.glass(k, 1); }], moon_fumble: [0.8, k => SFXLIB.whoosh(k, 500, 200, 0.12, 0.12)],
  moon_block: [1, k => SFXLIB.glass(k, 0.5)], moon_grind: [0.6, k => SFXLIB.whoosh(k, 3200, 1600, 0.08, 0.1)],
  seed_lob: [1, k => SND.seed_lob.fn(k)], seed_land: [1, k => SND.seed_land.fn(k)], seed_armed: [1, k => SND.seed_armed.fn(k)],
  seed_tick0: [0.6, k => seedTickFn(k, 0)], seed_tick1: [0.6, k => seedTickFn(k, 1)], seed_tick2: [0.6, k => seedTickFn(k, 2)],
  seed_trigger: [1, k => SND.seed_trigger.fn(k)], seed_roots: [1, k => SFXLIB.whoosh(k, 600, 200, 0.25, 0.2)], seed_ripe: [1, k => SND.seed_armed.fn(k)],
  seed_boom: [2.2, k => SND.seed_boom.fn(k)], seed_split: [2, k => SND.seed_split.fn(k)], seed_mini: [1.2, k => SND.seed_mini.fn(k)], seed_nova: [2.2, k => SND.seed_nova.fn(k)],
  star_up: [1, k => SND.star_up.fn(k)], star_mark: [1.2, k => SND.star_mark.fn(k)], star_fall: [1.2, k => SND.star_fall.fn(k)],
  star_impact: [2.4, k => SND.star_impact.fn(k)], star_needle: [0.8, k => SND.star_needle.fn(k)],
  react_shatter: [1.4, k => SFXLIB.shatter(k, 1.1)], react_freeze: [1, k => SFXLIB.freeze(k, 0.3)],
  react_vapor: [1.2, k => { const tn = nowT(); vNoise(tn, 0.5 * k, 0.35, 'bandpass', 2500, 600, 0.6, { att: 0.005, wet: 0.3 }); vThump(tn, 90, 40, 0.3 * k, 0.5, 2); }],
};
function makeCtx(seconds) {
  const sr = 48000;
  AC = new wa.OfflineAudioContext(2, Math.ceil(sr * seconds), sr);
  master = AC.createGain(); master.gain.value = 0.7;
  const comp = AC.createDynamicsCompressor(); comp.threshold.value = -16; comp.knee.value = 12; comp.ratio.value = 5; comp.attack.value = 0.002; comp.release.value = 0.18;
  master.connect(comp); comp.connect(AC.destination);
  rev = AC.createConvolver(); const rl = Math.floor(sr * 1.8), ir = AC.createBuffer(2, rl, sr);
  for (let c = 0; c < 2; c++) { const d = ir.getChannelData(c); let lp = 0; for (let i = 0; i < rl; i++) { lp += ((Math.random() * 2 - 1) * Math.pow(1 - i / rl, 3) - lp) * Math.max(0.05, 0.6 * (1 - i / rl)); d[i] = lp; } }
  rev.buffer = ir; rev.connect(master);
  noiseBuf = AC.createBuffer(1, sr, sr); const nd = noiseBuf.getChannelData(0); for (let i = 0; i < sr; i++) nd[i] = Math.random() * 2 - 1;
}
function wav(ch, sr) {
  const frames = ch[0].length, o = Buffer.alloc(44 + frames * 4);
  o.write('RIFF', 0); o.writeUInt32LE(36 + frames * 4, 4); o.write('WAVE', 8); o.write('fmt ', 12);
  o.writeUInt32LE(16, 16); o.writeUInt16LE(1, 20); o.writeUInt16LE(2, 22); o.writeUInt32LE(sr, 24); o.writeUInt32LE(sr * 4, 28);
  o.writeUInt16LE(4, 32); o.writeUInt16LE(16, 34); o.write('data', 36); o.writeUInt32LE(frames * 4, 40);
  let peak = 0; for (let i = 0; i < frames; i++) peak = Math.max(peak, Math.abs(ch[0][i]), Math.abs(ch[1][i]));
  const g = peak > 0.98 ? 0.98 / peak : 1;
  for (let i = 0; i < frames; i++) for (let c = 0; c < 2; c++) o.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(ch[c][i] * g * 32767))), 44 + i * 4 + c * 2);
  return { o, peak };
}
async function renderAll() {
  for (const [id, [secs, fn, opt]] of Object.entries(SOUNDS)) {
    makeCtx(secs); fn(1);
    const buf = await AC.startRendering(), sr = buf.sampleRate, src = [buf.getChannelData(0), buf.getChannelData(1)];
    let ch;
    if (opt && opt.loop) {          // bucle: tramo central con fundido cruzado del final sobre el principio
      const a = Math.floor(opt.loop[0] * sr), b = Math.floor(opt.loop[1] * sr), xf = Math.floor(0.12 * sr), n = b - a;
      ch = src.map(s => { const r = new Float32Array(n); for (let i = 0; i < n; i++) r[i] = s[a + i]; for (let i = 0; i < xf; i++) { const u = i / xf; r[i] = r[i] * u + s[b + i] * (1 - u); } return r; });
    } else {                         // recorte del silencio final
      let last = 0; for (let i = 0; i < src[0].length; i++) if (Math.abs(src[0][i]) > 0.004 || Math.abs(src[1][i]) > 0.004) last = i;
      const n = Math.min(src[0].length, last + Math.floor(0.05 * sr));
      ch = src.map(s => s.slice(0, n));
    }
    const { o, peak } = wav(ch, sr);
    fs.writeFileSync(path.join(out, id + '.wav'), o);
    console.log(id.padEnd(16), (ch[0].length / sr).toFixed(2) + 's', 'pico', peak.toFixed(2));
  }
}
renderAll().catch(e => console.log('ERROR', e.stack));
`;
vm.runInContext(driver, sb, { filename: 'driver' });
