## ADDED Requirements

### Requirement: EV source profile definition
The system MUST provide a reusable audio profile asset that defines how EV source material is used for drone runtime playback, including loop behavior and runtime tuning defaults.

#### Scenario: Profile can be assigned to runtime controller
- **WHEN** a designer assigns a valid EV-derived profile to the drone audio controller
- **THEN** the controller SHALL load clip and parameter defaults from that profile without code changes

### Requirement: `ys.wav` source onboarding
The system MUST support onboarding of the provided `ys.wav` source clip into the profile and expose settings needed to produce a seamless continuous motor loop.

#### Scenario: Loop-ready EV clip configuration
- **WHEN** profile references `ys.wav` and loop settings are configured
- **THEN** runtime playback SHALL use the configured loop behavior for continuous motor bed playback

### Requirement: Tunable mapping ranges in profile
The profile MUST store tunable ranges for base pitch/volume, thrust boost amount, smoothing times, vertical modifiers, and pan depth.

#### Scenario: Tuning change without script modification
- **WHEN** a designer changes a mapping value in the profile
- **THEN** runtime behavior SHALL reflect the new value without modifying source code

### Requirement: Safe fallback behavior
The runtime system MUST fail safely when profile data is incomplete (for example missing mixer parameter bindings) and continue playing valid base motor audio.

#### Scenario: Missing optional mixer parameter
- **WHEN** an optional mixer parameter name is empty or unavailable
- **THEN** the controller SHALL skip that modulation path and continue processing remaining audio logic without runtime exception
