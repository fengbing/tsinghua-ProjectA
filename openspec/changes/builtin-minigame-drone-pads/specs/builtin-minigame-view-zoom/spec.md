## ADDED Requirements

### Requirement: Background image as 2D playfield

The system SHALL display the mini-game background using a configurable image (sprite or texture) large enough for panning, aligned with the 2D gameplay plane used by the orthographic camera.

#### Scenario: Scene shows configured backdrop

- **WHEN** the `BuiltinMiniGame` scene is running and a background image asset is assigned
- **THEN** the player sees that image as the static visual base of the play area (modulo pan/zoom)

### Requirement: Zoom with scroll wheel

The system SHALL adjust view magnification when the user rotates the mouse scroll wheel while the playfield is accepting input (not blocked by higher-priority UI, per design).

#### Scenario: Scroll in increases magnification

- **WHEN** the user scrolls the wheel in the configured “zoom in” direction over the playfield
- **THEN** the visible area shrinks (effective zoom in) within configured min/max limits

#### Scenario: Scroll out decreases magnification

- **WHEN** the user scrolls the wheel in the “zoom out” direction over the playfield
- **THEN** the visible area expands within configured min/max limits

### Requirement: Pan driven by mouse position

The system SHALL update camera (or root) translation so that the view pans in response to the mouse cursor position on screen, using the behavior implemented in `MiniGameViewController` (or successor) and documented in `design.md`.

#### Scenario: Moving the mouse shifts the view

- **WHEN** the user moves the mouse within the game window and the playfield is active
- **THEN** the background and world-aligned objects move relative to the screen according to the pan rule (continuous mapping from cursor position to camera offset)
