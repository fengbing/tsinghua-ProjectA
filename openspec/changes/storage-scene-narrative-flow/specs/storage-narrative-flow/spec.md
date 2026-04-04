## ADDED Requirements

### Requirement: Storage scene narrative image sequence

The system SHALL present a linear sequence of full-screen (or dominant) **image-based** UI phases when the storage scene begins, in this order: (1) initial loading screen, (2) background story screen, (3) second loading screen, (4) transition to normal in-scene view with narrative overlays hidden.

#### Scenario: Enter storage scene

- **WHEN** the storage scene finishes loading and the narrative system is enabled
- **THEN** the initial loading image panel is shown before the background story panel
- **AND** gameplay movement input that would control the drone SHALL remain disabled until the narrative sequence completes (unless a documented skip feature is implemented).

#### Scenario: Complete pre-play narrative

- **WHEN** the narrative sequence reaches the end of the second loading phase
- **THEN** all narrative image panels for this flow are hidden
- **AND** normal scene interaction (including decrypt / 取件码 UI if present in scene) becomes available per existing scene setup.

### Requirement: One-shot staged audio before gameplay

The system SHALL play staged audio **at most once per storage scene session** for each listed cue: one clip on the first loading phase, one on the background story phase, two clips in order on the second loading phase, and two clips in order after entering the playable scene view (post-overlay).

#### Scenario: First loading audio

- **WHEN** the initial loading screen is shown for the first time in the session
- **THEN** exactly one associated audio clip plays and does not automatically repeat if the panel is shown again within the same session.

#### Scenario: Story screen audio

- **WHEN** the background story screen is shown for the first time in the session
- **THEN** exactly one associated audio clip plays once for that phase.

#### Scenario: Second loading dual audio

- **WHEN** the second loading screen is entered for the first time in the session
- **THEN** two associated audio clips play **sequentially** (first completes or starts per design, then the second), each only once for that phase.

#### Scenario: Post-enter dual audio

- **WHEN** narrative panels have been dismissed and the playable scene view is active for the first time in the session
- **THEN** two associated audio clips play in order, each only once for that phase.
