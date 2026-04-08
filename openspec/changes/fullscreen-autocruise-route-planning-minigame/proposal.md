## Why

Current drone autocruise route setup lacks an in-game interactive planning flow, making route authoring and validation disconnected from gameplay.
Adding a fullscreen route-planning minigame now enables players/designers to define meaningful waypoint paths directly in context and feed them into autocruise reliably.

## What Changes

- Add a fullscreen built-in 2D planning minigame that can be toggled with `H`.
- Add a route-node UI graph where each selectable 2D point maps to a concrete scene waypoint.
- Implement click-to-select behavior: selected nodes change visual state and are appended to the current path in order.
- Draw path segments dynamically: first selected node connects from the defined start node; each subsequent node connects to the previous selected node; final completion reaches the end node.
- Output the selected ordered route as an autocruise-consumable path definition for existing drone autocruise systems.

## Capabilities

### New Capabilities
- `fullscreen-route-planning-minigame`: Fullscreen 2D route-planning gameplay for selecting waypoint sequences and generating autocruise routes.

### Modified Capabilities
- None.

## Impact

- Affected systems: in-game UI input handling, fullscreen overlay state management, route node visualization, and line rendering.
- Integrates with existing drone autocruise route data model and route execution entry points.
- Requires deterministic mapping between minigame node IDs and scene waypoint references.
