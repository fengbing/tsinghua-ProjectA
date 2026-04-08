## ADDED Requirements

### Requirement: Movement-driven speed ramp

The drone movement controller SHALL increase its effective maximum horizontal flight speed from the configured base `maxSpeed` to the configured peak speed (historically `maxSpeed` multiplied by `boostSpeedMultiplier`) over a continuous hold of valid movement input, completing the transition in exactly **two seconds** by default (duration MUST be configurable in the Unity Inspector).

Valid movement input SHALL mean the Horizontal and Vertical axes used for planar movement (e.g. WASD / arrow keys) are outside their configured deadzones, consistent with existing idle detection for those axes in `PlaneController`.

#### Scenario: Ramp to peak over two seconds

- **WHEN** the player holds valid planar movement input continuously
- **THEN** the effective maximum speed SHALL interpolate from base to peak such that it reaches peak at two seconds (or the configured duration) from the moment input became active after an idle period

#### Scenario: Reset when input stops

- **WHEN** planar movement input returns to idle (inside deadzones)
- **THEN** the ramp progress SHALL reset so that the next active input starts a new two-second ramp from base toward peak

### Requirement: Acceleration audio tied to movement

While valid planar movement input is present and ramp progress is between zero and completion, the system SHALL play a designated acceleration audio clip on a drone audio source. When planar movement input becomes idle, playback SHALL stop immediately. When the player activates planar movement again after idle, playback SHALL start again in line with the new ramp cycle.

#### Scenario: Audio starts with movement

- **WHEN** the player begins holding valid planar movement input from an idle state
- **THEN** the acceleration audio SHALL begin playing (or restart if it was stopped)

#### Scenario: Audio stops when keys release

- **WHEN** the player releases all planar movement input into the idle state
- **THEN** the acceleration audio SHALL stop without continuing in the background

### Requirement: No right-mouse speed boost

Mouse button 1 (right mouse) SHALL NOT be used to determine maximum speed or to toggle boost mode. Tutorial and gameplay code that previously treated right mouse as “boosting” MUST be updated to use movement-based or ramp-completion semantics so behavior remains testable and consistent.

#### Scenario: Right mouse does not change speed cap

- **WHEN** the player holds right mouse but has no valid planar movement input
- **THEN** the effective maximum speed from the ramp system SHALL remain at the base or idle-appropriate cap (not the peak boost cap)

### Requirement: Tutorial gating compatibility

Components that gate or inspect “boost” (e.g. `TutorialInputRestriction`, `TutorialRing`, tutorial HUD messaging) SHALL be aligned with the new model: gating MUST prevent reaching peak ramp speed when disallowed, and pass/fail conditions for rings that required boost MUST use movement or full-ramp state instead of right-mouse state.

#### Scenario: Boost disallowed caps speed

- **WHEN** tutorial logic disables acceleration to peak
- **THEN** the drone SHALL NOT reach the peak speed multiplier cap regardless of how long movement input is held
