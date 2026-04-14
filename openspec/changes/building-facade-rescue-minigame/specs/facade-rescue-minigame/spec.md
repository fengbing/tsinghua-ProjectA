## ADDED Requirements

### Requirement: Minigame entry gate

The system SHALL start the facade rescue minigame only when all of the following are true: the associated window fire is fully extinguished, two configured post-extinguish audio clips have each completed playback, and the drone is overlapping the configured minigame trigger volume.

#### Scenario: Entry after full sequence

- **WHEN** the fire is extinguished and both configured audio clips have finished and the drone collider overlaps the minigame trigger
- **THEN** the facade rescue minigame enters its active fullscreen state exactly once until it completes or is reset by mission logic

#### Scenario: Blocked before audio completes

- **WHEN** the fire is extinguished but fewer than two configured clips have finished
- **THEN** the facade rescue minigame SHALL NOT open even if the drone is inside the trigger volume

### Requirement: Fullscreen facade presentation

The system SHALL present the minigame as a fullscreen 2D overlay with a building facade background image covering the play area, and SHALL hide or suppress conflicting gameplay UI input according to the same global rules used for other fullscreen built-in minigames.

#### Scenario: Overlay focus

- **WHEN** the minigame is active
- **THEN** the facade root is visible and interactive inputs target the minigame unless the global pause layer takes precedence

### Requirement: Initial elevator positioning

The system SHALL, upon entering the minigame, animate the elevator representation from the top of the facade downward until it aligns with the first window’s configured floor height before accepting window input for rescue step one.

#### Scenario: First stop

- **WHEN** the minigame becomes active
- **THEN** the elevator moves automatically to the first window’s vertical stop before the first window becomes clickable

### Requirement: Per-window interaction sequence

For each active window in order (one through three), the system SHALL enforce this interaction order: window activation → user opens a panel showing an informational image and an action image-button → user opens a trapped-person details panel → user presses a control that reveals a bottom choice bar.

#### Scenario: Window one pipeline

- **WHEN** the elevator is aligned to window one
- **THEN** only window one accepts clicks and the UI SHALL follow the ordered panels before showing the bottom choice bar

#### Scenario: Later windows locked

- **WHEN** the active index is window one
- **THEN** window two and window three SHALL NOT advance the flow if clicked

### Requirement: Escape choice controls

The system SHALL show a bottom choice bar with exactly three buttons: left slide, center elevator, right slide. The system SHALL treat any of the three as a valid completion action for the current person once the bar is visible.

#### Scenario: Slide plays sound

- **WHEN** the user selects either slide button
- **THEN** the system plays the configured slide sound effect once

#### Scenario: Elevator silent aside from flow

- **WHEN** the user selects the elevator button
- **THEN** the system completes the choice without playing the slide sound effect

### Requirement: Post-choice UI and elevator content

After any valid choice from the bottom bar, the system SHALL close the trapped-person details panel, close the bottom choice bar, and SHALL attach the current person’s portrait to the elevator graphic in the next available portrait slot.

#### Scenario: Portrait stacking

- **WHEN** the first person is rescued
- **THEN** exactly one portrait slot on the elevator becomes occupied with that person’s image

### Requirement: Sequential floor progression

After resolving a person, the system SHALL automatically move the elevator downward to the next window’s configured height and advance the active window index until three persons are rescued.

#### Scenario: Advance from window one to two

- **WHEN** window one’s choice is completed
- **THEN** the elevator animates to window two’s height and window two becomes the sole interactive window

### Requirement: Final exit and system prompt

After the third person is rescued, the system SHALL continue animating the elevator downward until it is fully off the visible facade, then SHALL play the configured system prompt audio (or equivalent system dialog audio hook).

#### Scenario: Completion audio

- **WHEN** the elevator has cleared the facade after the third rescue
- **THEN** the system prompt audio starts and the minigame reaches a completed state suitable for mission handoff
