## 1. Map Configuration and Data Flow

- [x] 1.1 Create a map configuration asset for custom map image assignment and world-bounds-to-map coordinate mapping.
- [x] 1.2 Implement a shared map state/controller that provides normalized player/objective positions to both minimap and fullscreen views.
- [x] 1.3 Add validation/debug support to verify coordinate calibration against level bounds.

## 2. Minimap UI Implementation

- [x] 2.1 Create top-left minimap HUD prefab with circular frame/mask and bind it to the custom map image.
- [x] 2.2 Implement real-time player indicator updates on the minimap using the shared map controller.
- [x] 2.3 Add marker rendering for active objective targets with clear visual distinction from the player icon.

## 3. Fullscreen Map and Toggle Behavior

- [x] 3.1 Build fullscreen map panel/canvas that reuses map image, player indicator, and objective markers from shared state.
- [x] 3.2 Implement `M` key input handling with explicit mode transitions between minimap and fullscreen.
- [x] 3.3 Ensure toggling preserves marker/player context and does not desynchronize UI visibility state.

## 4. Objective State Sync and Quality Verification

- [x] 4.1 Integrate mission objective lifecycle events so markers update for active/completed/hidden states in both views.
- [x] 4.2 Add/adjust play mode tests or manual verification checklist for real-time tracking, marker sync, and `M` toggle transitions.
- [x] 4.3 Tune icon scale, marker readability, and fullscreen interaction behavior to avoid conflicts with existing HUD overlays.
