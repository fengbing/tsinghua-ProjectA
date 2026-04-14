## ADDED Requirements

### Requirement: EV source audio is analyzed into reusable drone layers
The system SHALL define a repeatable pipeline to analyze `tsl audio` and produce at least base, dynamic, and maneuver-focused reusable clips for drone playback.

#### Scenario: Source analysis extracts timeline segments
- **WHEN** the pipeline is executed for the provided source clip
- **THEN** it SHALL identify usable acceleration, cruising, and deceleration regions with documented timestamps

#### Scenario: Derived clips are loop-ready and normalized
- **WHEN** output clips are exported for runtime use
- **THEN** each clip SHALL have documented loop points, loudness normalization target, and import settings required by the project

### Requirement: Pipeline output includes tunable metadata
The system MUST generate or maintain metadata needed for runtime mapping and later tuning.

#### Scenario: Metadata records playable ranges and intended role
- **WHEN** derived clips are added to project assets
- **THEN** associated metadata SHALL include role tag, recommended gain range, and pitch modulation range

#### Scenario: Source replacement preserves runtime contract
- **WHEN** a new EV-style source audio is substituted in the future
- **THEN** the pipeline SHALL produce outputs that preserve the same mixer input contract without requiring runtime script redesign
