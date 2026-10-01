# Plan — "quiero un enemigo así": from a request to a finished part-based enemy

Requested 2026-10-02, for a coming session. An experiment first: **find out whether Claude can get good
body parts out of Comfy Desktop**. If it can, the rest of the chain already exists (Enemy Studio →
export → Unity import, effects from the VFX Lab, synthesised sounds).

## ▶ How to start (after /clear)

> Lee `docs/plan-comfyui-enemy-workflow.md` y empieza por la Fase 0.

Read first: `CLAUDE.md`, this file, `docs/web-enemy-studio-plan.md`, `docs/architecture/procedural-rig.md`,
`Web/RedMagicWeb/ARCHITECTURE.md ▸ Enemy Studio tab` and `▸ Rig composer`, and the memory notes on enemies
(root pivot = body centre, sorting order starts at 1, one prefab with `RigDriverCode`, movement matches
the creature).

## The goal

The user writes one request ("a floating crystal golem that throws shards, slow, tanky") and Claude
delivers the enemy in the game: parts, body, gestures, behaviour, effects, sounds — the user only
reviews and playtests. Success is judged on **three enemies in a row** made this way, not on one.

## What already exists (do not rebuild)

- **Body + behaviour**: 🧬 Enemy Studio (templates, parts, pivots, gestures, effects on gesture phases,
  behaviour form) → `.zip` → Unity `Importar rig`. Its data shape is `rig-config` (`docs/schemas/`).
- **Effects**: ✨ VFX Lab presets; `tools/bake-vfx.mjs` bakes them without a browser.
- **Sounds**: `vfx-sounds.js` + `tools/bake-sounds.mjs`.
- **Background removal / part cutting**: the Sprites tab's chroma key and despill code
  (`background-removal.js`, `despill-edge.js`, `sprite-detection.js`) — DOM-free enough to run in node.

## What is unknown (Phase 0 answers these before anything is built)

1. **Can Claude drive Comfy Desktop?** It is installed on this machine. ComfyUI exposes an HTTP API
   (`POST /prompt` with a workflow in API format, `GET /history/<id>`, `GET /view`); find the port the
   Desktop app listens on and whether the app must be open. Fallback: the user queues a saved workflow
   by hand and Claude only reads the output folder.
2. **Which models does the user have**, and which give painted, HD, on-style results (the game's art
   direction: `docs/GDD.md`; reference parts: `Assets/Art/RigImports/IceRobot/`, `koros/`)?
3. **How to get PARTS, not a creature.** Candidates to test, cheapest first:
   - a *parts sheet*: one image, the creature already taken apart (head, torso, each limb) on a flat
     magenta background — then the existing chroma-key + detection code cuts it;
   - one generation per part with a shared style reference (IP-Adapter / reference image) so they match;
   - a whole creature + automatic segmentation into parts (hardest to get clean joints).
4. **Consistency and joints**: do the parts match in style, scale and lighting, and do their joints
   overlap well enough for the rig (a limb needs a rounded end that hides under the body)?
5. **Can Claude judge the result?** It can look at images; decide what it checks (silhouette, clean
   alpha, part count vs. template slots, left/right pairs, scale) and what stays the user's call.

## Phases

- **Phase 0 — probe (≈ 1 session, no pipeline code).** Answer the five unknowns with real generations.
  Deliverable: a section "Findings" here with the working recipe (model, workflow, prompt skeleton,
  resolution, how many tries per usable part) **or** an honest "not good enough yet, because…".
  Stop and show the user the parts before going on.
- **Phase 1 — the parts tool.** `tools/make-parts.mjs`: request + template → Comfy workflow → raw
  images → cut / key / despill → named PNGs per template slot in a folder. Data-driven (a prompt
  skeleton per template: quadruped, floater, turret…), reusable, never a one-off.
- **Phase 2 — assembly without the browser.** Build the Studio document from a template + the parts
  folder in node (reuse `rig-templates.js`, `rig-model.js`, `rig-export.js`, `enemy-bundle.js`): place
  parts in slots, guess pivots from the art (the template code already does this), write the same
  `.zip` the Studio exports. The user can still open it in the Studio to adjust.
- **Phase 3 — behaviour, effects, sounds from the request.** Map the request's words to a template's
  behaviour, pick Lab presets by element for the gesture phases and the projectile, bake them, pick /
  synthesise sounds. One JSON "enemy brief" records every choice so it can be re-run or edited.
- **Phase 4 — one command.** `node tools/make-enemy.mjs "<request>"` → brief → parts → zip → Unity import
  (compile check only; the user playtests). Then the three-enemies-in-a-row test.

## Rules

- The user's test assets and existing enemies are never overwritten: new enemies get new names.
- Unity imports are not verified by running the game (standing rule): compile + console, then a
  numbered playtest list in `docs/part-rig-pending-playtests.md`.
- If Phase 0 says the parts are not good enough, stop there and say so — do not build Phases 1–4 on
  parts that need hand-fixing every time. A middle road is fine: the user generates the parts sheet,
  Claude does everything after it.
- Ties in with `docs/idea-fracture.md`: if creatures are *fractured* by design, visible seams between
  parts become style instead of a defect, which lowers the bar for joints considerably.

## Findings

(to be written in Phase 0)

## Decisions

(to be filled while working)
