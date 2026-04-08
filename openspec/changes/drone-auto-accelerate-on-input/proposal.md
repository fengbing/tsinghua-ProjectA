## Why

Drone movement currently treats “boost” as a separate mode toggled by holding the right mouse button, which splits attention between steering and an extra input. The desired feel is that forward/strafe intent naturally ramps to full speed over a short window, with audio reinforcing that buildup and stopping cleanly when the player releases movement.

## What Changes

- Remove reliance on **mouse right-button** for top speed; maximum effective speed is reached automatically while **movement (directional) input** is held.
- While movement input is active, **interpolate** from base cruise speed to the previous “boost” peak over **2 seconds** (configurable in code/Inspector).
- During that 2-second window, **play a dedicated acceleration audio clip** (loop or sustained playback as appropriate); **stop** the audio as soon as movement input goes idle (all axes below deadzone); **play again** from the start (or per design: restart ramp) on the next movement press.
- **Tutorial / gating**: `TutorialInputRestriction`, `TutorialRing`, `TutorialHud`, and any code that queries “right-click boost” must be updated to match the new model (e.g. “movement-held acceleration” or timed ramp) so tutorials and restrictions remain coherent.

## Capabilities

### New Capabilities

- `drone-movement-acceleration`: Defines how the plane/drone reaches max speed from directional input, timing of the ramp, audio start/stop rules, and how external systems (tutorials) observe “accelerating / at max” state.

### Modified Capabilities

- _(none — no existing baseline specs in `openspec/specs/`.)_

## Impact

- **Primary**: `Assets/Scripts/PlaneController.cs` — speed cap logic, new ramp timer/state, optional `AudioSource` + clip for acceleration SFX.
- **Secondary**: `Assets/Scripts/Tutorial/TutorialInputRestriction.cs`, `TutorialRing.cs`, `TutorialHud.cs` — replace or extend “right mouse boost” checks with the new acceleration semantics.
- **Scenes / prefabs**: Drone prefab or scene objects may need an assigned acceleration clip and AudioSource reference if not added purely in code.
- **Input**: Uses existing movement axes (WASD / arrows / whatever feeds `PlaneController`); no new key binding for boost.
