## ADDED Requirements

### Requirement: Bottom dialog panel visibility
The system SHALL provide a dialog panel anchored to the bottom of the game screen, and the panel SHALL be shown only when explicitly requested by gameplay logic.

#### Scenario: Show dialog on demand
- **WHEN** a caller invokes the dialog play API with at least one sentence
- **THEN** the bottom dialog panel becomes visible before text starts rendering

#### Scenario: Hide dialog when not needed
- **WHEN** the active dialog session ends or the caller explicitly requests hide
- **THEN** the bottom dialog panel becomes invisible and no residual text is shown

### Requirement: Full-width black panel style
The dialog panel SHALL render as a black rectangle with width equal to the game viewport width.

#### Scenario: Panel spans full game width
- **WHEN** the panel is visible during gameplay
- **THEN** its rendered width matches the current game viewport width

#### Scenario: Panel uses black background
- **WHEN** the panel is visible
- **THEN** its background color is black with no alternative theme required by default

### Requirement: Centered white dialog text
The system SHALL render dialog text in white color and centered within the panel content area.

#### Scenario: Text appears centered
- **WHEN** a sentence is being displayed
- **THEN** the visible characters are positioned at the horizontal center of the panel text area

#### Scenario: Text color is white
- **WHEN** dialog text is rendered
- **THEN** the text color is white
