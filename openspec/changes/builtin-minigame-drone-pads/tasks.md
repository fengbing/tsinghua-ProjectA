## 1. Scene and content setup



- [x] 1.1 Add gameplay root under `BuiltinMiniGame`: background sprite/quad, orthographic camera references (or reuse Main Camera), sorting layers optional.

- [x] 1.2 Add a full-screen playfield `Collider2D` (or equivalent pick surface) behind receivers/drone so empty-map clicks resolve; set Z/layer consistent with `ScreenToWorldPoint`.

- [x] 1.3 Wire art assets: background texture, drone sprite, receiver sprite, buffer-pad sprite, cursor texture, click SFX — assign placeholders from `Assets/` where available.



## 2. View — pan and zoom



- [x] 2.1 Implement `MiniGameViewController` (or named per design): scroll wheel adjusts `Camera.orthographicSize` with configurable min/max and clamping.

- [x] 2.2 Implement mouse-position-driven pan on the camera (or tracked root) per `design.md`; skip pan/zoom when pointer is over blocking UI if using `EventSystem` checks.

- [x] 2.3 Validate background stays framed sensibly at min/max zoom (adjust bounds or scale if clipping).



## 3. Cursor and click routing



- [x] 3.1 Implement custom cursor via `Cursor.SetCursor` with configurable hotspot; add software-cursor fallback sprite following mouse if hardware path fails or art is oversized.

- [x] 3.2 Centralize playfield hit testing: distinguish UI (return panel) vs world colliders; expose helper for “world point under pointer” used by drone and pair scripts.



## 4. Drone movement and click audio



- [x] 4.1 Implement `MiniGameDrone` (or equivalent): on valid playfield click, set target world XY; move with configurable lerp/smooth damp or snap.

- [x] 4.2 Hook one-shot click audio (`PlayOneShot`) on the same code path as playfield targeting; no sound for pure UI clicks.



## 5. Receivers and buffer pads



- [x] 5.1 Implement `MiniGameReceiver` (or pair host): reference pad object, authored relative offset or child transform; init positions in `Start`.

- [x] 5.2 On receiver click: invoke drone move to receiver position; show paired pad (`SetActive` true or enable render/collider).

- [x] 5.3 Implement `MiniGameBufferPad`: on click while visible, hide pad and disable hit; ensure receiver click can show pad again after hide.

- [x] 5.4 Ensure all receivers share one receiver sprite reference and all pads share one pad sprite reference (prefab overrides or shared materials as appropriate).



## 6. Editor polish and verification



- [x] 6.1 Optionally extend `BuiltinMiniGameSceneSetup` to scaffold colliders/scripts references for faster regeneration (keep non-destructive if scene hand-authored).

- [x] 6.2 Play test: additive load from existing `MiniGame` flow, pan/zoom, drone to map/receiver/pad, audio fires, return UI still works.

- [x] 6.3 Fix compile/console errors; confirm specs in `specs/*/spec.md` scenarios pass manually in Editor.

