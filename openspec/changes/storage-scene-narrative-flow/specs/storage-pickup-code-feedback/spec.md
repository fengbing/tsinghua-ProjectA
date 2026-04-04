## ADDED Requirements

### Requirement: Correct pickup code success feedback

The system SHALL play one **success audio clip** when the player submits the **correct 取件码** through the existing decrypt puzzle flow, with that clip playing **only once per successful solve event** (not on every frame while solved).

#### Scenario: Correct code entered

- **WHEN** the decrypt puzzle reports success (correct full code entry)
- **THEN** one configured success audio clip plays
- **AND** the clip is not replayed again until a new successful solve occurs in the same session if the puzzle allows re-entry, or only once per run as implemented to match product rules.

### Requirement: Open map view after success

After successful 取件码 validation, the system SHALL apply the **same map view change** as pressing the minimap toggle key once (default **M**), by calling the shared minimap controller API used for keyboard input.

#### Scenario: Minimap toggle after success

- **WHEN** the decrypt puzzle success path runs
- **THEN** the minimap UI controller performs exactly **one** toggle equivalent to `Input.GetKeyDown` for the configured toggle key
- **AND** no duplicate toggles occur from the same success event.
