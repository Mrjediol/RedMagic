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
- [ ] F8 · (bug you reported, fixed) Enemy Creator ▸ Configurar inline ▸ BlueBullet, import with
      **Conservarlo** over an existing enemy: the shot uses BlueBullet (the own attack's projectile is
      now written on every import, like the deck). Check also that changing it back and reimporting
      follows the change.
- [ ] F9 · Enemy Creator layout: top = Enemigo (name + art), Tipo / proyectil / Ataques; everything
      else folded under "Más parámetros" (Vida y tier, Rangos, Movimiento…), Presencia and the
      sprite-sheet projectile art at the bottom. Does it read better? What else should be at hand
      or folded?
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

## Sounds and inspectors (after your 2026-10-01 note)

**What was added**
- `RigGesture` has its own sound (+ Start / Release moment); the Sounds tab lists it per gesture:
  `Enemies/<name>` ▸ "Gesture · …" (and for rig bosses). Enemy-deck attacks list under
  `Enemies/<name>` ▸ "Attack · …". Reimporting a rig keeps the gesture sounds.
- Enemy and boss inspectors: one foldout per header, the useful ones open.

**Tests**
- [ ] S1 · Sounds tab ▸ Regenerar registro: `Enemies/koros` (or your rig enemy) now shows its gestures and
      its deck attacks, besides the basic four. Assign a clip to a gesture and to an attack.
- [ ] S2 · Play: the gesture sound plays at the chosen moment (Release by default; the field is on the gesture
      in the `ProceduralRig` inspector: Start = at the windup), and the attack's own sound plays too.
- [ ] S3 · Reimport the rig (Conservarlo): the gesture sounds are still assigned.
- [ ] S4 · A sleeping rig enemy lists "Wake"; a non-sleeper does not.
- [ ] S5 · Inspector of an enemy (`EnemyStats`) and of a boss (`BossController`): foldouts by group, ranges /
      attack / projectile open, the rest closed; open state sticks. Tell me which groups you want open by default
      or which fields still feel noisy. (Colliders are Unity's own components, not folded.)

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

---

## Enemy Studio S1 — shell + templates (web only, branch `task/enemy-studio`)

**What was added**
- New web tab **🧬 Enemy Studio**: steps on the left (1 Cuerpo, 2 Plantilla, 3 Efectos, 4 Comportamiento,
  5 Exportar), the Rig Composer canvas in the centre, the inspector on the right. Steps 3–5 unlock when the
  body has a name and art. It uses the same composer and the same Enemy Creator form as the old tabs (both
  still there): what you do in one is in the other.
- Step 4 puts the Enemy Creator form next to the canvas, already linked to the body on screen. It is saved
  when you leave the step, when you save and when you export.
- **Templates**: "Guardar como plantilla" saves the body shape + gestures + behaviour (no art) to the library;
  it appears with a ★ in "Nuevo desde plantilla", with "Con sus gestos" / "Con su comportamiento" checkboxes.
  "Más ▸ Traer sólo sus gestos a este cuerpo" copies a template's gestures onto the enemy on screen.
- Rig Composer tab: the template block is now below the parts list (same content plus the new buttons).
- Fix: saving a rig where two parts use the same PNG no longer hangs.

**Tests** (reload with Ctrl+F5 first)
- [ ] ES1 · Open 🧬 Enemy Studio empty: 5 steps, 3–5 with a lock. Does the layout read well (widths, what is
      at hand, anything noisy)?
- [ ] ES2 · Step 2 ▸ Nuevo: Lanzador flotante, fill the slots in step 1, give it a name: steps unlock, step 1
      shows "n piezas · 3 gestos". Gestures, poses and "▶ Probar" work as in Rig Composer.
- [ ] ES3 · Step 4: the form appears on the right with the template's values (FlyingRanged, vida 30); name /
      art rows are not shown. Change health and add an attack with a gesture; go to step 5 and back to 4: the
      values are still there. Is the form comfortable at that width?
- [ ] ES4 · Step 5 ▸ Exportar .zip, import in Unity (Importar rig): the enemy has the health and the deck you
      set in step 4 without ever opening the Enemy Creator tab.
- [ ] ES5 · Step 2 ▸ Guardar como plantilla (give it a name). It shows as "★ name" in the list and in
      📚 Biblioteca ▸ Plantillas. Nuevo from it: same slots (pivots, boca, roles), same gestures, same
      behaviour in step 4; drop DIFFERENT art in the slots: pivots land in sensible places, "▶ Probar" on a
      gesture still reads right at the new size.
- [ ] ES6 · Same template with "Con sus gestos" / "Con su comportamiento" unchecked: no gestures / default
      behaviour.
- [ ] ES7 · Open another saved rig (different part names, one part marked "apunta"), step 2 ▸ pick your
      template ▸ Más ▸ Traer sólo sus gestos: the gestures appear and the arm poses move the part that aims;
      the status line names the slots it could not match.
- [ ] ES8 · Regression: 🦴 Rig Composer and 🧩 Enemy Creator tabs still work after visiting the Studio (panels
      in place, export from each). Open a rig in Rig Composer, then click 🧬: same document.
- [ ] ES9 · A boss rig in the Studio: step 4 shows the Boss Creator link (no enemy form); export as before.

**Known limits of S1**: step 3 only lists effects (attaching them to gesture phases is S2); a template keeps
no art and no state sprites; a boss template does not keep the feet height.
