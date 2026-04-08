## ADDED Requirements

### Requirement: Fullscreen planning mode toggle
The system MUST enter and exit a fullscreen 2D route-planning mode when the user presses `H`, and while active it MUST prioritize minigame input over normal flight control input.

#### Scenario: Enter planning mode with hotkey
- **WHEN** the user presses `H` during normal gameplay
- **THEN** the system enters fullscreen route-planning mode and displays the planning UI

#### Scenario: Exit planning mode with hotkey
- **WHEN** the planning mode is active and the user presses `H` again
- **THEN** the system exits planning mode and returns control to normal gameplay input

### Requirement: Node-to-waypoint mapping
Each selectable 2D node in the planning UI MUST map to exactly one scene waypoint reference used by drone autocruise route execution.

#### Scenario: Resolve waypoint from selected node
- **WHEN** the user selects a route node in planning mode
- **THEN** the system resolves and stores the mapped scene waypoint for that node in route order

#### Scenario: Reject invalid node mapping
- **WHEN** a node has no valid waypoint mapping at planning time
- **THEN** the system prevents that node from being committed into the route and surfaces a validation error state

### Requirement: Ordered node selection feedback
The system MUST append valid clicked nodes in selection order, visually mark selected nodes with a distinct color/state, and preserve order for route output.

#### Scenario: First node becomes selected
- **WHEN** the user clicks an unselected valid node as the first selection
- **THEN** the node changes to selected visual state and is stored as the first intermediate waypoint

#### Scenario: Subsequent node appends in order
- **WHEN** the user clicks another valid unselected node after at least one selection
- **THEN** the node is appended after the previous selected node in the route sequence

### Requirement: Segment rendering across route chain
The planning UI MUST draw route segments to represent continuity from start through selected nodes and finally to end when route completion conditions are met.

#### Scenario: First segment connects from start
- **WHEN** the user selects the first valid node
- **THEN** the system draws a segment from the start node to that first selected node

#### Scenario: Intermediate segment connects consecutive nodes
- **WHEN** the user selects a new node after previous selections
- **THEN** the system draws a segment from the previously selected node to the newly selected node

#### Scenario: Final segment reaches end
- **WHEN** the selected route is finalized as valid
- **THEN** the system draws the final segment from the last selected node to the end node

### Requirement: Route commit for autocruise
The system MUST export the finalized ordered waypoint sequence as an autocruise-consumable route only on explicit user confirmation.

#### Scenario: Commit valid planned route
- **WHEN** the user confirms planning with a valid start-to-end chain
- **THEN** the system publishes the ordered waypoint route to the drone autocruise subsystem

#### Scenario: Prevent commit on incomplete route
- **WHEN** the user attempts to confirm planning with an invalid or incomplete chain
- **THEN** the system rejects the commit and keeps the current planning state for correction
