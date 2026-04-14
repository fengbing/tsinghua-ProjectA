## ADDED Requirements

### Requirement: Speed-driven base motor bed
The system MUST compute a normalized speed ratio from drone rigidbody velocity and map it to base engine pitch and base volume so steady cruise remains sonically stable at high speed.

#### Scenario: Stable cruise pitch at max speed
- **WHEN** `Rigidbody.velocity.magnitude` remains near `MaxSpeed` for at least 1 second without major speed fluctuations
- **THEN** the base pitch SHALL converge to the configured high-speed target and remain stable within a small tolerance band

#### Scenario: Low-speed motor bed at takeoff
- **WHEN** speed ratio is near zero
- **THEN** the base pitch and volume SHALL converge to configured minimum values without discontinuities

### Requirement: Smoothed parameter transitions
All continuously changing audio parameters (at minimum pitch, volume, and pan) MUST be smoothed with damped interpolation and SHALL NOT step-change on single-frame input transitions.

#### Scenario: Abrupt input edge does not click
- **WHEN** player input changes from no thrust to thrust in one frame
- **THEN** audio parameters SHALL transition through smoothing functions rather than immediate hard assignment

### Requirement: Intent-driven thrust boost overlay
The system MUST detect active thrust intent from horizontal or vertical thrust input and apply a temporary pitch boost overlay above the speed-based base pitch.

#### Scenario: Constant speed with held thrust still sounds loaded
- **WHEN** drone speed is near constant and any thrust key remains held
- **THEN** final pitch SHALL stay above base pitch by a configured boost amount

#### Scenario: Boost decays after release
- **WHEN** all thrust input is released
- **THEN** the boost overlay SHALL decay smoothly back to zero using configured release timing

### Requirement: Vertical lift/drop tonal shaping
The system MUST apply additional vertical state shaping where lift increases energy/presence and drop reduces brightness/loudness.

#### Scenario: Lift state increases presence
- **WHEN** lift intent is active (`Space` or equivalent positive vertical intent)
- **THEN** output SHALL increase configured lift emphasis parameters (for example volume bonus and mixer EQ/presence value)

#### Scenario: Drop state darkens tone
- **WHEN** drop intent is active (`LeftCtrl` or equivalent negative vertical intent)
- **THEN** output SHALL decrease configured high-frequency emphasis and lower output loudness toward configured descent targets

### Requirement: Horizontal turn stereo panning
The system MUST map horizontal steering intent to stereo panning with configurable depth and smoothing.

#### Scenario: Right turn pans right
- **WHEN** horizontal input is positive
- **THEN** pan value SHALL move toward a positive pan target

#### Scenario: Neutral steering returns center
- **WHEN** horizontal input returns to zero
- **THEN** pan value SHALL smoothly return toward center
