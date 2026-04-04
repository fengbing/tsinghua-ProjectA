## 1. Minimap API for scripted toggle

- [x] 1.1 Add a public method on `MinimapUiController` (e.g. `PerformToggle()`) that executes the same logic as one `Input.GetKeyDown(toggleKey)` press: flip `MapViewMode` and call `ApplyMode()`.
- [x] 1.2 In Play Mode, verify manual **M** and scripted call produce identical visible result for one invocation.

## 2. Narrative coordinator script

- [x] 2.1 Create `StorageNarrativeController` (or agreed name) with serialized references: panel roots or Images for loading 1, story, loading 2; optional `AudioSource`; clip fields for each phase (1 + 1 + 2 + 2 clips); flags/gates for one-shot playback per session.
- [x] 2.2 Implement coroutines or a small queue so dual-audio phases play **sequentially** without overlap (second clip starts after first ends unless intentionally overlapped—default sequential).
- [x] 2.3 On sequence completion, hide narrative panels and re-enable `PlaneController` (or project input gate) if it was disabled at scene start.

## 3. Storage scene wiring

- [x] 3.1 In `storage` scene, place Canvas / full-screen **Image** hierarchy for the three narrative phases; assign sprites and hook references to the coordinator.
- [x] 3.2 Assign all audio clips in Inspector; confirm non-looping import settings where appropriate.
- [x] 3.3 Enter Play From Editor on storage scene: verify order loading → story → loading → gameplay; verify each audio fires once per run as specified.

## 4. Pickup code success: audio + minimap

- [x] 4.1 Subscribe to `DecryptPuzzleUI.OnDecryptPuzzleSolved` from the coordinator or a small helper; on invoke, play the success clip once per event.
- [x] 4.2 After success audio starts (or after it ends—pick one and document in code comments if product cares), call `MinimapUiController.PerformToggle()` exactly once per success.
- [x] 4.3 Test full flow: complete narrative → enter correct 取件码 → hear success audio → map view changes match one **M** press.

## 5. Polish and QA

- [x] 5.1 Confirm no duplicate event subscriptions on scene reload; unsubscribe in `OnDestroy`.
- [x] 5.2 Document optional skip (if implemented) in Inspector tooltips; if not implemented, leave note in `design.md` Open Questions for future work.
