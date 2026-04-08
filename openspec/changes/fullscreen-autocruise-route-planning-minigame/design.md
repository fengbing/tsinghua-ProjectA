## Context

The project already has drone autocruise functionality and route-related waypoint logic, but route selection is currently external to a dedicated in-game planning interaction.
This change introduces a fullscreen 2D minigame layer where users can construct an ordered route between a fixed start and end by selecting intermediate nodes.
The selected node sequence must remain aligned with scene waypoint references so the resulting route can be consumed by autocruise without manual remapping.

## Goals / Non-Goals

**Goals:**
- Provide a fullscreen route-planning interface that can be toggled in gameplay with `H`.
- Represent selectable 2D nodes as proxies for scene waypoint points.
- Support ordered click selection with immediate visual confirmation and line connections.
- Produce a deterministic ordered route payload that existing autocruise logic can execute.
- Keep route-planning state isolated and resettable without corrupting runtime flight state.

**Non-Goals:**
- Replacing the existing autocruise execution algorithm.
- Introducing freehand path drawing, path smoothing, or optimization heuristics.
- Building a generalized graph solver (shortest path / cost-aware planning) in this iteration.
- Supporting multiplayer or network-synchronized planning sessions.

## Decisions

1. Fullscreen overlay as a dedicated game state
- Decision: Implement the minigame as a fullscreen UI/gameplay overlay state entered/exited by `H`.
- Rationale: This avoids camera and world-control conflicts while giving clear focus to route planning.
- Alternative considered: in-world diegetic panel; rejected for higher integration complexity and lower interaction clarity.

2. Stable node-to-waypoint mapping
- Decision: Each UI node has a unique ID mapped to exactly one scene waypoint reference.
- Rationale: Ensures deterministic route output and simplifies validation before route commit.
- Alternative considered: runtime nearest-waypoint inference from UI positions; rejected due to ambiguity and brittle behavior.

3. Incremental selection pipeline
- Decision: Maintain an ordered `selectedNodes` list; each click appends if valid, updates node color, and draws one new segment.
- Rationale: Aligns exactly with user mental model ("choose next stop") and keeps implementation testable.
- Alternative considered: selecting entire predefined route templates; rejected because user requested manual path choice.

4. Segment rendering from start through end
- Decision: Render line segments as:
  - start -> first selected
  - selected[i] -> selected[i+1]
  - last selected -> end (only when route is finalized)
- Rationale: Makes planning progress explicit and guarantees route continuity.
- Alternative considered: only render segments between selected nodes; rejected because it obscures start/end linkage.

5. Explicit commit/cancel behavior
- Decision: Route is only exported to autocruise when user confirms (commit action), and can be canceled/reset cleanly.
- Rationale: Prevents partial route states from leaking into flight control.
- Alternative considered: auto-commit on every click; rejected due to accidental updates and hard rollback.

## Risks / Trade-offs

- [Risk] Input conflicts between planning controls and normal flight input -> Mitigation: gate flight controls while minigame overlay is active.
- [Risk] Broken waypoint references after scene edits -> Mitigation: add validation on open/commit and clear error state for missing nodes.
- [Risk] Visual clutter with many nodes and segments -> Mitigation: define visual states (idle/selected/invalid) and cap line thickness/opacity.
- [Risk] Users create non-useful routes (loops or sparse picks) -> Mitigation: enforce minimal validity rules before commit (start/end continuity, minimum intermediate count if required by scenario).
- [Trade-off] Manual selection maximizes control but is slower than presets -> Accept for this feature because authoring clarity is prioritized.
