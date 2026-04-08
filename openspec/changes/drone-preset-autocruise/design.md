## Context

The drone is driven by `PlaneController` on a `Rigidbody` with hover-style velocity smoothing. There is no existing waypoint or nav agent; tutorials and other systems already call `SetInputEnabled` on `PlaneController`. Authors need to place and reorder waypoints in the scene without recompiling.

## Goals / Non-Goals

**Goals:**

- **Preset route**: An ordered sequence of waypoints (minimum: world positions), editable in the Unity Editor (Transforms in a parent chain, or a dedicated route component listing references).
- **Start with Y**: Press **Y** to begin following the route from waypoint 0 (or resume policy defined in spec); configurable key in Inspector.
- **Progression**: Move toward current waypoint; when within **arrival radius**, advance to next; on final waypoint arrival, **end cruise** and restore manual control.
- **Manual override off during cruise**: Disable `PlaneController` input while autocruise is active; re-enable when cruise completes or is cancelled.
- **Cruise driver**: Apply motion in `FixedUpdate` consistent with physics (set target velocity or steer `Rigidbody` toward waypoint—match hover drone feel where possible).

**Non-Goals:**

- Full pathfinding / obstacle avoidance (routes are strictly preset; collisions rely on existing physics).
- Cinemachine or camera automation (unless trivially free).
- Multi-drone fleet routing.

## Decisions

1. **Authoring model** — Prefer a **`DroneAutocruiseRoute`** component on an empty GameObject: serialized **`Transform[] waypoints`** (or child transforms under a `RouteRoot`) so designers drag scene objects and reorder in Inspector. **Alternative**: `ScriptableObject` with `Vector3[]` for reuse across scenes; **choice**: start with **Transform list** for gizmo visibility and scene tweaking; SO can be a later extension.

2. **Toggle vs one-shot** — **Y** on `KeyDown`: if **idle**, start from index 0; if **cruising**, **cancel** and return control (same key as “stop”). Document in UI/tooltip.

3. **Movement law** — Compute **desired horizontal direction** (and vertical if waypoints differ in Y) toward active waypoint; set **`Rigidbody.velocity`** (or use `MovePosition` only if consistent with existing hover—**default**: velocity toward target with capped speed matching a serialized **cruise max speed**, separate from player boost ramp).

4. **Facing** — Optional: align yaw to movement direction or leave camera-driven; **default**: do not fight `PlaneController` camera follow—only drive translation unless spec requires facing.

5. **Integration** — **`PlaneController.SetInputEnabled(false)`** when cruise starts; **`true`** when cruise ends or cancels. If `DroneGripper` is holding cargo, **default**: **block cruise start** or **auto-cancel** with log—pick **block start** to avoid joint issues (document in spec).

6. **Input** — Use **`Input.GetKeyDown(KeyCode.Y)`** initially to match existing `PlaneController` legacy input style; expose `KeyCode` in Inspector.

## Risks / Trade-offs

- **[Risk]** Cruise velocity fights `PlaneController` if both run in same `FixedUpdate` order → **Mitigation**: only one active; cruise component runs when input disabled or cruise uses dedicated mode flag on a thin coordinator.
- **[Risk]** Waypoint under terrain / inside collider → **Mitigation**: author responsibility; optional gizmo draw for radii.
- **[Risk]** Tutorial input restriction conflicts → **Mitigation**: cruise disabled when tutorial locks input, or tutorial explicitly allows Y—note in tasks.

## Migration Plan

- Add route object + component to target scenes; assign waypoints; add autocruise driver to drone prefab.
- Rollback: remove/disable components; no data migration.

## Open Questions

- Should cruise **loop** the route vs **one-shot**? **Default one-shot** per proposal; optional Inspector bool `loopRoute`.
- **Arrival radius** global vs per-waypoint? **Default global** float for v1.
