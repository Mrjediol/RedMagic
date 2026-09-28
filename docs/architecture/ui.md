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

Runtime UI Toolkit panels don't appear in `screenshot` or `capture_game_view --source camera`.
What does work is **`capture_game_view --source screen`, Play Mode only**. Rendering a cloned
`PanelSettings` into a `RenderTexture` does *not* work in Edit Mode — an offscreen panel never
draws without a runtime and the texture comes back blank.
