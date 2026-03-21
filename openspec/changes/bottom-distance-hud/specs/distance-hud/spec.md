## ADDED Requirements

### Requirement: Bottom distance strip placement

The system SHALL render the distance indicator as a horizontal strip anchored to the bottom safe area of the screen. The strip SHALL occupy only a fraction of the viewport height (configurable; default visually thin, e.g. roughly 8%–15% of screen height) and SHALL NOT cover the full screen.

#### Scenario: HUD visible during play

- **WHEN** the game view is active and the distance HUD is enabled
- **THEN** the strip appears along the bottom edge of the screen within the configured height

### Requirement: Center-to-sides falloff

The strip’s visual intensity (opacity and/or color weight) SHALL be highest at the horizontal center of the screen and SHALL decrease toward the left and right edges, producing a “radiate from center to sides” appearance.

#### Scenario: Symmetric horizontal falloff

- **WHEN** the player views the bottom strip
- **THEN** the brightest or most visible part of the effect aligns with the screen horizontal center and fades toward both sides

### Requirement: Distance-based color bands

Given a display distance `d` in meters, the system SHALL map `d` to a base color with smooth blending between bands using these default thresholds:

- **WHEN** `d > 300`, the base tone SHALL be white (or near-white neutral).
- **WHEN** `150 < d ≤ 300`, the base tone SHALL interpolate between white and yellow.
- **WHEN** `50 < d ≤ 150`, the base tone SHALL interpolate between yellow and red.
- **WHEN** `d ≤ 50`, the base tone SHALL be red-dominant.

#### Scenario: Approaching critical range

- **WHEN** the displayed distance decreases from above 300 m to 50 m or below
- **THEN** the strip’s base color transitions continuously from white through yellow to red without a hard flicker at exact threshold frames (assuming finite frame-to-frame distance changes)

### Requirement: Semi-transparent gradient styling

The strip SHALL use semi-transparent layering with a visible gradient along the vertical and/or horizontal axis as designed, so that underlying gameplay remains partially visible. The overall look SHALL suggest a premium “glass” or soft-glow bar rather than an opaque solid block.

#### Scenario: Game world remains partially visible

- **WHEN** the strip is shown over the 3D view
- **THEN** the user can still perceive content behind the strip through transparency

### Requirement: Blur or agreed fallback

The system SHALL apply a frosted / blurred treatment to the strip region **when** the project’s render pipeline supports the chosen blur approach within acceptable performance. **If** full-screen or local UI blur is not feasible, the system SHALL use an agreed fallback (e.g. layered soft gradients) that preserves the semi-transparent gradient intent documented in design.

#### Scenario: Fallback when blur unavailable

- **WHEN** the blur implementation is disabled or not supported in a build target
- **THEN** the strip still renders with semi-transparent gradient styling and meets the color and breathing requirements

### Requirement: Breathing animation

The strip SHALL exhibit a periodic “breathing” modulation of overall visibility (e.g. alpha or emissive intensity) using a smooth periodic function. The period SHALL be configurable; default SHOULD be in a calm range (e.g. approximately 2–4 seconds per cycle).

#### Scenario: Continuous breathing during display

- **WHEN** the distance HUD is enabled over multiple seconds
- **THEN** the strip’s visibility rhythmically pulses in a smooth, repeating pattern

### Requirement: Distance data source

The HUD SHALL obtain the displayed distance in meters from a single, explicit integration point (e.g. interface, serialized reference, or event). The HUD SHALL NOT hardcode gameplay-specific distance calculations inside the view component.

#### Scenario: External provider updates distance

- **WHEN** the gameplay system updates the current distance value available to the HUD
- **THEN** the strip’s colors update to reflect the new distance according to the distance-based color rules

### Requirement: Configurable thresholds and tuning

Default distance thresholds SHALL be 300 m, 150 m, and 50 m for the white / yellow / red bands respectively. These values and breathing parameters (period, amplitude) SHALL be adjustable in the Unity Inspector or equivalent configuration without code changes.

#### Scenario: Designer adjusts thresholds

- **WHEN** a designer changes the yellow upper bound from 150 m to 180 m in configuration
- **THEN** the color mapping uses the new value at runtime
