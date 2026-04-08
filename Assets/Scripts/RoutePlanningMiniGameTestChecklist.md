# Route Planning Minigame Test Checklist

- [ ] Press `H` during gameplay: fullscreen planner opens and flight input is disabled.
- [ ] Press `H` again: planner closes and previous flight input state is restored.
- [ ] Click a mapped node: node color changes to selected and route ordering appends correctly.
- [ ] First selected node draws segment from start marker.
- [ ] Subsequent selections draw segment from previous selected node.
- [ ] Click an unmapped node: node shows invalid color and route cannot be committed.
- [ ] Click confirm with no selected nodes: commit is blocked with feedback.
- [ ] Click confirm with valid nodes: final segment reaches end marker and route is submitted.
- [ ] After successful confirm, trigger autocruise: drone follows committed start->selected->end waypoint chain.
- [ ] Click cancel/reset: selected state and segments are cleared for reselection.
