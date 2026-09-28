# Input
Three input sources are meant to coexist, not be exclusive: the Input System asset
(`RedMagicControls.inputactions`, keyboard/gamepad), and `Assets/Scripts/Input/TouchInput.cs`, a
static class that on-screen touch buttons (`TouchControlsController`) push queued presses into.
Gameplay scripts (`PlayerMovement`, `PlayerAttack`, `RangedAttack`) check both every frame. All
input is gated behind `GameStateManager.CanPlayerAct` — check that first in any new input-driven
script rather than re-deriving a pause check.

**`InputDeviceManager` has three modes — `Touch`, `Gamepad`, `KeyboardMouse` — and only a real
`Touchscreen` sets `Touch`.** In the Input System `Mouse` derives from `Pointer` exactly like
`Touchscreen`, so the old code (which matched `Pointer`) dropped a PC into touch mode on the first
click and the mobile on-screen buttons appeared. `OnEvent` now checks `Touchscreen` **before**
`Mouse`/`Keyboard`; keep that order. `DetectInitialMode()` decides what you start in before
touching anything: gamepad if one is connected, `Touch` on a mobile platform (or a touchscreen with
no keyboard), otherwise `KeyboardMouse`. Use the static helpers rather than comparing the enum:
`TouchActive` (falls back to `Application.isMobilePlatform` when the manager doesn't exist yet, so
PC never flashes the touch buttons for a frame), `KeyboardMouseActive`, `GamepadActive`.

Each mode shows its own on-screen help and nothing else:
- **Touch** → the on-screen buttons (`TouchControls.uxml`, container class `touch-only`, shown/
  hidden by `TouchOnlyUI`) plus the on-screen pause button (`PauseMenuController._touchMode`). On PC
  both hide; Esc still opens pause, so nothing is lost.
- **KeyboardMouse** → **`Assets/Ui/ControlsLegendHud.cs`**, a self-bootstrapping translucent key
  list in the bottom-left (WASD / Espacio / Shift / Clic izq. / E / I / Esc). It shares
  `InteractionPromptPanelSettings` and hides itself whenever `CanPlayerAct` is false, which also
  keeps it off the main menu and out from under any open menu. **Its rows are hand-written** — if a
  binding changes in `RedMagicControls.inputactions` (or in a script that reads keys directly, like
  E to interact or I for items), update `ControlsLegendHud.Rows` too.
- **Gamepad** → neither, since the prompts would be wrong buttons.
