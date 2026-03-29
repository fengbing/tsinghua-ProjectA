## ADDED Requirements

### Requirement: Custom pointer graphic

The system SHALL replace or override the default mouse pointer in the mini-game with a designer-specified sprite or texture and configurable hotspot (or follow-sprite fallback if hardware cursor cannot be used).

#### Scenario: Pointer appears as configured art

- **WHEN** the mini-game scene is active and a cursor image is assigned
- **THEN** the user sees the configured graphic at the mouse position instead of the default arrow (within platform limits)

### Requirement: World click picks target position

The system SHALL translate primary click (or mouse down) on the playfield into a 2D world position on the gameplay plane, excluding clicks consumed by blocking UI elements (e.g., return controls).

#### Scenario: Click on empty map yields world point

- **WHEN** the user clicks on the background collider / playfield pick surface
- **THEN** the game resolves a world XY position used as the drone movement target

### Requirement: Drone moves to click position

The system SHALL move the drone object to the resolved world target on each valid playfield click, using motion parameters (snap vs. interpolated) exposed in the inspector.

#### Scenario: Each playfield click retargets drone

- **WHEN** the user clicks a new point on the playfield
- **THEN** the drone travels to that point (or snaps) without requiring a modifier key

### Requirement: Click sound on playfield interaction

The system SHALL play a configured one-shot audio clip on every valid playfield click that triggers pointer targeting (same clip for each click unless multiple clips are explicitly introduced later).

#### Scenario: Audio fires with click

- **WHEN** a valid playfield click is processed
- **THEN** the configured click sound plays once for that click
