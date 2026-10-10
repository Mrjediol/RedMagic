// Uso: en una carpeta temporal, `npm i node-web-audio-api@1.0.4` y luego
// node render-sonidos-cometa.cjs <docs/bocetos/ataque-cometa-estados.html> <Assets/Audio/Sounds/Player/Comet>
// Cambia aquí qué variante del boceto va a cada clip (CHV.c = carga turbina, RDV.a = listo descarga seca, RLV.a = disparo láser grave,
// SETS.B = impactos energía eléctrica, CHN.a = primer golpe de la cadena).
// Renderiza a WAV los sonidos del cometa elegidos en el boceto (docs/bocetos/ataque-cometa-estados.html),
// con el mismo código de síntesis WebAudio del boceto, en un OfflineAudioContext.
const fs = require('fs');
const wa = require('node-web-audio-api');
const [, , sketchPath, outDir] = process.argv;
const html = fs.readFileSync(sketchPath, 'utf8');
const a = html.indexOf('let AC = null, master = null');
const b = html.indexOf('// eventos de sonido de una escena');
if (a < 0 || b < 0) throw new Error('marcadores del boceto');
const synth = html.slice(a, b);

const prelude = `
const CH = 0.55; let HOLD = 0.5; const T0 = 0.35;
const document = { getElementById: () => ({ onchange: null, innerHTML: '', checked: false, value: '' }) };
const window = {};
`;
const driver = `
function makeCtx(seconds) {
  const sr = 48000;
  AC = new wa.OfflineAudioContext(2, Math.ceil(sr * seconds), sr);
  master = AC.createGain(); master.gain.value = 0.7;
  const comp = AC.createDynamicsCompressor(); comp.threshold.value = -16; comp.knee.value = 12; comp.ratio.value = 5; comp.attack.value = 0.002; comp.release.value = 0.18;
  master.connect(comp); comp.connect(AC.destination);
  rev = AC.createConvolver(); const rl = Math.floor(sr * 1.8), ir = AC.createBuffer(2, rl, sr);
  for (let c = 0; c < 2; c++) { const d = ir.getChannelData(c); let lp = 0; for (let i = 0; i < rl; i++) { lp += ((Math.random() * 2 - 1) * Math.pow(1 - i / rl, 3) - lp) * Math.max(0.05, 0.6 * (1 - i / rl)); d[i] = lp; } }
  rev.buffer = ir; rev.connect(master);
  const n = sr; noiseBuf = AC.createBuffer(1, n, sr); const nd = noiseBuf.getChannelData(0); for (let i = 0; i < n; i++) nd[i] = Math.random() * 2 - 1;
  humNodes = null;
}
function wav(buffer, from, to, trim) {
  const sr = buffer.sampleRate, ch = [buffer.getChannelData(0), buffer.getChannelData(1)];
  let s0 = Math.floor(from * sr), s1 = Math.min(ch[0].length, Math.floor(to * sr));
  if (trim) { let last = s0; for (let i = s0; i < s1; i++) if (Math.abs(ch[0][i]) > 0.004 || Math.abs(ch[1][i]) > 0.004) last = i; s1 = Math.min(s1, last + Math.floor(0.05 * sr)); }
  const frames = s1 - s0, out = Buffer.alloc(44 + frames * 4);
  out.write('RIFF', 0); out.writeUInt32LE(36 + frames * 4, 4); out.write('WAVE', 8); out.write('fmt ', 12);
  out.writeUInt32LE(16, 16); out.writeUInt16LE(1, 20); out.writeUInt16LE(2, 22); out.writeUInt32LE(sr, 24); out.writeUInt32LE(sr * 4, 28);
  out.writeUInt16LE(4, 32); out.writeUInt16LE(16, 34); out.write('data', 36); out.writeUInt32LE(frames * 4, 40);
  let peak = 0; for (let i = s0; i < s1; i++) peak = Math.max(peak, Math.abs(ch[0][i]), Math.abs(ch[1][i]));
  const g = peak > 0.98 ? 0.98 / peak : 1;
  for (let i = 0; i < frames; i++) for (let c = 0; c < 2; c++) out.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(ch[c][s0 + i] * g * 32767))), 44 + i * 4 + c * 2);
  return { out, peak };
}
async function render(name, seconds, fn, from = 0, to = seconds, trim = true) {
  makeCtx(seconds);
  fn();
  const buf = await AC.startRendering();
  const { out, peak } = wav(buf, from, to, trim);
  fs.writeFileSync(outDir + '/' + name + '.wav', out);
  console.log(name, (out.length - 44) / 4 / 48000 .toFixed ? ((out.length - 44) / 4 / 48000).toFixed(2) + 's' : '', 'peak', peak.toFixed(2));
}
(async () => {
  startHum = () => {};                                   // el zumbido va en su propio clip en bucle
  await render('comet_charge', 1.2, () => CHV.c.fn(1, CH - 0.04));
  await render('comet_ready', 1.6, () => RDV.a.fn(1));
  await render('comet_fire', 1.6, () => RLV.a.fn(1));
  await render('comet_impact', 2.2, () => SETS.B.impact(1));
  await render('comet_pierce_in', 1.4, () => SETS.B.pierceIn(1));
  await render('comet_pierce_out', 1.4, () => SETS.B.pierceOut(1));
  await render('comet_chain_stick', 1.6, () => CHN.a.stick(1, 0.25));
  await render('comet_chain_release', 1.6, () => CHN.a.out(1));
  // zumbido de mantener: estado estable de 2 s (330/333 Hz y la vibración de 7 Hz cierran ciclo cada 2 s → bucle sin corte)
  await render('comet_hold_loop', 4.2, () => {
    const t = 0, g = AC.createGain(); g.gain.value = 0.045;
    const l = AC.createOscillator(), lg = AC.createGain(); l.frequency.value = 7; lg.gain.value = 0.02; l.connect(lg); lg.connect(g.gain); l.start(t);
    const o1 = AC.createOscillator(), o2 = AC.createOscillator(); o1.frequency.value = 330; o2.frequency.value = 333; o1.connect(g); o2.connect(g); g.connect(master); o1.start(t); o2.start(t);
  }, 2.0, 4.0, false);
})().catch(e => { console.error(e); process.exit(1); });
`;
fs.writeFileSync(__dirname + '/combined.cjs', `const wa = require('node-web-audio-api'); const fs = require('fs'); const outDir = ${JSON.stringify(outDir)};\n` + prelude + synth + driver);
require(__dirname + '/combined.cjs');
