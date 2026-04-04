## ADDED Requirements

### Requirement: PlaneGame starts with background music

When PlaneGame becomes the active loaded scene, the mission background music SHALL begin playing as a looping bed for the level.

#### Scenario: Scene enter

- **WHEN** PlaneGame loads and the narrative audio controller becomes active
- **THEN** the configured background music clip plays continuously until stopped by the proximity rule or level exit

### Requirement: Proximity stops BGM and plays a one-shot cue

The system SHALL monitor the distance to the mission target using the same source as the bottom distance HUD strip (`IDistanceHudSource.GetDistanceMeters()`). When that distance is less than or equal to 50 meters, the background music SHALL stop and a configured one-shot audio clip SHALL play exactly once for that play session (no repeat while the player remains inside the threshold).

#### Scenario: First crossing inside 50 m

- **WHEN sampled distance to target is less than or equal to 50 m** and the proximity cue has not yet fired
- **THEN** background music stops and the one-shot clip plays once

#### Scenario: Still inside 50 m after cue

- **WHEN** distance remains less than or equal to 50 m after the one-shot has already played
- **THEN** the one-shot does not play again and BGM does not restart automatically

### Requirement: Successful platform delivery triggers voiced outro

When the package is successfully placed on the designated platform in PlaneGame, the system SHALL play exactly three configured voice clips in strict order (clip 1, then clip 2, then clip 3) with no overlap. The delivery-success sequence SHALL run at most once per session unless the controller is reset by an explicit design-time behavior (default single run).

#### Scenario: Delivery success

- **WHEN** the game determines the package is successfully on the platform
- **THEN** the three voice clips play sequentially

### Requirement: Load Level 2 after final voice

After the third voice clip completes playback, the system SHALL load the Level 2 scene (`Assets/Scenes/Level 2.unity` as registered in the project build settings).

#### Scenario: Outro complete

- **WHEN** the third voice clip has finished playing
- **THEN** the application loads Level 2
