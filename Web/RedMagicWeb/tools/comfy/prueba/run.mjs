// Batch style test: node run.mjs [--lora file] [--strength 0.8] [--iters 2] [--only id,id]
// Builds each enemy's base prompt (enemy-prompt.mjs) with a cartoon style line and queues the Claude workflow.
// Prompts land in ./prompts/, images in Comfy's output/claude/prueba/.
import { buildEnemyPrompt } from '../../enemy-prompt.mjs';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';

const a = process.argv.slice(2);
const get = (f, d) => (a.includes(f) ? a[a.indexOf(f) + 1] : d);
const lora = get('--lora');
const strength = get('--strength', '0.8');
const iters = Number(get('--iters', 2));
const only = get('--only')?.split(',');
const here = (p) => new URL(p, import.meta.url);
const list = JSON.parse(fs.readFileSync(here('./enemies.json'), 'utf8')).filter((e) => !only || only.includes(e.id));

const REAL = 'Painted digital illustration, stylized semi-realistic, slight texture, no grain/haze outside the character.';
const CARTOON = 'Hand-drawn 2D cartoon game art, flat cel-shaded colors, bold thick outlines, simplified chunky shapes, like a polished mobile game sprite, NOT realistic, no grain/haze outside the character.';
fs.mkdirSync(here('./prompts/'), { recursive: true });

for (const e of list) {
  let p = buildEnemyPrompt({ ...e, target: 'comfy' });
  if (!p.includes(REAL)) throw new Error('style line changed in enemy-prompt.mjs');
  p = (lora ? 'cine1p, ' : '') + p.replace(REAL, CARTOON);
  fs.writeFileSync(here(`./prompts/${e.id}.txt`), p);
  for (let i = 1; i <= iters; i++) {
    const args = ['claude-workflow.mjs', 'run', '--size', '1024x1024', '--no-cut', '--name', `prueba/${e.id}_${i}`, '--prompt', p,
      ...(lora ? ['--lora', lora, '--lora-strength', strength] : [])];
    const r = spawnSync('node', args, { cwd: new URL('..', import.meta.url), encoding: 'utf8' });
    console.log(e.id, i, (r.stdout + r.stderr).trim().split('\n').pop());
  }
}
