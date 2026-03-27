## Context

The project is a Unity game that currently lacks an in-game map workflow for continuous navigation. Players need a compact HUD map for moment-to-moment movement and a fullscreen mode for route planning, both driven by the same world-to-map coordinate system and objective data.

Constraints include: supporting custom map images supplied by the project, preserving gameplay visibility with a circular top-left minimap, and keeping control interaction simple via a single `M` key toggle.

## Goals / Non-Goals

**Goals:**
- Provide a real-time minimap in the top-left HUD with player position updates.
- Render mission objective markers in both minimap and fullscreen map modes.
- Support custom map texture assignment without code changes.
- Toggle between minimap and fullscreen map using `M` while preserving marker/player state.

**Non-Goals:**
- Implementing dynamic fog-of-war, terrain scanning, or procedural map generation.
- Building a quest authoring system; this change only visualizes existing mission targets.
- Introducing multiplayer teammate tracking or networked map synchronization.

## Decisions

1. Shared map state, dual presentation
- Decision: Use one map controller/model for position conversion and marker state, with two visual layouts (mini + fullscreen).
- Rationale: Prevents state drift and duplicated update logic.
- Alternative considered: Two independent map widgets with separate logic. Rejected due to synchronization complexity.

2. Image-driven coordinate mapping
- Decision: Introduce a map configuration asset (world bounds + map sprite/texture metadata) for world-to-UI conversion.
- Rationale: Allows replacing map artwork by asset assignment while keeping coordinate math stable.
- Alternative considered: Hardcoding conversion values in scripts. Rejected because it is brittle and harder to tune.

3. Circular mask for minimap only
- Decision: Apply a circular mask/container for the compact HUD version, while fullscreen uses rectangular full viewport rendering.
- Rationale: Matches requested visual style and maximizes readability in fullscreen.
- Alternative considered: Circular fullscreen map. Rejected due to reduced usable area and readability.

4. Input toggle as UI state machine
- Decision: Handle `M` with explicit state transitions (`Minimap` <-> `Fullscreen`) and idempotent view activation.
- Rationale: Reduces edge-case bugs from repeated key presses and keeps behavior testable.
- Alternative considered: Simple panel enable/disable toggle without state guard. Rejected because rapid input can desynchronize UI visibility.

## Risks / Trade-offs

- [Risk] Incorrect world-to-map calibration can place player/targets inaccurately. -> Mitigation: expose bounds in config and add validation gizmo/debug readout.
- [Risk] Fullscreen map may conflict with existing HUD/input focus. -> Mitigation: centralize map mode ownership and define precedence with other overlays.
- [Risk] High marker counts could affect UI performance. -> Mitigation: pool marker UI elements and update only visible/active objectives.
- [Trade-off] Shared controller improves consistency but introduces tighter coupling between two views. -> Mitigation: keep rendering adapters thin and data-driven.

## Migration Plan

1. Add map config asset and default values for current level/world bounds.
2. Add minimap HUD prefab with circular frame/mask and player icon.
3. Add fullscreen map canvas/panel reusing same map controller and marker feed.
4. Integrate objective marker provider to map controller.
5. Wire input action/key (`M`) to map mode toggle and verify interaction with existing UI.
6. Playtest and tune icon scaling, marker styling, and transition behavior.
7. Rollback strategy: disable new map prefabs/input binding and fallback to prior HUD-only flow if blocking issues occur.

## Open Questions

- Should fullscreen mode pause gameplay or remain real-time during movement/combat?
- Should objective markers include distance labels or only icon indicators in this phase?
- Do completed/hidden objectives disappear immediately or use a delayed fade behavior?
