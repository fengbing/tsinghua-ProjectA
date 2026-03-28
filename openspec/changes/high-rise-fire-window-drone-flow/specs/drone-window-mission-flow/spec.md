## ADDED Requirements

### Requirement: Proximity activates the first interaction prompt and narration
The system SHALL detect when the player drone enters a configured proximity region near a mission window. Upon first activation for that window’s mission, the system SHALL display a two-part HUD: one part showing the `F` key and the other showing the text “快打开喷淋系统灭火！”, and SHALL play the first narrator audio clip for this phase.

#### Scenario: Approaching the window shows sprinkler prompt
- **WHEN** the drone enters the window’s proximity trigger while the mission is idle and the window is still burning
- **THEN** the dual-box HUD appears with `F` and “快打开喷淋系统灭火！” and the first narrator clip begins playback

### Requirement: First F key triggers water effect and completes extinguish phase
The system SHALL accept the `F` key as the confirm action while the first prompt is active. When the player presses `F`, the system SHALL show a water or sprinkler spray effect at the window, complete the extinguish behavior for fire (per `building-window-fire-vfx`), play the follow-up narrator clip, and transition the HUD to the second prompt.

#### Scenario: F after sprinkler prompt
- **WHEN** the first prompt is shown and the player presses `F`
- **THEN** a water/sprinkler visual plays, flame at the window is suppressed, the second narrator clip plays, and the HUD updates to show `F` and “快启用双光热成像相机！”

### Requirement: Second F key loads the thermal imaging scene
The system SHALL accept the `F` key while the second prompt is active. When the player presses `F`, the system SHALL load the configured destination scene using Unity’s scene loading API so gameplay continues in the thermal-imaging scenario.

#### Scenario: F after thermal prompt loads scene
- **WHEN** the second prompt is shown and the player presses `F`
- **THEN** the application loads the designated next scene (as configured for that window mission)

### Requirement: Input is ignored outside the correct phase
The system SHALL ignore `F` presses that do not apply to the current mission phase (e.g. before the first prompt is shown, or after the second `F` has already triggered loading).

#### Scenario: No premature F
- **WHEN** the drone has not yet entered the proximity region
- **THEN** pressing `F` does not start the sprinkler effect, suppress fire, or load the next scene

#### Scenario: No double load
- **WHEN** the second `F` has already initiated scene loading
- **THEN** additional `F` presses do not trigger a second load or duplicate transitions
