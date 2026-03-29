## ADDED Requirements

### Requirement: Receivers share visual art

The system SHALL render all receiver objects using one shared sprite (or material) reference so art updates propagate consistently across receivers.

#### Scenario: Uniform receiver appearance

- **WHEN** multiple receiver instances are placed in the scene
- **THEN** they all display the same receiver image sourced from one assigned sprite

### Requirement: Buffer pads share visual art

The system SHALL render all buffer pad objects using one shared sprite reference distinct from the receiver art (unless intentionally set the same by designer).

#### Scenario: Uniform pad appearance

- **WHEN** multiple buffer pads exist
- **THEN** they all display the same buffer-pad image sourced from one assigned sprite

### Requirement: Fixed pairing and relative transform

Each receiver SHALL be associated with exactly one buffer pad. The relative offset (or parent/child relationship) between receiver and pad SHALL remain fixed after authoring; runtime logic SHALL NOT drift this offset for a pair unless the receiver itself is moved in-editor or by future features outside this change.

#### Scenario: Pad position follows receiver offset

- **WHEN** a pair is initialized in the scene
- **THEN** the pad’s position relative to its receiver matches the authored offset (e.g., constant local position under receiver or stored offset vector)

### Requirement: Receiver click shows pad and moves drone

The system SHALL, on primary activation of a receiver, move the drone to the receiver’s world position and then make the paired buffer pad visible (active). If the pad was already visible, it remains visible unless a separate rule hides it.

#### Scenario: First activation of receiver

- **WHEN** the user clicks a receiver
- **THEN** the drone moves to the receiver and the paired buffer pad becomes visible

### Requirement: Buffer pad click hides pad

The system SHALL hide the buffer pad when the user clicks the pad while it is visible. Hiding means the pad no longer receives hits (inactive or non-raycast collider per implementation).

#### Scenario: Dismiss pad

- **WHEN** the user clicks a visible buffer pad
- **THEN** the buffer pad becomes hidden

### Requirement: Receiver re-activation after pad hidden

The system SHALL allow clicking the same receiver again after its pad was hidden to move the drone to the receiver and show the pad again.

#### Scenario: Toggle cycle via receiver

- **WHEN** the pad was hidden by a pad click and the user clicks the same receiver again
- **THEN** the drone moves to the receiver and the pad is shown again
