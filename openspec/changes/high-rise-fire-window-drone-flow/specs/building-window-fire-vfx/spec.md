## ADDED Requirements

### Requirement: Window fire and smoke effects are present at designated anchors
The system SHALL attach or reference smoke and flame visual effects (particle systems, VFX Graph, or equivalent) at configured window positions on the high-rise facade so that a burning-window look is visible before player interaction.

#### Scenario: Effects visible before interaction
- **WHEN** the fire mission scene loads and the window is in its initial burning state
- **THEN** both smoke and flame effects for that window are active and visible from typical drone viewing distances

### Requirement: Fire effects can be suppressed when the sprinkler phase completes
The system SHALL provide a controlled way to stop or substantially hide flame effects at a window when the sprinkler/extinguish interaction succeeds, without requiring scene reload.

#### Scenario: Flames stop after successful extinguish
- **WHEN** the sprinkler phase completes successfully for that window
- **THEN** flame effects at that window’s anchor are stopped or disabled so flames are no longer visible

### Requirement: Smoke effects respond to extinguishment
The system SHALL reduce, stop, or fade smoke at the window when the sprinkler phase completes, consistent with the design choice for that window (default: smoke SHALL NOT continue at full intensity after extinguish).

#### Scenario: Smoke diminishes after extinguish
- **WHEN** the sprinkler phase completes successfully for that window
- **THEN** smoke emission at that window is stopped, faded, or clearly reduced compared to the pre-extinguish state
