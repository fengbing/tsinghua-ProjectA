## ADDED Requirements

### Requirement: M key toggles minimap and fullscreen map modes
The system SHALL toggle the map display mode between compact minimap and fullscreen map when the player presses the `M` key.

#### Scenario: Toggle to fullscreen map
- **WHEN** the map is in minimap mode and the player presses `M`
- **THEN** the map switches to fullscreen mode

#### Scenario: Toggle back to minimap
- **WHEN** the map is in fullscreen mode and the player presses `M`
- **THEN** the map switches back to minimap mode

### Requirement: Map mode switch preserves map context
The system SHALL preserve player indicator position and mission marker state when switching between minimap and fullscreen modes.

#### Scenario: Player and marker positions persist across toggle
- **WHEN** the player toggles map mode with `M`
- **THEN** the new mode shows player and objective markers at positions consistent with the previous mode
