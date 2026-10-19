// sound.js — síntesis WebAudio para elegir carácter (misma base que el cometa: capas con peso, sin pitidos de arcade).
// k = estiramiento en cámara lenta (1 = tiempo real). Cada habilidad registra sus piezas en SND con sound(id, nombre, fn).
'use strict';
let AC = null, master = null, noiseBuf = null, rev = null, recDest = null;
function audioInit() {
  if (AC) { if (AC.state === 'suspended') AC.resume(); return; }
  AC = new (window.AudioContext || window.webkitAudioContext)(); master = AC.createGain(); master.gain.value = 0.7;
  const comp = AC.createDynamicsCompressor(); comp.threshold.value = -16; comp.knee.value = 12; comp.ratio.value = 5; comp.attack.value = 0.002; comp.release.value = 0.18;
  master.connect(comp); comp.connect(AC.destination);
  recDest = AC.createMediaStreamDestination(); comp.connect(recDest);          // para grabar el clip con sonido
  rev = AC.createConvolver(); const rl = Math.floor(AC.sampleRate * 1.8), ir = AC.createBuffer(2, rl, AC.sampleRate);
  for (let c = 0; c < 2; c++) { const d = ir.getChannelData(c); let lp = 0; for (let i = 0; i < rl; i++) { lp += ((Math.random() * 2 - 1) * Math.pow(1 - i / rl, 3) - lp) * Math.max(0.05, 0.6 * (1 - i / rl)); d[i] = lp; } }
  rev.buffer = ir; rev.connect(master);
  const n = AC.sampleRate; noiseBuf = AC.createBuffer(1, n, AC.sampleRate); const d = noiseBuf.getChannelData(0); for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
}
const X = 0.0001;
const nowT = () => AC.currentTime + 0.01;
function sendOut(node, wet) {
  node.connect(master);
  if (wet > 0) { const w = AC.createGain(); w.gain.value = wet; node.connect(w); w.connect(rev); }
}
const curves = {};
function driveNode(amount) {
  if (!curves[amount]) { const n = 2048, c = new Float32Array(n); for (let i = 0; i < n; i++) { const x = i * 2 / n - 1; c[i] = Math.tanh(x * amount) / Math.tanh(amount); } curves[amount] = c; }
  const ws = AC.createWaveShaper(); ws.curve = curves[amount]; ws.oversample = '2x'; return ws;
}
// golpe grave con caída de tono
function vThump(t0, f0, f1, dur, peak, dr = 0, wet = 0.1) {
  const o = AC.createOscillator(), g = AC.createGain(); o.type = 'sine';
  o.frequency.setValueAtTime(f0, t0); o.frequency.exponentialRampToValueAtTime(f1, t0 + dur * 0.6);
  g.gain.setValueAtTime(X, t0); g.gain.exponentialRampToValueAtTime(peak, t0 + 0.003); g.gain.exponentialRampToValueAtTime(X, t0 + dur);
  o.connect(g); let last = g; if (dr > 0) { const ws = driveNode(dr); g.connect(ws); last = ws; }
  sendOut(last, wet); o.start(t0); o.stop(t0 + dur + 0.05);
}
// ruido filtrado; o = { rising, att, dr, wet, pan:[a,b], lfo:[hz, depth] }
function vNoise(t0, dur, peak, type, f0, f1, q = 1, o = {}) {
  const s = AC.createBufferSource(); s.buffer = noiseBuf; s.loop = true;
  const f = AC.createBiquadFilter(); f.type = type; f.Q.value = q; f.frequency.setValueAtTime(f0, t0); f.frequency.exponentialRampToValueAtTime(f1, t0 + dur);
  const g = AC.createGain(); g.gain.setValueAtTime(X, t0);
  if (o.rising) { g.gain.exponentialRampToValueAtTime(peak, t0 + dur * 0.95); g.gain.exponentialRampToValueAtTime(X, t0 + dur + 0.03); }
  else if (o.hold) { g.gain.exponentialRampToValueAtTime(peak, t0 + (o.att || 0.03)); g.gain.setValueAtTime(peak, t0 + dur * 0.85); g.gain.exponentialRampToValueAtTime(X, t0 + dur); }
  else { g.gain.exponentialRampToValueAtTime(peak, t0 + (o.att || 0.002)); g.gain.exponentialRampToValueAtTime(X, t0 + dur); }
  s.connect(f); f.connect(g); let last = g;
  if (o.lfo) { const am = AC.createGain(), l = AC.createOscillator(), lg = AC.createGain(); am.gain.value = 1 - o.lfo[1] / 2; l.frequency.value = o.lfo[0]; lg.gain.value = o.lfo[1] / 2; l.connect(lg); lg.connect(am.gain); last.connect(am); last = am; l.start(t0); l.stop(t0 + dur + 0.06); }
  if (o.dr) { const ws = driveNode(o.dr); last.connect(ws); last = ws; }
  if (o.pan) { const pn = AC.createStereoPanner(); pn.pan.setValueAtTime(o.pan[0], t0); pn.pan.linearRampToValueAtTime(o.pan[1], t0 + dur); last.connect(pn); last = pn; }
  sendOut(last, o.wet || 0); s.start(t0, Math.random() * 0.5); s.stop(t0 + dur + 0.06);
}
// diente de sierra con filtro que barre
function vSaw(t0, f0, f1, dur, peak, lp0, lp1, dr = 0, wet = 0.1, o = {}) {
  const os = AC.createOscillator(); os.type = o.type || 'sawtooth'; os.frequency.setValueAtTime(f0, t0); os.frequency.exponentialRampToValueAtTime(f1, t0 + dur);
  const f = AC.createBiquadFilter(); f.type = 'lowpass'; f.Q.value = o.q || 1; f.frequency.setValueAtTime(lp0, t0); f.frequency.exponentialRampToValueAtTime(lp1, t0 + dur);
  const g = AC.createGain(); g.gain.setValueAtTime(X, t0);
  if (o.hold) { g.gain.exponentialRampToValueAtTime(peak, t0 + (o.att || 0.03)); g.gain.setValueAtTime(peak, t0 + dur * 0.85); g.gain.exponentialRampToValueAtTime(X, t0 + dur); }
  else if (o.rising) { g.gain.exponentialRampToValueAtTime(peak, t0 + dur * 0.95); g.gain.exponentialRampToValueAtTime(X, t0 + dur + 0.04); }
  else { g.gain.exponentialRampToValueAtTime(peak, t0 + (o.att || 0.006)); g.gain.exponentialRampToValueAtTime(X, t0 + dur); }
  os.connect(f); f.connect(g); let last = g;
  if (o.vib) { const l = AC.createOscillator(), lg = AC.createGain(); l.frequency.value = o.vib[0]; lg.gain.value = o.vib[1]; l.connect(lg); lg.connect(os.frequency); l.start(t0); l.stop(t0 + dur + 0.06); }
  if (o.trem) { const am = AC.createGain(), l = AC.createOscillator(), lg = AC.createGain(); am.gain.value = 0.6; l.frequency.value = o.trem; lg.gain.value = 0.4; l.connect(lg); lg.connect(am.gain); last.connect(am); last = am; l.start(t0); l.stop(t0 + dur + 0.06); }
  if (dr > 0) { const ws = driveNode(dr); last.connect(ws); last = ws; }
  sendOut(last, wet); os.start(t0); os.stop(t0 + dur + 0.06);
}
// parciales inarmónicos (cristal / metal)
function vRing(t0, freqs, dur, peak, wet = 0.2) {
  freqs.forEach((fr, i) => {
    const o = AC.createOscillator(), g = AC.createGain(); o.type = 'sine'; o.frequency.value = fr;
    g.gain.setValueAtTime(X, t0); g.gain.exponentialRampToValueAtTime(peak / (1 + i * 0.4), t0 + 0.002); g.gain.exponentialRampToValueAtTime(X, t0 + dur * (1 - i * 0.1));
    o.connect(g); sendOut(g, wet); o.start(t0); o.stop(t0 + dur + 0.05);
  });
}
// chisporroteo
function vCrackle(t0, dur, peak, count, wet = 0.2, f = [2000, 5000]) {
  for (let i = 0; i < count; i++) {
    const tt = t0 + Math.random() * dur, a = peak * (0.3 + Math.random() * 0.7) * (1 - (tt - t0) / dur * 0.7), fr = f[0] + Math.random() * (f[1] - f[0]);
    vNoise(tt, 0.012 + Math.random() * 0.01, a, 'highpass', fr, fr * 1.3, 1, { att: 0.001, wet });
  }
}
// tono que sube de volumen hasta el final (riser)
function vRise(t0, type, f0, f1, dur, peak, lp0, lp1, dr = 0, wet = 0.1, q = 1) { vSaw(t0, f0, f1, dur, peak, lp0, lp1, dr, wet, { type, rising: true, q }); }
// sinusoide corta (cuerpo de campanilla); att en s
function vTone(t0, f0, f1, dur, peak, type = 'sine', wet = 0.15, att = 0.004) {
  const o = AC.createOscillator(), g = AC.createGain(); o.type = type; o.frequency.setValueAtTime(f0, t0); o.frequency.exponentialRampToValueAtTime(f1, t0 + dur);
  g.gain.setValueAtTime(X, t0); g.gain.exponentialRampToValueAtTime(peak, t0 + att); g.gain.exponentialRampToValueAtTime(X, t0 + dur);
  o.connect(g); sendOut(g, wet); o.start(t0); o.stop(t0 + dur + 0.05);
}
// chasquidos que se aceleran y suben
function rampTicks(t0, d, n, p0, p1, f0, f1, wet = 0.1) {
  for (let i = 0; i < n; i++) { const u = i / n, tt = t0 + d * (1 - Math.pow(1 - u, 2)), f = f0 + (f1 - f0) * u; vNoise(tt, 0.012, p0 + (p1 - p0) * u, 'bandpass', f, f, 5, { att: 0.001, wet }); }
}

// ---------------------------------------------------------------- piezas reutilizables
const SFXLIB = {
  castShort(k) { const t = nowT(), d = 0.25 * k; vNoise(t, d, 0.12, 'bandpass', 600, 4000, 1, { rising: true, wet: 0.15 }); vRise(t, 'triangle', 160, 520, d, 0.07, 400, 2500); },
  whoosh(k, f0 = 1800, f1 = 400, dur = 0.22, peak = 0.22, pan = [-0.3, 0.4]) { const t = nowT(); vNoise(t, dur * k, peak, 'bandpass', f0, f1, 1.2, { att: 0.01, wet: 0.12, pan }); },
  hitEnergy(k, w = 1) { const t = nowT(); vThump(t, 120, 40, 0.3 * k, 0.6 * w, 2.5, 0.12); vNoise(t, 0.2 * k, 0.32 * w, 'bandpass', 5000, 400, 0.8, { dr: 1.5, wet: 0.2 }); vCrackle(t, 0.2 * k, 0.18 * w, 18, 0.2); },
  boom(k, w = 1) { const t = nowT(); vThump(t, 95, 28, 0.7 * k, 0.95 * w, 3, 0.2); vNoise(t, 0.55 * k, 0.5 * w, 'lowpass', 3000, 120, 0.8, { att: 0.002, dr: 2.5, wet: 0.35 }); vCrackle(t + 0.02 * k, 0.5 * k, 0.25 * w, 40, 0.3); vNoise(t + 0.05 * k, 0.9 * k, 0.12 * w, 'lowpass', 600, 100, 1, { wet: 0.5 }); },
  glass(k, w = 1) { const t = nowT(); vRing(t, [2630, 3910, 5270, 6810], 0.35 * k, 0.07 * w, 0.25); vNoise(t, 0.05 * k, 0.25 * w, 'highpass', 5000, 9000, 1, { att: 0.001 }); },
  shatter(k, w = 1) { const t = nowT(); vThump(t, 140, 50, 0.25 * k, 0.5 * w, 2); vNoise(t, 0.35 * k, 0.45 * w, 'highpass', 2500, 7000, 0.8, { att: 0.001, wet: 0.25 }); vRing(t, [1870, 2950, 4410, 6230], 0.6 * k, 0.07 * w, 0.35); vCrackle(t, 0.5 * k, 0.3 * w, 50, 0.3, [3000, 8000]); },
  freeze(k, dur = 0.5) { const t = nowT(); vNoise(t, dur * k, 0.12, 'highpass', 3000, 9000, 1, { rising: true, wet: 0.3 }); vCrackle(t, dur * k, 0.12, 30, 0.3, [4000, 9000]); vTone(t + dur * k * 0.8, 1760, 1760, 0.4 * k, 0.04, 'sine', 0.4); },
  burn(k, dur = 0.8) { const t = nowT(); vNoise(t, dur * k, 0.16, 'bandpass', 900, 500, 0.7, { hold: true, att: 0.06, wet: 0.15, lfo: [9, 0.5] }); vCrackle(t, dur * k, 0.18, Math.round(30 * dur), 0.15, [1200, 3500]); },
};

// registro para la pestaña de sonidos: SND[id] = { name, group, fn(k) }
const SND = {};
function sound(id, group, name, fn) { SND[id] = { group, name, fn }; return fn; }
