## ADDED Requirements

### Requirement: Active mission objectives appear as map markers
The system SHALL render markers for active mission objective targets on the map using each objective's world position transformed into map coordinates.

#### Scenario: Objective marker appears for active mission target
- **WHEN** a mission objective with a valid target position is active
- **THEN** the map displays a marker at the corresponding location

### Requirement: Objective markers stay synchronized across map views
The system SHALL keep objective marker state consistent between minimap and fullscreen map views, including active, completed, and hidden visibility states.

#### Scenario: Marker visibility updates when objective state changes
- **WHEN** an objective changes from active to completed or hidden
- **THEN** the marker visibility updates in both minimap and fullscreen views to reflect the new state
