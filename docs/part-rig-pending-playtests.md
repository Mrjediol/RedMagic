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

---

## Enemy Studio S2 — effects on gesture phases (web + Unity)

**What was added**
- A gesture card now has three tabs — **Carga / Golpe / Vuelta** — each with its pose button, its timing and
  its **effects**: library effects (✨ VFX Lab or Sprites) hung from a part, the muzzle, the body or the feet.
- Per effect: when (start / end / a point of the phase), "En bucle hasta que acabe la fase" (a charge) or one
  burst, "⌖ Colocar en el lienzo" (click where it goes), and folded: size, offset, "sigue a su ancla".
- Enemy Studio ▸ step 3 **Efectos** is a palette: ＋ (or drag the row onto a gesture) adds it to the phase
  that gesture is showing.
- "▶ Probar" shows the effects at their moment, on the canvas.
- Unity: the effects travel in the rig .zip, become pooled prefabs (`Assets/Prefabs/Fx/Enemies/<name>/`) and
  the driver spawns them during the gesture. `ProceduralRig` inspector ▸ gesture ▸ "Efectos" lists them.
- Templates keep the effects of their gestures.

**Tests** (Ctrl+F5 first; needs at least one effect in the library, e.g. "Orbe cargándose" and "Impacto mágico"
from ✨ VFX Lab)
- [ ] ES10 · Open a caster rig in 🧬, open gesture `Charge`: tabs Carga / Golpe / Vuelta. Carga has "Posar
      carga" + Temblor, Golpe has "Posar golpe" + Golpe (s), Vuelta has only its time. Posing still works.
- [ ] ES11 · Step 3: with `Charge` open on Carga, press ＋ on the charging orb: it appears in the card and on
      the canvas. "Cuelga de" ▸ Boca del arma, or "⌖ Colocar" and click the weapon tip. Then tab Golpe ▸
      ＋ Efecto ▸ the impact, on the muzzle. Dragging a row from step 3 onto a gesture also works.
- [ ] ES12 · "▶ Probar": the orb loops on the weapon tip during the charge and follows the arm, disappears at
      release, the burst plays once. Sizes and positions read right? (say which defaults to change)
- [ ] ES13 · Export, import in Unity (Conservarlo): no errors; prefabs in `Assets/Prefabs/Fx/Enemies/<name>/`;
      the gesture in the `ProceduralRig` inspector lists 2 effects.
- [ ] ES14 · Play, with an attack of the deck using that gesture: charge effect on the weapon tip for the whole
      telegraph (following the arm, mirrored when the enemy faces left), gone at release; burst at the muzzle.
      Same size and place as in the web preview? In front of the body?
- [ ] ES15 · Kill the enemy mid-charge, and let it be interrupted (another attack / it falls asleep): no effect
      stays on screen. Several enemies charging at once: each has its own.
- [ ] ES16 · An effect on "Pies" with "sigue a su ancla" off in Vuelta (dust on landing): stays on the ground
      where it appeared.
- [ ] ES17 · Remove an effect in the web, re-export, reimport: it is gone in Unity. Change its size: follows.
- [ ] ES18 · Regression: enemies and bosses without effects (Robot, Turret, BossIce2) animate as before.
- [ ] ES19 · Save as template with effects, new enemy from it with other art: effects are on the same parts,
      scaled with the body.

**Known limits of S2**: effects always draw in front of the body; colours are baked; the enemy's built-in attack
has no effects unless it is repeated with a gesture; a boss's held gestures (move / dash travel) only fire the
effects set "al empezar" of Carga; an effect has no sound of its own.

### Verified by Claude on 2026-10-01 (real Chrome + Unity Play Mode, with your permission)

Run end to end with the Robot's part PNGs on the "Lanzador flotante" template and two VFX Lab effects:
- **Web (ES1–ES3, ES5, ES8, ES10–ES12, ES19)**: the Studio lays out and works in Chrome; steps lock / unlock; step 4
  form linked and its values kept (health 55 survived leaving the step, a reload, and travelled into the zip);
  effects added from step 3 and from the card, shown on the canvas and in "▶ Probar" following the arm;
  "Guardar como plantilla" → "★" entry → new enemy from it with gestures, effects and behaviour; the 🦴 and 🧩
  tabs intact afterwards. No console errors.
- **Unity import (ES4, ES13)**: the browser's .zip imports with no errors; health and the two effects are on
  the prefab; effect prefabs built (loop without `VfxOneShot`, burst with it).
- **Play (ES14, ES15)**, stepping frame by frame: the charge effect sits exactly on the muzzle every frame,
  rotates with the arm, animates, and is mirrored when facing left; at release it is returned to the pool and
  the burst appears, follows for its duration and returns itself; disabling the enemy or killing it mid-charge
  despawns the effect at once; re-enabling reuses the pooled instance. Nothing is parented to the enemy.

Still yours to judge (feel, not function): sizes / defaults of the effects against a body, whether a strike burst
should follow the muzzle by default (it follows the whip of the arm; untick "Sigue a su ancla" to leave it where
the shot left), layout comfort, and ES6, ES7, ES9, ES16–ES18 (not run). Two things seen and left as they are:
filling a template's optional slots one by one after the required ones leaves the collider fitted to the first
ones ("Ajustar a las piezas" fixes it); the canvas is not re-framed when the toolbar wraps.
Fixed from this run: a new effect now defaults to the weapon tip (not the selected part); effect prefabs are
named after the effect (`Fx_<enemy>_gfx_<name>_<id>`); the gesture name no longer gets squeezed in its card.

---

## Enemy Studio S3 — sprites per state and per gesture (web + Unity)

**What was added**
- Each part can have a sprite for more states: **Detecta al objetivo**, **Herido**, **Muerto**, besides
  Dormido / Despierto / Atacando — and one **per gesture** ("in the attack that uses gesture X, this arm
  looks like this").
- 🧬 step 4 ▸ **Sprites por estado**: choose the state (the canvas shows the body in it), then give each part
  its sprite (button or drop the PNG on the row). The per-part section in the inspector has the same slots.
- The toolbar's sprite selector previews every state; "▶ Probar" shows a gesture's sprites, "💥 Golpe" the hurt ones.
- Unity: one rule decides the sprite (dead → hurt → gesture → attacking → alert → asleep/awake → stage → base).
  A state you leave empty falls through to the next one.

**Tests**
- [ ] ES20 · Board: pick "detecta al objetivo", give the head another sprite: the canvas shows it; the thumbnail
      gets a frame; ✕ removes it. Pick "gesto: Charge", give the arm a sprite; "▶ Probar" on Charge shows it only
      during that gesture.
- [ ] ES21 · Export / import / Play: the head changes when the enemy notices you and goes back when it gives
      up; the hurt sprite shows while it flinches; the dead sprite at death; the arm sprite only in the attack
      whose gesture is Charge, and it returns after the recover.
- [ ] ES22 · Regression (rule changed): Turret eyes asleep / awake / attacking as before; BossIce2 torso changes
      with the phase and keeps it after a hit and after an attack. Report any sprite that does not come back.
- [ ] ES23 · Rename a gesture that has sprites: they follow. Delete it: the export note says nothing is lost silently.

**Known limits of S3**: bosses have no "alert" (their brain does not report it); a state sprite is per part,
there is no "hide this part in this state".

---

## Enemy Studio S4 — idle gestures (web + Unity)

**What was added**
- A gesture can be marked "💤 Gesto de reposo": the enemy does it on its own now and then while it has nothing
  to do (no target, standing still, awake). Under the gestures list: every how many seconds, the chance, and
  how long the wind-up takes; "▶ Probar uno" plays one.
- It is only flavour: it never fires or warns of an attack. A real attack or moving cuts it with a blend.

**Tests**
- [ ] ES24 · Web: make a small gesture (a head tilt, a weapon check), tick 💤, "▶ Probar uno".
- [ ] ES25 · Play: leave the enemy alone: it does the gesture every few seconds, not always. Walk into its range
      mid-gesture: it blends into its chase / attack with no pop and no stray shot. While it chases or attacks it
      never does it. A sleeper does not do it asleep.
- [ ] ES26 · Tune "Cada (s)" and "Probabilidad" to taste and tell me the defaults you want (now 4 s, 0.6).
- [ ] ES27 · A boss with an idle gesture: it plays between attacks while standing still — wanted or not?

---

## Enemy Studio S5 — defensive moves (web + Unity)

**What was added**
- Behaviour form ▸ **Defensa**: "＋ Añadir movimiento defensivo". Each one: *when* (you get closer than X / it is
  hit / it is low on health), *how* (shoots backwards, hops back, sidesteps), and the gesture it holds while it
  moves; folded: chance, cooldown, distance, duration, hop height, tell before moving.
- Unity: `EnemyStats` ▸ group "Defensa"; the enemy enters a new state while it gets away, then goes back to
  chasing / attacking. Sound slot "Evade" in the Sounds tab. Works on sprite-sheet enemies too.
- Already verified in Play by Claude (frame stepping): the dash travels its distance and decelerates, the held
  gesture and the alert sprite show, the enemy resumes afterwards; idle gestures do not disturb the brain.

**Tests** (feel)
- [ ] ES28 · A ranged flyer with "se acerca a menos de 3 → sale disparado": get close: short tell, dash away, then
      it fights again. Does it read as a dodge? Tune distance / duration / tell and tell me better defaults.
- [ ] ES29 · "Al recibir un golpe" with chance 0.5 + "Salto hacia atrás" on a ground enemy: about half the hits it
      hops back in an arc and lands; it never hops off a ledge; a hit mid-attack does not cancel the attack.
- [ ] ES30 · "Poca vida": happens once per life.
- [ ] ES31 · With a gesture that has a looping effect "al empezar" in Carga (a trail): the trail shows during
      the dash and stops at the end.
- [ ] ES32 · Assign the "Evade" sound; it plays at the start of the move.
- [ ] ES33 · Remove every defence in the web, re-export, reimport (Conservarlo): it no longer dodges.

**Known limits of S5**: a walking enemy cannot sidestep (2D side view): "De lado" dashes away; bosses have no
defensive moves (their movement is the Boss Creator's); it never interrupts its own attack to dodge.

---

## Enemy Studio S6 — polish (web)

**What changed**
- The **🦴 Rig Composer tab is gone**: part-based enemies and bosses are made in 🧬 Enemy Studio. In 📚 Biblioteca
  their card says "Editar en Enemy Studio"; the filter is "Por partes".
- **🧩 Enemy Creator stays, for sprite-sheet enemies**: its art picker no longer lists part-based enemies.
- Step 2 is a **gallery**: your saved templates (★, with thumbnail) and the three built-in shapes as cards.
- One export for a part-based enemy: step 5 (.zip with body, behaviour, effects, state sprites).

**Tests**
- [ ] ES34 · Open a part-based enemy saved before today from Biblioteca ▸ "Editar en Enemy Studio": parts,
      gestures and behaviour are there.
- [ ] ES35 · A sprite-sheet enemy in 🧩 Enemy Creator works as always (pick its sheet, edit, export combined);
      after using the Studio's step 4 and coming back to 🧩, the picker is empty (not on the part-based one).
- [ ] ES36 · Gallery: cards readable? Enough info on each? Want the built-in ones to show a schematic?
- [ ] ES37 · Decide: keep 🧩 for sprite-sheet enemies as it is, or should the Studio absorb them too?

---

## After your first pass with the ice robot (2026-10-01) — fixes

**What you reported → what changed**
- *The finger sprite did not show in game (it did in "▶ Probar")* — bug in the Unity import: the PNGs of the new
  states (alerta / herido / muerto / por gesto) were never copied out of the .zip. Fixed. **Re-export the ice robot
  and import it again** to get them.
- *The finger shows during the whole gesture* — each gesture sprite now has a window: gesture card ▸ **Sprites de
  este gesto** ▸ "se ve desde … hasta …" (¾ de la carga → mitad de la vuelta, for your case). "▶ Probar" shows it.
- *Effects look pixelated next to 2048 px parts* — ✨ VFX Lab has **Nitidez (× resolución)**, default 3: more pixels,
  same size in game. Open each saved effect in the Lab and "Guardar cambios" to rebake it, then re-export the enemy.
- *White square at the muzzle* — it is the placeholder muzzle flash. A gesture that has its own effects no longer
  shows it. For the plain built-in attack: Tiempos del rig ▸ Fogonazo ▸ tamaño 0.
- *Removing the flame's motion from the template was tedious* — part inspector ▸ Balanceo y comportamiento ▸
  **⏹ Dejar quieta**: one click zeroes sway, pulse, lean follow and removes it from the gesture poses.
- *Unity window* — renamed **Biblioteca Jere** (Tools ▸ Web ▸ Biblioteca Jere). Tab **Enemies** = part-based
  enemies (everything in Enemies/Procedural with an EnemyStats), tab **EnemiesSprite** = the sprite-sheet ones.

**Tests**
- [ ] ES38 · Re-export + reimport the ice robot: the finger sprite shows in the defence, only in its window.
- [ ] ES39 · Rebake one effect with Nitidez 3–4: sharp enough? Same size as before?
- [ ] ES40 · The white square is gone on gestures with effects.
- [ ] ES41 · "Dejar quieta" on the template's lower part: is anything of it still moving?
- [ ] ES42 · Biblioteca Jere: IceRobot, Robot, Turret… under Enemies; "Eliminar enemigo" there also removes its
      imported art, effect prefabs and attack assets (try it on a throwaway one).

---

## ✨ VFX Lab — effects library (2026-10-02)

40 new presets on top of the 5 old ones (unchanged). ✨ VFX Lab ▸ pick from the gallery; "Fondo" switches the scene
behind the preview; "Ver todos los frames" shows every frame on the three backgrounds. Checked by me: every preset
bakes at Nitidez 1 and 3, loops close, nothing clips, lanes are the ones the pickers expect; looked at every frame
on dark / ice / forest. **Not checked: any of them inside Unity** (the pipeline did not change; test VFX5 below).

| Family | New effects |
|---|---|
| Proyectiles | Bola de fuego, Esquirla de hielo, Glóbulo de veneno, Chispa eléctrica, Misil arcano, Orbe sombrío, Roca, Aguja / lanza de luz, Cuchilla giratoria, Orbe pesado, Fuego fatuo |
| Impactos | Chispazo de golpe, Explosión, Estallido de hielo, Salpicadura, Rayo caído, Polvareda de caída, Cura, Escudo roto, Tajo |
| Cargas / auras / estados | Carga en espiral, Aura de mejora, Burbuja de escudo, Ardiendo, Congelado, Envenenado, Aturdido, Marca |
| Estelas | Estela de dash, Polvo de pisada |
| Suelo y zonas | Zona de aviso (rellena), Onda de choque, Círculo de invocación, Suelo en llamas, Charco de veneno, Púas de hielo, Rayo continuo |
| Disparo y aparición | Fogonazo de disparo, Nube de aparición, Teletransporte |

**Tests**
- [ ] VFX1 · Gallery: go through every card. Per family, list the ones that **look wrong** (and what: shape, colour,
      too busy, too weak) and the ones you would **cut**.
- [ ] VFX2 · What is **missing** for the enemies / bosses / items you have in mind? (name + one line each)
- [ ] VFX3 · "Fondo ▸ Hielo": which effects get lost on the ice scene? (my weakest there: Fuego fatuo, Carga en
      espiral, Círculo de invocación, Escudo roto)
- [ ] VFX4 · Weakest of the set in my own judgement — keep, redo or cut: Polvo de pisada (a plain puff), Envenenado
      (thin bubbles), Estela de dash, Tajo (thin arc), Cuchilla giratoria (reads more like a fan than a blade).
- [ ] VFX5 · In 🧬 Enemy Studio hang **Carga en espiral** (bucle) on a gesture's *Carga* and **Chispazo de golpe** on
      its *Golpe*, "▶ Probar", export, import in Unity: do both show, at the right size, loop / one-shot correct?
- [ ] VFX6 · Ground effects (Ardiendo, Congelado, Rayo caído, Púas de hielo, Suelo en llamas) have the floor in the
      lower part of the frame: comfortable to place with ⌖, or would you rather have the floor at the centre?
- [ ] VFX7 · Use one as an enemy projectile (Esquirla de hielo or Bola de fuego) and one in a boss art slot (Zona de
      aviso as a warning, Rayo continuo on a sweep): size and orientation right in game?
- [ ] VFX8 · Defaults: frame 96 px, Nitidez 3 — sharp enough next to the 2048 px parts? Any effect whose default
      colours you would change?
