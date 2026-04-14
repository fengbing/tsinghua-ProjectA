## ADDED Requirements

### Requirement: Flight controller telemetry integration
The drone audio runtime MUST bind to the active drone physics/controller context and consume at minimum speed and max-speed reference values for state mapping.

#### Scenario: Uses Rigidbody speed as source of truth
- **WHEN** rigidbody speed changes due to thrust, drag, or external dynamics
- **THEN** audio base mapping SHALL follow the resulting physical speed rather than input intent alone

### Requirement: Input intent integration for thrust and vertical actions
The runtime MUST detect horizontal and vertical thrust intent from configured flight inputs and use that intent to drive thrust boost, lift, and drop overlays.

#### Scenario: Any thrust key activates pushing state
- **WHEN** at least one of horizontal, vertical, lift, or drop thrust inputs is active
- **THEN** the system SHALL mark pushing intent active and apply intent-based audio overlays

### Requirement: Autocruise compatibility
When the drone enters autocruise or other virtual-input movement modes, audio behavior MUST remain coherent and SHALL continue to represent active movement intent.

#### Scenario: Autocruise keeps motor load feedback
- **WHEN** manual input is disabled and movement is driven by autocruise virtual axes
- **THEN** the drone audio SHALL continue updating pitch/volume/pan from telemetry and virtual movement intent without muting into idle

### Requirement: Input lock and modal UI safety
The runtime MUST handle states that intentionally disable pilot controls (tutorial gates, route planner, narrative UI) without producing unstable audio artifacts.

#### Scenario: Planner UI lock transitions to neutral intent
- **WHEN** planning mode disables manual control and drone movement intent becomes neutral
- **THEN** thrust overlays SHALL decay smoothly to neutral while base motor bed follows actual speed state
