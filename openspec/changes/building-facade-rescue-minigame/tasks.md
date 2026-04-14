## 1. Mission entry gate and trigger

- [x] 1.1 Track window-fire extinguished plus completion of two serialized post-extinguish `AudioClip` playbacks (events or coroutine waits) in the mission flow that owns this minigame.
- [x] 1.2 Add a dedicated trigger collider (minigame trigger volume) and detect drone root or tagged rigidbody overlap; combine with 1.1 into a single “may open” latch to avoid double opens.
- [x] 1.3 Invoke the facade rescue minigame `Open()` entry point only when the latch conditions are satisfied; reset latch appropriately on scene reload or mission reset.

## 2. Fullscreen shell and input gating

- [x] 2.1 Create a fullscreen UI root (Canvas/prefab) with facade background image and inactive-by-default container matching the route-planning overlay pattern.
- [x] 2.2 Add `FacadeRescueMiniGameController` (final name as implemented) with `Open`/`Close` (or equivalent) and a static `IsFacadeRescueOpen` flag consumed by `GameUi` or related input routers.
- [x] 2.3 While the minigame is open, disable or ignore drone movement inputs consistent with `RoutePlanningMiniGameController` behavior; restore on close.

## 3. State machine and window sequencing

- [x] 3.1 Implement enum-based states for Idle, elevator approach, per-window panels, choice bar, resolve, advance, final exit, and complete as described in `design.md`.
- [x] 3.2 Serialize three window definitions (click targets, floor heights or `RectTransform` stops, person profile sprites, optional strings for UI binding).
- [x] 3.3 Enforce single active window index and ignore clicks on inactive windows.

## 4. UI panels and choice bar

- [x] 4.1 Wire window click to show informational image + action image-button overlay for the active window only.
- [x] 4.2 On action button, show trapped-person details panel with a control that reveals the bottom three-button bar (left slide, center elevator, right slide).
- [x] 4.3 On any choice, hide details panel and bottom bar; if slide, play configured slide one-shot; advance state to “attach portrait and move elevator.”

## 5. Elevator motion and portraits

- [x] 5.1 Implement tweened vertical motion of the elevator `RectTransform` between serialized stops (top → window1 → window2 → window3 → off-screen).
- [x] 5.2 Add stacked portrait slots on the elevator; fill next free slot after each rescue with the current person’s sprite.
- [x] 5.3 After third rescue, animate elevator off the visible facade then transition to completion state.

## 6. Audio and completion handoff

- [x] 6.1 Serialize and play the final system prompt `AudioClip` (or call existing system dialog audio API) when the post-third exit animation completes.
- [x] 6.2 Close fullscreen root, clear static flag, re-enable drone control, and notify mission code (event or callback) that the rescue minigame finished successfully.

## 7. Scene integration and verification

- [x] 7.1 Place and wire all references in the target gameplay scene (facade art, elevator art, windows, clips, trigger, drone reference if needed).
- [ ] 7.2 Manual playtest checklist: extinguish → both clips → trigger enter → full three-person flow → elevator exit → system prompt → control return.
