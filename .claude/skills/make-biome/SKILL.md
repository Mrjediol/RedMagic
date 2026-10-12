---
name: make-biome
description: Make the level art of a new RedMagic biome (forest, volcano…) end to end, reusing what was learnt with ice — parallax layers in Gemini, ComfyUI cleanup, terrain rock texture and rim colours, wall/door, particles, Unity import into the Level system. Use when the user asks to start a biome / its backgrounds / its terrain, or to redo one step of it.
---

# make-biome — biome → levels in Unity

Ice is the worked example (2026-10-10/11). **Do not re-derive the process; follow it.** Details and the full
"what failed" list: `docs/biomas-receta.md`; Unity system: `docs/architecture/levels.md`; ice prompts/results:
`docs/niveles-prompts-hielo.md`.

## Rules learnt the hard way (user feedback)
- **Prompts always pasted in the chat**, ready to copy. Docs only keep them as reference.
- **One step at a time**, validate with the user before the next (one layer, one texture…). Show it mounted, not alone.
- Gemini: no reference images; one piece per request; flat **magenta #FF00FF** where transparent; **lateral magenta
  margins** (≥ 8-10 %) so it can be mirrored; "ONE single …, no repeated copies" (it tiles 2×2 otherwise); pick
  16:9 / 1:1 / 9:16 in its selector and ask for a band (it ignores 8:1).
- Tall pieces: proportion matters, not pixels (Qwen ≤ 2048/side) → ask it **narrow** (`--size 256x2048`) and `--upscale`.
- Background layers: **no outlines/highlights** on far layers (or they read as terrain); occupancy shrinks as layers
  get closer (100 / 45-50 / 35-40 / 25-30 / 15-20 %).
- Terrain: the **rim is procedural** (code), never AI strips bent along the contour (wedges, overlaps). AI gives only
  the tileable rock texture and loose decor. No outer glow, no floor depth, no blurred halo shadows (all rejected).
- Everything of the level draws on **Default with negative orders** (`LevelSorting`); enemies are on Default 0..20.

## Steps
1. **Parallax, layer by layer** (prompts in chat; template in `biomas-receta.md` §Fondo):
   far full background (no magenta) → far isolated shapes → pillars/trunks (straight shaft, 1:1, ~3 slim) → ceiling
   hangers (anchored to ceiling) → near silhouettes (optional, in front). Mount each in the web tab **🏞️ Fondos**
   (paste/drag; anchors proportional/techo/suelo; espejo; extremos del nivel; atenuar/difuminar) with the user.
2. **Wall + door** as layers 6-7 ("extremos del nivel", anchored to floor). Best source so far: cut them out of a good
   Gemini map with ComfyUI (`claude-workflow.mjs run --ref <crop> --keep-size` "keep ONLY … exactly as drawn …
   same thick dark navy outlines … pure white background"), piece by piece; tall wall via the narrow+upscale trick.
   ⚠ The ice door doesn't sit on the flat rim — see `docs/tareas/niveles-hielo.md` step 4 before making another.
3. **Clean every image** (from the ORIGINAL with magenta/white bg): `node claude-workflow.mjs finish --ref <img> --name <p>`
   (×4 + RMBG) → if a pink rim remains `node despill.mjs <p>_cut.png out.png` (touches only a 10 px band at the
   transparent edge). Review side by side with the user in `Web/ParallaxTest/comparar-recortes.html` (original vs new, zoom
   to pixel, bg black/white/magenta/game blue). Load PNGs with
   **Subir/drag, never Ctrl+V** (Windows drops alpha). `clean` workflow and erode exist but were rejected for ice.
4. **Save it in the Biblioteca** (💾 in 🏞️ Fondos) and **export** it (`⬇ Exportar a Unity` → `*.parallax.json`; transparent layers go as PNG).
5. **Rock texture** (T1 template in `biomas-receta.md`): seamless 1:1, flat light, no outline. Copy to
   `Assets/Levels/_Biomas/<Bioma>/roca.*`.
6. **Unity**: create `LevelBiome` (copy `Hielo.asset`): rock texture, 4 floor + 4 ceiling band colours, outline,
   inner glow, widths (⚠ `LevelSetup.EnsureIceBiome` is ice-only: make the new `LevelBiome` by hand or generalize it).
   Background: open a level of that biome and run **EditorNiveles ▸ Importar fondo…** with the exported `*.parallax.json`
   (makes `Fondo_<name>` and offers to set it on the biome + open level); particles (copy `Nieve.prefab` + `SnowSettings`; leaves/ash: change texture,
   speed, collision), test scenes 1000/2000/3000 px.
7. Compile-check (`unity command recompile`), capture with `capture_game_view` in edit mode to self-check (delete the
   PNG after), commit, then give the user a short test list (they play; no Play Mode from here).
8. Write down anything new that worked/failed in `docs/biomas-receta.md` in the same turn.
