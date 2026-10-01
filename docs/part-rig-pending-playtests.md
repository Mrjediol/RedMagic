# Part-rig plan — pending playtests (D → F)

The user could not test after Phase D (2026-10-01) and asked to keep going. Every phase built
since then is **untested in Play / in a real browser**. This file is the single list to run at the
end; feedback from it drives the readjustments. All of it lives on branch `task/enemy-attack-deck`
(merge only after this list passes).

How to report: per item, ✓ or what you saw (which enemy/attack, what happened, what you expected).

---

## Phase D — several attacks per enemy

**What was added**
- `IAttackHost` / `IAttackBody`: boss attacks no longer talk to `BossController` directly, so the
  same attack assets work on normal enemies (`EnemyAttackHost`). Every boss attack was touched.
- The deck: `EnemyStats ▸ Ataque · baraja` (`attacks`, `builtInAttack`, `builtInWeight`). Empty =
  the enemy attacks exactly as before.
- Per-attack gesture on enemies (rig gesture, or Animator state on sprite-sheet enemies).
- Web ▸ Enemy Creator ▸ **Ataques** section; picking an art entry loads its saved behaviour.
- Import: one asset per attack in `Assets/ScriptableObjects/EnemyAttacks/<Enemy>/`; the deck is
  rewritten on every import.

**Tests**
- [ ] D1 · Regression, enemies: 2–3 existing sprite-sheet enemies + `Robot` + `Turret` attack,
      cool down, take hits and die exactly as before.
- [ ] D2 · Regression, bosses: BossIce2 and one sprite boss — every attack, its warnings, the
      charge (moves and faces correctly), the thrown rock, hazards disappear when the boss dies,
      phase change.
- [ ] D3 · Web: Enemy Creator, pick the 🦴 caster rig as art → its behaviour loads into the form
      (archetype, health, ranges). Section *Ataques* shows the built-in card and "＋ Añadir ataque";
      boss-only attacks (Guardia, Invocar esbirros, Ritual de anclas, Negar plataformas) are not offered.
- [ ] D4 · Web: add 3 attacks (Lluvia/abanico → gesture `Sweep`; Corona de orbes → `Charge`;
      Onda de choque → `Flick`) with different min/max distances; the summary says "✓ Válido";
      export the .zip.
- [ ] D5 · Unity: *Tools ▸ Web ▸ Rig ▸ Importar rig* with **Conservarlo**: 3 assets in
      `Assets/ScriptableObjects/EnemyAttacks/<name>/`, the deck listed in `EnemyStats`,
      `EnemyAttackHost` on the prefab, no errors in the summary.
- [ ] D6 · Play: the enemy alternates its own shot and the spells; each spell plays its arm gesture
      and its warning before hitting; distances respected; `attackCooldown` between attacks.
- [ ] D7 · Play: kill it mid-attack → nothing stays on screen (warnings, hazards, projectiles end).
- [ ] D8 · Play: uncheck "Entra en el sorteo" on the built-in attack, re-export, reimport → it only
      casts spells. Remove every attack in the web, re-export, reimport → back to the built-in only.
- [ ] D9 · A sprite-sheet enemy with one catalog attack whose gesture is one of its animations
      (combined bundle import): the animation plays as the telegraph, the attack fires, it returns to Idle.
- [ ] D10 · "Sólo si está por encima": a ground enemy with a card set to TargetAbove only casts it
      when you stand on a ledge above it.

- [ ] D11 · Enemy Creator ▸ Ataques ▸ Ataque propio ▸ "＋ Repetir el ataque propio con otro gesto":
      pick a gesture (e.g. a pose made in Gestos), export, import: the enemy fires its normal shot
      (same projectile) but telegraphs it with that gesture, and the shot leaves from the Muzzle at
      the end of the windup. With peso 1 vs 1 it alternates both gestures.

**Known limits of D (not bugs)**: enemy warnings use code placeholders unless prefabs are set on
`EnemyAttackHost`; a sprite-sheet gesture plays at the clip's own speed; a `Static` non-sleeper
needs the target within `verticalTolerance` before any card is considered.

---

## Phase E — gesture pose editor (web only)

**What was added**
- Rig Composer ▸ inspector ▸ **Gestos**: create / rename / duplicate / delete gestures and edit
  their timings.
- **Posar carga / Posar golpe**: the canvas becomes a pose editor (Mover, Rotar, Escalar act on the
  pose, not on the part; drag on empty space poses the whole body; the rest pose shows as a ghost).
- **▶ Probar**: plays the gesture with the same timing Unity uses.

**Tests**
- [ ] E1 · Open a saved caster rig: *Gestos (3)* lists Flick / Charge / Sweep; **▶ Probar** on each
      plays a visible, different arm motion and returns to rest.
- [ ] E2 · *＋ Nuevo gesto*, rename it, **Posar carga**: drag the casting arm (Mover and Rotar) —
      only the pose changes, the ghost shows where the arm rests; drag on empty space moves the
      whole body. **Posar golpe**: pose the opposite motion. **▶ Probar** reads as wind-up → whip.
- [ ] E3 · While posing: Pivote / Boca tools refuse to act; Supr removes the selected part from the
      pose (it does NOT delete the part); Esc or *■ Salir de posar* leaves; after leaving, Mover
      moves the part itself again. Ctrl+Z undoes a pose drag.
- [ ] E4 · Does dragging feel right (direction, amount) on a part hanging from a rotated/scaled
      parent, and with the body facing its drawn side? Report any part that moves the wrong way.
- [ ] E5 · Save the rig → Enemy Creator ▸ Ataques: the new gesture is in the gesture dropdown.
      Assign it to an attack, export, import, Play: the enemy plays YOUR pose as that attack's
      telegraph, and it matches what "▶ Probar" showed (timing = the attack's `telegraph`).
- [ ] E6 · A boss rig: edit one of its gestures (e.g. `Throw`), reimport, the boss attack that
      uses it shows the new pose.

---

## Phase F — VFX preset library (web only)

**What was added**
- New web tab **✨ VFX Lab**: five parametric effects (Orbe de hechizo, Disparo con estela,
  Impacto mágico, Orbe cargándose, Aura de aviso) with live preview.
- Saving one creates a normal library entry (Proyectil or VFX, a baked frame sheet) that every
  art picker lists; it stays editable from the Lab (same entry) or as a variant.
- Nothing new in Unity: it travels inside the enemy/boss .zip through the existing importers.

**Tests**
- [ ] F0 · (fixed after your report) the VFX Lab preview no longer covers the tab bar / step 2: reload the page (Ctrl+F5) and check the layout.
- [ ] F1 · Web: the tab opens, each of the five effects previews animated (loops are seamless,
      one-shots play and pause), every slider/colour changes the preview at once. Do they LOOK
      right for the game? Note which need a different shape, more/less glow, other defaults.
- [ ] F2 · Save an orb ("Guardar en biblioteca"): it appears in the tab's list and in 📚 Biblioteca
      as a Proyectil. Change its colour, "Guardar cambios": still one entry. "Guardar como nuevo":
      a second one.
- [ ] F3 · Enemy Creator ▸ Ataques: in an attack with a projectile slot (Lluvia/abanico), the saved
      orb is in the art dropdown. Export, import (rig or sprite enemy), Play: the spell throws
      that orb, animated, and it bursts with its Impact on hit. Size in game follows the attack's
      projectile `size`.
- [ ] F4 · The same orb as an enemy's own projectile (Enemy Creator ▸ Ataque · a distancia ▸
      Configurar inline ▸ Arte desde biblioteca): flies and bursts.
- [ ] F5 · An fx preset (Impacto mágico or Aura de aviso) in an attack's effect / warning slot, on
      a boss (Boss Creator) or an enemy: it shows at the right place and size.
- [ ] F6 · Retune a saved preset (e.g. another colour), re-export the enemy that uses it, reimport:
      the enemy shows the new look without touching anything else.
- [ ] F7 · Pixelado 3–4: does the pixel-art look match the rest of the game's art?

**Known limits of F**: colours are baked (not tinted at runtime); re-saving a preset entry from the
Sprites tab turns it into a plain sheet (no longer reopenable in the Lab); no trail / muzzle-flash
presets yet.

---

## After the pass

Bring back the list with ✓ / notes. Expected kinds of readjustment: numbers (ranges, telegraphs,
gesture poses, effect defaults), then anything that does not behave as described above.

---

## Rig driver — decision after the DOTween comparison

The user compared the DOTween copies in Play (2026-10-01): no clear difference, and the extra bounce
on every action was not liked. Decision: **one driver, `RigDriverCode`**; the DOTween driver is in
`Assets/Scripts/Legacy/`, its copies and pack are deleted.

**Tests**
- [ ] T1 · Regression: `Robot`, `Turret` and `BossIce2` animate exactly as before the experiment
      (idle sway, attack, hurt, wake/sleep, death, boss gestures). `RigDriverCode` was only opened
      for inheritance, nothing in its behaviour should have changed.
- [ ] T2 · No missing-script or missing-prefab warnings in scenes where a `…Tween` copy was placed
      for the comparison (remove those instances; the prefabs no longer exist).
