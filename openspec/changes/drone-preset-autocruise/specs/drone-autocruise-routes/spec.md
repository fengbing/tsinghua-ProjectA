## ADDED Requirements

### Requirement: Preset ordered route

The system SHALL allow level authors to define an **ordered sequence of waypoints** representing the cruise path. Each waypoint SHALL resolve to a **world position** at runtime (e.g. via `Transform` references). The drone SHALL visit waypoints **in list order** starting from the first index when a cruise begins.

#### Scenario: Author assigns waypoints in Editor

- **WHEN** a designer assigns one or more waypoint references on the route configuration
- **THEN** the runtime cruise SHALL use those positions in the configured order without requiring code changes

#### Scenario: Empty route is rejected

- **WHEN** a player attempts to start cruise with zero valid waypoints
- **THEN** the system SHALL not enter cruise mode and SHALL leave manual control unchanged

### Requirement: Start and stop with Y

The system SHALL start autocruise when the player presses the configured **Y** key (default) on `KeyDown` while cruise is inactive and start conditions are met. The system SHALL **cancel** an active cruise and restore manual control when the player presses the same key again on `KeyDown` while cruise is active. The key MUST be overridable in the Inspector.

#### Scenario: Y starts cruise

- **WHEN** cruise is inactive, the route is valid, and the player presses the start key
- **THEN** the drone SHALL enter autocruise and begin progressing toward the first waypoint

#### Scenario: Y cancels cruise

- **WHEN** cruise is active and the player presses the start key again
- **THEN** the cruise SHALL end immediately and manual flight input SHALL be re-enabled per integration rules

### Requirement: Progress along the path

While autocruise is active, the drone SHALL move toward the **current** waypoint. When the drone’s position is within a configured **arrival distance** of that waypoint, the system SHALL advance to the **next** waypoint. When the **last** waypoint is reached within arrival distance, the cruise SHALL **complete** and manual control SHALL be restored.

#### Scenario: Advance to next waypoint

- **WHEN** autocruise is active and the drone enters the arrival radius of the current waypoint before the last
- **THEN** the current target SHALL become the next waypoint in the ordered list

#### Scenario: Complete at final waypoint

- **WHEN** autocruise is active and the drone enters the arrival radius of the final waypoint
- **THEN** autocruise SHALL stop and manual control SHALL be restored

### Requirement: Manual control suspended during cruise

While autocruise is active, player-driven movement from `PlaneController` SHALL NOT apply (manual input disabled or equivalent). When autocruise completes or is cancelled, `PlaneController` manual input SHALL be restored to the state consistent with other game systems (e.g. tutorial gates still respected after re-enable).

#### Scenario: No manual steer during cruise

- **WHEN** autocruise is active and the player moves WASD or other flight axes
- **THEN** those inputs SHALL not move the drone under manual flight rules until cruise ends

### Requirement: Safe start policy with cargo

If the drone is **holding cargo** (per existing gripper/holding state), starting autocruise SHALL be **refused** until the cargo is released, unless a future explicit requirement overrides this. Refusal SHALL not toggle partial cruise state.

#### Scenario: Block cruise while holding

- **WHEN** the gripper reports holding and the player presses the cruise start key
- **THEN** the system SHALL remain out of cruise mode and manual control SHALL remain as before
