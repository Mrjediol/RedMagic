# UI
All menus (`MainMenuController`, `PauseMenuController`, `OptionsMenuController`,
`TouchControlsController`) are UI Toolkit (`UIDocument` + UXML/USS), not uGUI, and implement
`IMenuScreen` (`SetVisible(bool)`) so one menu can hide itself and hand focus to another
(Pause → Options → back) without losing UI Toolkit layout state — visibility toggles via
`style.visibility`/`display`, not by disabling the GameObject.

**UI art kits** — read `Assets/_Pipeline/UI_ART_PIPELINE.md` before dressing a screen with art.
A kit of flat-background images becomes clean sprites through a `UiArtKitRecipe`
(`<Kit>.uikit.asset`) + `UiArtKitProcessor` (background keying, soft halos, state grids,
**Quad** frames: 4 quadrant 9-slices that stretch only a 2px strip, so centre ornaments never
smear, plus the frame's interior panel as one `_Fill` sprite drawn on top — stretching the
interior from the strips made it look split into 4 rectangles), and a per-kit pack wires typed refs into a `<Screen>Skin` asset in `Resources`.
Runtime side is `Assets/Ui/UiFrame.cs` (`UiFrame.Dress(element)`, `UiStateSprites`). Two kits
ship: the items screen (`ItemsUiPack` → `Resources/ItemMenuSkin.asset`, read by
`ItemMenuController`) and the **menu kit** (`MenusUiPack` over `Assets/Ui/UiSprites` →
`Resources/MenuSkin.asset`), which dresses four screens: main, pause, options and the **permanent
upgrades** grid. Any missing piece falls back to the plain `MenuStyle`/USS look, so deleting a skin
only removes the art, never breaks a screen.

The first three are UXML+USS and the upgrades grid is code-built — which changes nothing for
dressing, since `UiFrame.Dress` works on any `VisualElement`. They share
**`Assets/Ui/MenuSkinDresser.cs`** (UXML ones call `DressWithSkin()` from `OnEnable`; the grid
dresses inside its own `BuildUi`/`BuildCell`); it is idempotent via an `rm-skinned` marker class, so
a repeated `OnEnable` doesn't stack layers or re-register callbacks. In the upgrades grid, a cell's
state (locked / affordable / maxed) is shown by **tinting the frame**, not by a background colour —
the background sits behind the stone and wouldn't be visible (`UpgradeMenuController.PaintCell`).
**`UI_ART_PIPELINE.md` §3-ter is the tweak guide**: which knob moves what, the vertical budget, and
what must be mirrored back into the pack so a skin reset doesn't undo it. Traps it exists to avoid,
all of which bit during that pass:
- **A frame layer covers the element's own text.** UI Toolkit draws children above the parent's
  text, and the frame is a child — so titles get **wrapped** in a plaque and a `Button`'s `text` is
  moved into a child `Label`. Font properties inherit, so the USS (`:hover` included) still rules.
- **Padding must exceed the frame's stone thickness**, which is `fill.rect.x * scale` in screen px
  with `scale` from `UiFrame.Fit` — compute it, don't eyeball it; too little and the text sits
  inside the bevel. If the needed padding eats the panel, lower `maxScale` instead.
- **Multi-state pieces of the same drawing share `trimGroup` *and* `interior`** (the three button
  states do), or the hover's glow shrinks its fill and the button jumps on mouse-over.

## Menus that must exist in every scene (`Resources/MenusUI.prefab`)
Touch controls, **pause** and **options** (`TouchControlsUI`, `PauseMenuUI`, `OptionsMenuUI`) used to live inside MainHub and Ice1, so a run (MainHub
unloads) or any new level scene had no pause button, Esc/Start or touch controls. They are now one prefab, spawned before the first scene by
`PersistentMenus` (`DontDestroyOnLoad`; its children are deactivated while `MainMenuController.IsOpen`, the main menu has its own options screen).
**Never put them in a scene again.** The one-off migration is `Tools ▸ RedMagic ▸ UI ▸ Migrar menús a prefab persistente` (`MenusUiMigration`, delete it).

**Performance options** (Options ▸ General): `PerformanceSettings` — FPS limit 30/60/90/120/144/*display* (default: the display's highest refresh
rate; was a hardcoded 60) and *Show FPS* (`FpsOverlay`, now in every build, default on only in the editor / Development Build). Applied at startup by
`PerformanceBootstrap`; the limit needs vSync off or Unity ignores `targetFrameRate`.

**Shaders found by name** (`Shader.Find`: Fx Vertex Color, Parallax Layer/Shadow, Melee Slash Layer, Door Energy, Sprite Lit FX…) must be in
*Project Settings ▸ Graphics ▸ Always Included Shaders* or a build strips them (magenta quads). The custom ones are listed there.

## In-game HUD (`Assets/Ui/Hud/`)
One persistent, self-bootstrapping panel, **`GameHud`** (`Resources/CurrencyHudPanelSettings`,
sort 15: above touch buttons, below Pause (20) and Options (25)), code-built from widget classes that each
animate in a `Tick(dt)` on unscaled time. Four **movable pieces** (`Settings.HudElement`; factory
spot in brackets):
- **Health** (top-left): `HudHealthBar` (fill + delayed "ghost" trail on damage, green lead on heal,
  white flash + shake on hit, pulse under `Health.LowHealthThreshold`, shield bar floating above it —
  pips for ≤12 hits, continuous otherwise — positioned absolutely so a shield never resizes the piece).
- **Cooldowns** (under health): a row of `HudCooldownSlot`s, one per `CooldownSlot` enum value.
- **Currency** (top-right): `HudCurrency` (gain counts up + pop, `GameHud.PlaySpend` for the shop's
  count-down, reroll counter only while `ShopManager.Current`) with `HudBuffRow` (`BuffIndicators`) under it.
- **Controls** (bottom-left): `HudControlsLegend`, keyboard/mouse only, `HudSettings.ShowControls`.

Behind them, **`HudScreenFx`**: code-generated vignettes (centre always clear) — a red flash on
`Health.Damaged` scaled by hit size, a green glow on `Health.Healed` (only real `Heal()`, not revive/reset),
and a soft red double-pulse "heartbeat" under the low-health threshold, all faded in/out, each with on/off +
intensity in `HudSettings`.

**Enemy health** (`HudEnemyBars`, mode in `HudSettings.EnemyHealth`: Off / Overhead / Focus / List,
default List): `HudEnemies` builds the "in combat" set each frame from `Health.Active` (a static set
every `Health` joins in `OnEnable`) — enemy = has `EnemyStats` / legacy `EnemyController` /
`TrainingDummy` and no `BossController` above it (bosses keep `BossHealthBar`); in combat =
`EnemyBrain.InCombat` or hit within 6 s (`Health.LastDamagedTime`); sorted by distance to the
player. **Overhead** = pooled `EnemyBarView`s on a full-screen layer placed via
`Camera.WorldToScreenPoint` → `RuntimePanelUtils.ScreenToPanel` at the collider's top; **Focus** =
one big bar (last hit, else nearest), piece `EnemyFocus`, default top-centre under the boss bar;
**List** = up to 6 rows + "+N", piece `EnemyList`, default right, anchored by its top so new rows
don't move it. Bars fade in/out, show a ghost trail + flash, exact HP optional
(`EnemyHealthNumbers`), `IShieldSource` children (`HitShield`) as pips, and — when
`EnemyCooldownReveal.Granted` (for a future item: `Grant/Revoke(key)`) or the Options test toggle
`EnemyCooldownsDebug` — one strip per `IEnemyCooldownSource` (`EnemyBrain` = attack cooldown;
blinks while attacking). The old per-character uGUI `HealthBarUI` is in Legacy.

**Placement**: each piece is absolutely positioned from a `HudAnchor` (pivot 0/½/1 per axis + screen
fraction), saved per piece in `PlayerPrefs` by `HudSettings`; unset = `GameHud.DefaultAnchor`. On drop
the pivot is picked by the screen third the piece lands in, so a right-docked piece grows inward.
**`HudLayoutEditor`** (Options ▸ Interfaz ▸ Colocar elementos → `GameHud.BeginLayoutEdit`) dims the
screen, draws a 48×24 grid (gold centre lines) and a draggable handle per piece; dragging snaps the
nearest of start/centre/end edge to a grid line (cyan guide), drop saves. Options hides itself while
editing; Escape / Done (`GameHud.EndLayoutEdit`) returns to it. While editing every piece is forced
visible (preview health bar in the main menu).

**Options** has two tabs, General (audio, display, language) and Interfaz (the `HudSettings` above,
place / reset positions); `ShowTab` toggles the pages, `.option-pages` min-height keeps the panel from jumping.

Data comes from the player through **interfaces, not wiring** (`Gameplay/CooldownSource.cs`):
`ICooldownSource` (slot, `CooldownActive`, `CooldownState` {remaining, duration, blocked, charge},
icon) — implemented by `WeaponUser`, `PlayerAttack` (both `Attack`; the active one wins) and
`PlayerMovement` (`Dash`, blocked when air dashes are spent) — and `IShieldSource`
(`LegendaryPassivePlayerHook`). `HudPlayer` finds the player by tag (re-searched when the hub/run
instance changes) and collects them with `GetComponents`. `PlayerParry` fills `Parry` (touch: `parryButton`). **New ability with a cooldown**:
implement `ICooldownSource` on a player component with a new `CooldownSlot` value; the slot appears
by itself, and on touch add one `AttachDial(button, slot)` line in `TouchControlsController`.

The cooldown animation is **`CooldownDial`** (Painter2D: shrinking clockwise pie, accent progress
ring with bright tip, outer charge ring, expanding "ready" ring + flash — skipped for cooldowns
< 0.7 s so the basic attack doesn't strobe). Both the HUD slots and the touch buttons host the same
dial, so they look identical; on touch the HUD hides its slot row. Sizes/colours live in `HudStyle`.
**Art** (`HudSkin`, `Resources/HudSkin.asset`, built by **Tools ▸ RedMagic ▸ UI ▸ HUD · Procesar kit de arte** =
`HudUiPack` from `Assets/Ui/HudSprites/`, prompts in `docs/hud-art-prompts.md`): bar frames are **hollow** rims
(interior keyed out) drawn **in front** of the code-drawn fill with `UiFrame.Overlay`. The pack also emits each
rim's **mask** (outer silhouette, eroded 4 px, same Quad cuts → `UiFrame.maskQuads`): every fill layer is a
`HudStyle.BarLayer` = a `UiFrame.Mask` sized to the whole bar inside a width-% `overflow:hidden` clip, so the fill has
the art's exact shape and never peeks past rounded ends (`PrepareSkinnedBar`; without a mask it falls back to an
inset rounded track). Low HP tints the rim (`UiFrame.TintLayer`). Sources are the Gemini JPGs upscaled + cut in ComfyUI (`HudSprites/HD/`, ~2048 px with alpha); the pack
**downsamples each piece itself** (`UiArtPiece.outputHeight`, area average) to ~2× its on-screen height, because the
UI Toolkit shader samples inside a branch and can't be trusted to use mipmaps — letting the GPU shrink 2048 → 70 px
is what pixelated the edges. `HudSkin` scales are UI px per **output** sprite px. Ability slots overlay a ring `Image` (idle / ready sprite, `abilityRingOverflow` = halo room,
`abilityRingInset` = sweep drawn inside); icons = source icon (weapon) → skin icon → vector glyph. Currency plaques
and legend keycaps use the normal `UiFrame.Dress` (keycap in its own box: frame children cover a Label's text).
Every piece is optional: missing art falls back to the vector look of `HudStyle`.

Runtime UI Toolkit panels don't appear in `screenshot` or `capture_game_view --source camera`.
What does work is **`capture_game_view --source screen`, Play Mode only**. Rendering a cloned
`PanelSettings` into a `RenderTexture` does *not* work in Edit Mode — an offscreen panel never
draws without a runtime and the texture comes back blank.
