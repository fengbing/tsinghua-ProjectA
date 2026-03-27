## Why

Players currently navigate with limited spatial awareness, which makes orientation and objective tracking harder than necessary. A real-time minimap with mission markers improves moment-to-moment navigation and reduces frustration during task-driven gameplay.

## What Changes

- Add a top-left minimap UI that updates the player indicator in real time based on world position.
- Support rendering mission objective markers on the map so players can see destination targets.
- Add support for custom user-provided map artwork as the minimap background.
- Default minimap presentation to a circular frame in the HUD.
- Add `M` key interaction to toggle from minimap mode to a fullscreen map view and back.

## Capabilities

### New Capabilities
- `hud-minimap-display`: Real-time player position display in a top-left circular minimap using custom map imagery.
- `mission-map-markers`: Objective target markers rendered on minimap/fullscreen map for active tasks.
- `map-view-toggle`: Keyboard toggle (`M`) between compact minimap and fullscreen map presentation.

### Modified Capabilities
- None.

## Impact

- Affects Unity HUD/UI code and scene prefab setup for map containers and markers.
- Introduces data/config surface for assigning custom map image assets and objective marker styling.
- Adds input handling for map mode switching and state synchronization between minimap/fullscreen views.
