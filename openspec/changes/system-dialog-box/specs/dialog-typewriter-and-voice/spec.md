## ADDED Requirements

### Requirement: Typewriter text progression
The system SHALL reveal each sentence character by character in display order instead of rendering the full sentence at once.

#### Scenario: Characters appear incrementally
- **WHEN** a new sentence starts rendering
- **THEN** characters become visible one by one until the full sentence is shown

#### Scenario: Sentence completes after incremental reveal
- **WHEN** the typewriter progression reaches the final character
- **THEN** the sentence state is marked complete for flow control

### Requirement: Per-sentence voice playback
The system SHALL allow each sentence to bind an individual audio clip and SHALL play that clip when the sentence starts.

#### Scenario: Play audio for sentence with clip
- **WHEN** a sentence with a bound audio clip begins
- **THEN** the system starts playback of that sentence's clip

#### Scenario: No audio for sentence without clip
- **WHEN** a sentence without a bound audio clip begins
- **THEN** the system proceeds with text rendering without audio errors

### Requirement: Sentence transition audio handling
The system SHALL stop or replace the current sentence audio when transitioning to the next sentence to prevent overlapping unintended voice.

#### Scenario: Transition to next sentence
- **WHEN** the current sentence ends and the next sentence begins
- **THEN** the previous sentence audio is no longer active and only the next sentence audio may play

#### Scenario: Session end cleanup
- **WHEN** the dialog session ends or is cancelled
- **THEN** any active sentence audio is stopped

### Requirement: Dialog flow completion signals
The system SHALL expose completion signals for sentence completion and full session completion so gameplay scripts can sequence behavior.

#### Scenario: Sentence completion callback
- **WHEN** one sentence finishes rendering (or is skipped to complete)
- **THEN** the sentence completion signal is emitted once for that sentence

#### Scenario: Session completion callback
- **WHEN** all sentences in the session are complete
- **THEN** the session completion signal is emitted once
