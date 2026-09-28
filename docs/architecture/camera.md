# Camera (`Assets/Scripts/Gameplay/CameraFollow.cs`)
The camera follows both axes (`followVertical` on everywhere), but **the two axes are not treated
the same**: horizontal is smoothed with `smoothTime` (0.18), vertical is **hard-locked to the
player with no smoothing at all** (`verticalSmoothTime` defaults to 0 = snap). That split is the
whole point — an earlier pass that smoothed the vertical axis and added a fall look-ahead was
rejected as unplayable, because any vertical lag means you don't see where you're landing until
after you've landed. If vertical follow ever feels wrong again, fix the framing (`offset.y`, zoom),
**not** by adding vertical smoothing back.

Zoom is a separate knob: `RunManager.cameraOrthographicSize` is forced onto every
`CameraFollow` camera after each load, so the hub and the sections match.
`ShakeAll(amplitude, duration)` is the project-wide camera shake (see Bosses).

**Bounds — what is clamped is the visible rectangle, not the camera's centre.** Background art is
AI-generated with no margin past the playable area, so its edges *are* the limits of the framing.
`CameraBoundsSource.Background` (the default) measures the GameObject named **`BG`** in the
camera's **own scene** — by name, per scene, because run sections load additively and two `BG`s
are in memory during a transition. **If `BG` has its own `Renderer`, that renderer alone is the
bounds and its children are ignored**: level decoration (platforms, frames, vines) is parented
under `BG` and sticks out past the background image, so unioning it pushes the limit *beyond the
art* — in `World1_Boss` the image ends at x 22.82 and the union reached 22.95, which is exactly the
sliver of clear-colour blue this system exists to prevent. Only a `BG` with no renderer of its own
(a parallax container) falls back to the union of its children. `ClampCenter` subtracts half a viewport
(`orthographicSize` × `aspect`, both read every frame since `RunManager` rewrites the zoom after
each load) from that rectangle; if the background is *smaller* than the screen on an axis there is
no valid position, so the camera centres on the background on that axis, splitting the overspill
instead of dumping it all on one side. The clamp is applied to the SmoothDamp **target** (so the
camera doesn't accumulate velocity into a wall and lurch off the edge) **and** to the final
position after shake (so a boss stomp can't shove the view past the art). `Manual` mode keeps a
hand-typed rectangle for a scene with no background; `None` disables it. No `BG` found is not an
error — the camera just follows unclamped, as before.

`Tools > RedMagic > Camera > Auditar encuadre (BG vs cámara)` (`CameraBoundsAudit`) opens every
scene under `Assets/Scenes` and reports, at the zoom `RunManager` actually forces and at both 16:9
and 20:9, how many world units each background is short of covering the screen. Run it whenever new
background art lands: a background too small produces no error and no pink material, only a visible
strip of nothing at the level's edge. Selecting the camera also draws it — green is the background,
amber is where the camera centre may go, red means the screen doesn't fit.
