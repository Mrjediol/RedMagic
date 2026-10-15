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

**Tap buttons are polled, not UI events** (2026-10-15): jump / attack / dash / weapon / parry (both schemes) are read
by `Assets/Ui/TouchTapButtons.cs` from `EnhancedTouch.Touch.activeTouches` every frame and hit-tested against the
buttons' `worldBound` (+ `buttonHitPadding`). UI Toolkit's `Button` clickable captured the pointer and delayed/dropped
presses. A touch that begins inside a button queues the action that same frame (`TouchControlsController` runs at
execution order −50, before the player). Pressed look = USS class `touch-button--pressed`. Hold buttons (left/right/
down/interact) still use pointer events.

**Gesture touch scheme (2026-10-15, experimental)**: Options ▸ Interfaz ▸ *Controles táctiles por gestos*
(`HudSettings.GestureControls`) swaps `movePad`/`actionPad` for `gestureLayout` in `TouchControls.uxml`. Logic lives in
`Assets/Test/TouchGestures/TouchGestureInput.cs`, a serialized field (`gestures`) on `TouchControlsController` with all tuning
in the Inspector. A full-screen `gestureSurface` sits UNDER the gesture buttons (attack, dash, jump): a finger in the
bottom-left move zone is a floating stick (`TouchInput.Horizontal` = ±1 past the deadzone, drag down = `Down`); lift-and-
retap near the same spot within `retapWindow` queues a dash and resumes walking the same way. Any other finger sets
`TouchInput.WeaponHeld` (charge weapons charge/release like holding Q) and `TouchInput.AimScreen`, which
`ProjectileAim.MouseWorld` uses instead of the mouse in Touch mode. Test in the Editor with Game view ▸ Simulator (mouse =
one finger); multi-finger needs a device.

**Melee vs weapon are separate buttons** (2026-10-13): `Player/Attack` (left click, X, gamepad `buttonWest` = square)
is always the melee combo (`PlayerAttack`); `Player/Weapon` (Q, gamepad **R1** `rightShoulder`; it replaced the
unused `Fireball` action) fires the equipped weapon (`WeaponUser`). Equipping a weapon no longer silences the melee.
Touch: `weaponButton` (was `fireballButton`) → `TouchInput.QueueWeapon/ConsumeWeapon`, shown only while a weapon is
equipped, with its own cooldown dial; the weapon has its own HUD slot, `CooldownSlot.Weapon` (weapon icon).

**Interact** (`InteractInput.Pressed`): E / Enter, gamepad **triangle** (`buttonNorth`), or the
touch `interactButton` ("USAR"), which only shows while something can be interacted with: `InteractionPromptUi.IsVisible`
or an interactable calling `TouchInput.RequestInteractButton()` that frame (the shop does, while an altar is focused).
It also reports `TouchInput.InteractHeld` for hold actions (recycling a ground item; keyboard uses hold F).

**Parry** (`Player/Parry`, read by `PlayerParry`): right mouse button, `L`, gamepad `buttonEast`;
touch `parryButton` → `TouchInput.QueueParry/ConsumeParry`. Listed in `HudControlsLegend.Rows`.

**Gamepad layout (2026-10-14)**: square = melee, **triangle = interact** (hold = recycle a ground item), **L1 = dash**, **R1 = weapon**, circle = parry.

**Options menu with a gamepad** (`OptionsMenuController.OnNavigationMove`): up/down walks ONE linear list of stops per page (tab, sliders,
mute toggles, one stop per selector row, buttons, back) — UI Toolkit's geometric navigation skipped right-aligned toggles. Left/right
changes a slider or a selector, switches tabs. The focused row gets `option-row--focus` (USS) for feedback. Pause menu pauses the
music (`AudioManager.SetMusicPaused`); Options un-pauses it so volume changes are audible.
