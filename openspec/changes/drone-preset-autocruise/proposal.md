## Why

Manual flight is fine for skill sections, but the project needs a **repeatable, author-controlled path** so the drone can **automatically fly to one or more preset destinations** without the player steering the whole way. A single hotkey (**Y**) should start that run, while designers define **both the ordered route and the final targets** ahead of time in the Editor.

## What Changes

- Add an **autocruise / autopilot mode** that moves the drone along a **preset ordered list of waypoints** (positions and optional per-point tolerances or dwell times—exact knobs in design/tasks).
- **Press Y** (configurable) to **start** a cruise: follow the authored route until the last preset point (or loop / cancel policy per design).
- **Preset workflow for authors**: scene or prefab objects (e.g. empty GameObjects or a `ScriptableObject` route asset) define the path; no hard-coded world positions in code.
- **Interaction with manual control**: while cruising, **player input is overridden or blended** in a defined way (default: disable `PlaneController` input or drive desired velocity from the cruise controller); **cancel** path (second Y press, Esc, or collision—see spec).
- Optional: **visual/debug gizmos** in Scene view for the polyline route.

## Capabilities

### New Capabilities

- `drone-autocruise-routes`: Defines how preset routes are authored, how Y starts/stops cruise, progression along waypoints, and how cruise cooperates with `PlaneController`, tutorials, and gripper states.

### Modified Capabilities

- _(none — no existing baseline specs under `openspec/specs/`.)_

## Impact

- **Primary**: New component(s) on the drone or a manager (e.g. `DroneAutocruiseController`) plus optional `ScriptableObject` or `Transform[]` route references.
- **`PlaneController`**: `SetInputEnabled` / similar hooks to pause manual input during cruise; may need a small API to apply **desired velocity/position** or a dedicated cruise driver that moves `Rigidbody`.
- **Input**: New **Y** binding (Input Manager or Input System—match project convention).
- **Scenes**: One or more route prefabs / waypoint chains per level as needed.
