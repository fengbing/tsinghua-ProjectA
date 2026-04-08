## 1. Minigame Mode and UI Shell

- [x] 1.1 Add a fullscreen route-planning minigame state that toggles on/off with `H`.
- [x] 1.2 Gate normal flight/drone control input while minigame mode is active.
- [x] 1.3 Build the fullscreen 2D planning UI root with start node, end node, and selectable intermediate nodes.

## 2. Node Mapping and Selection Logic

- [x] 2.1 Define data model for UI node ID -> scene waypoint reference mapping.
- [x] 2.2 Implement click handling for selectable nodes and maintain ordered `selectedNodes` state.
- [x] 2.3 Add node visual-state updates (idle, selected, invalid/missing mapping).
- [x] 2.4 Add validation for unmapped or invalid nodes and prevent invalid selections from route commit.

## 3. Route Line Rendering

- [x] 3.1 Implement rendering for segment `start -> first selected` after first valid click.
- [x] 3.2 Implement rendering for each incremental segment `selected[i] -> selected[i+1]`.
- [x] 3.3 Implement final segment `last selected -> end` when route is finalized.
- [x] 3.4 Add clear/reset handling to rebuild line visuals correctly after cancel or reselection.

## 4. Autocruise Integration and Verification

- [x] 4.1 Implement explicit confirm action that exports ordered waypoint sequence to the autocruise subsystem.
- [x] 4.2 Block confirm for incomplete/invalid chains and present corrective feedback.
- [x] 4.3 Integrate committed route payload with existing drone autocruise route entry points.
- [x] 4.4 Add test checklist (manual or automated) covering hotkey toggle, mapping validity, selection order, line continuity, and commit behavior.
