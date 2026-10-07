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

Behind them, **`HudScreenFx`**: a code-generated red vignette (centre always clear) — a flash on
`Health.Damaged` scaled by hit size, and a soft double-pulse "heartbeat" under the low-health
threshold, both faded in/out, each with on/off + intensity in `HudSettings`.

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
instance changes) and collects them with `GetComponents`. **New ability with a cooldown (parry)**:
implement `ICooldownSource` on a player component with a new `CooldownSlot` value; the slot appears
by itself, and on touch add one `AttachDial(button, slot)` line in `TouchControlsController`.

The cooldown animation is **`CooldownDial`** (Painter2D: shrinking clockwise pie, accent progress
ring with bright tip, outer charge ring, expanding "ready" ring + flash — skipped for cooldowns
< 0.7 s so the basic attack doesn't strobe). Both the HUD slots and the touch buttons host the same
dial, so they look identical; on touch the HUD hides its slot row. Sizes/colours live in `HudStyle`.
**Art** (`HudSkin`, `Resources/HudSkin.asset`, built by **Tools ▸ RedMagic ▸ UI ▸ HUD · Procesar kit de arte** =
`HudUiPack` from `Assets/Ui/HudSprites/`, prompts in `docs/hud-art-prompts.md`): bar frames are **hollow** rims
(interior keyed out) drawn **in front** of the code-drawn fill with `UiFrame.Overlay` — `HudStyle.SkinBar` drops the
vector border, insets the track to 6/10 of the rim and rounds it like the hole; low HP tints the rim
(`UiFrame.TintLayer`). Ability slots overlay a ring `Image` (idle / ready sprite, `abilityRingOverflow` = halo room,
`abilityRingInset` = sweep drawn inside); icons = source icon (weapon) → skin icon → vector glyph. Currency plaques
and legend keycaps use the normal `UiFrame.Dress` (keycap in its own box: frame children cover a Label's text).
Every piece is optional: missing art falls back to the vector look of `HudStyle`.

Runtime UI Toolkit panels don't appear in `screenshot` or `capture_game_view --source camera`.
What does work is **`capture_game_view --source screen`, Play Mode only**. Rendering a cloned
`PanelSettings` into a `RenderTexture` does *not* work in Edit Mode — an offscreen panel never
draws without a runtime and the texture comes back blank.
