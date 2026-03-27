## Minimap Verification Checklist

- [ ] Assign `MinimapConfig` asset with custom map sprite and world bounds.
- [ ] Add `MinimapUiController` to scene and bind `playerTransform`.
- [ ] Confirm top-left minimap is visible and updates player icon while moving.
- [ ] Confirm minimap is circular by default (mask/frame).
- [ ] Add mission targets in `MissionObjectiveProvider` and verify markers appear.
- [ ] Change objective state to `Completed` and verify marker color change.
- [ ] Change objective state to `Hidden` and verify marker disappears in both views.
- [ ] Press `M` to toggle fullscreen map and verify toggle is reversible.
- [ ] Verify player and objective marker positions remain consistent after toggling.
- [ ] Validate map bounds using `MapBoundsGizmo` in Scene view.
