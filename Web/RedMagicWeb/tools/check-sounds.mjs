// tools/check-sounds.mjs
// -----------------------------------------------------------------------------
// What can be checked about a synthesised sound without ears: numbers. It says nothing about whether
// the sound is GOOD — only that it is not broken.
//
//   checkSound(samples, def, maxSeconds) → string[]   (empty = fine)
//     · every sample finite, peak in a sane range, not (nearly) silent
//     · a one-shot starts and ends at rest (no click) and has really died out by its end
//     · a loop closes: the last sample continues into the first
//     · length within its family's budget (loops are exempt)
//
//   node check-sounds.mjs        prints a table of every game sound (length, peak, RMS, tail)

import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const SR = 44100;

function rms(samples, from, to) {
  let sum = 0;
  for (let i = from; i < to; i++) sum += samples[i] * samples[i];
  return Math.sqrt(sum / Math.max(1, to - from));
}

export function measure(samples) {
  let peak = 0, finite = true, maxStep = 0;
  for (let i = 0; i < samples.length; i++) {
    if (!Number.isFinite(samples[i])) finite = false;
    peak = Math.max(peak, Math.abs(samples[i]));
    if (i > 0) maxStep = Math.max(maxStep, Math.abs(samples[i] - samples[i - 1]));
  }
  const tailLen = Math.min(samples.length, Math.floor(0.02 * SR));
  return {
    finite, peak, maxStep,
    rms: rms(samples, 0, samples.length),
    first: Math.abs(samples[0] ?? 0),
    last: Math.abs(samples[samples.length - 1] ?? 0),
    tail: rms(samples, samples.length - tailLen, samples.length),   // level of the last 20 ms
    seam: Math.abs((samples[0] ?? 0) - (samples[samples.length - 1] ?? 0)),
  };
}

export function checkSound(samples, def, maxSeconds) {
  if (!samples || !samples.length) return ['renders nothing'];
  const m = measure(samples);
  const issues = [];
  if (!m.finite) issues.push('non-finite samples');
  if (m.peak < 0.5 || m.peak > 0.9) issues.push(`peak ${m.peak.toFixed(2)} (expected ≈ 0.85)`);
  if (m.rms < 0.02) issues.push(`nearly silent (RMS ${m.rms.toFixed(3)})`);
  if (Math.abs(samples.length / SR - def.seconds) > 0.001) issues.push('length differs from `seconds`');
  if (def.loop) {
    if (m.seam > Math.max(0.12, m.maxStep)) issues.push(`loop does not close (seam ${m.seam.toFixed(2)}, largest step inside ${m.maxStep.toFixed(2)})`);
  } else {
    if (m.first > 0.25) issues.push(`starts on a click (first sample ${m.first.toFixed(2)})`);
    if (m.last > 0.01) issues.push(`ends on a click (last sample ${m.last.toFixed(3)})`);
    if (m.tail > 0.12) issues.push(`still loud at its end (last 20 ms RMS ${m.tail.toFixed(2)}): it is cut off — lengthen it`);
    if (def.seconds > maxSeconds) issues.push(`${def.seconds} s is over its family's budget (${maxSeconds} s)`);
  }
  return issues;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const { GAME_SOUNDS, GAME_FAMILIES, renderGameSound } = await import(pathToFileURL(path.join(here, '../modules/game-sounds.js')).href);
  let bad = 0;
  for (const [id, def] of Object.entries(GAME_SOUNDS)) {
    const samples = renderGameSound(id);
    const m = measure(samples);
    const issues = checkSound(samples, def, GAME_FAMILIES[def.family]?.max ?? 0);
    if (issues.length) bad++;
    console.log(`${issues.length ? '✗' : '✓'} ${id.padEnd(24)} ${def.family.padEnd(10)} ${def.seconds.toFixed(2)} s  peak ${m.peak.toFixed(2)}  rms ${m.rms.toFixed(3)}  tail ${m.tail.toFixed(3)}${def.loop ? '  loop' : ''}${issues.length ? `   ← ${issues.join('; ')}` : ''}`);
  }
  console.log(`${Object.keys(GAME_SOUNDS).length} sounds, ${bad} with problems`);
  process.exit(bad ? 1 : 0);
}
