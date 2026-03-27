## ADDED Requirements

### Requirement: HUD minimap displays real-time player position
The system SHALL render a minimap in the top-left HUD and update the player indicator position in real time based on the configured world-to-map coordinate conversion.

#### Scenario: Player icon updates during movement
- **WHEN** the player moves in world space during gameplay
- **THEN** the minimap player indicator updates position to the corresponding map location without requiring manual refresh

### Requirement: HUD minimap uses circular frame and custom map image
The system SHALL display the minimap within a circular frame and SHALL allow developers to assign a custom map image asset for the minimap background.

#### Scenario: Custom map image is applied in circular minimap
- **WHEN** a custom map image is assigned in map configuration
- **THEN** the minimap renders that image inside the circular frame in the top-left HUD
