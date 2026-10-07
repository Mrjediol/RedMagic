# rigtrans — Unity plays part-state transitions

## State (2026-10-07)
- The web (👾 Enemigos ▸ sheet ▸ 🎭 Estados de piezas) asks for a part state with **N frames**.
- make-enemy (SKILL ▸ "Estados pedidos") draws them: `<Slot>__<state>.t1.png … .t<N-1>.png` + the final
  `<Slot>__<state>.png`, all in the parts' `final/` folder (same canvas, part's silhouette).
- `make-enemy.mjs` puts only the final state in the zip and skips the `.t<k>` frames (a `·` note in its output).
- Unity swaps the sprite at once (`RigSpriteSwap` → `ProceduralRig.RefreshSprites`): no transition.

## Left
1. Format: the frames per state in the rig document (`rig-model.js` variants: `transitions[stateId] = [img…]`),
   `make-enemy.mjs` reads `.t<k>` into it, the bundle exports them, `RigSpriteSwap` stores them per state.
2. `ProceduralRig.RefreshSprites`: when a renderer goes base → state with frames, play t1…final over a short
   time (field on the swap, ~0.05 s per frame), and backwards when it leaves. Pool-safe (`RestorePose` resets).
3. Importer writes them; 🧬 Enemy Studio shows / edits them.

## Decisions
- The last frame IS the state sprite: an enemy without transition support still looks right.
