## 1. PlaneController — ramp and audio



- [x] 1.1 Add serialized fields: `boostRampDuration` (default 2f), acceleration `AudioClip`, optional `AudioSource` (or `GetComponent`/add child), and clarify tooltips that `boostSpeedMultiplier` is the peak reached after the ramp.

- [x] 1.2 Track planar movement activity using the same x/z deadzone logic as existing `isIdle` for horizontal strafe and forward/back; reset ramp progress when planar input idles; advance ramp in `FixedUpdate` with `Time.fixedDeltaTime`.

- [x] 1.3 Replace `effectiveMaxSpeed` calculation: remove `Input.GetMouseButton(1)`; use `Mathf.Lerp(maxSpeed, maxSpeed * boostSpeedMultiplier, ramp01)` when `_boostEnabled`, else cap at `maxSpeed`.

- [x] 1.4 Drive audio: when planar input active and ramp not complete (or while ramping—match spec: play during ramp window), start/keep playing; on planar idle call `Stop()`; ensure restart on next movement cycle.

- [x] 1.5 Add public query methods for tutorials (e.g. whether planar movement is active, whether ramp ≥ 1) and keep `SetBoostAllowed` semantics as “allow peak ramp” per design.



## 2. Tutorial and UX alignment



- [x] 2.1 Update `TutorialInputRestriction`: rename or document `allowBoost` as peak-ramp allowance; replace `IsBoosting()` with movement/ramp-based checks backed by `PlaneController` (or mirror state); remove reliance on `Input.GetMouseButton(1)` for pass conditions.

- [x] 2.2 Update `TutorialRing` `requiresBoost` path to use the new API (e.g. require full ramp or minimum speed) instead of `IsBoosting()` / RMB.

- [x] 2.3 Update `TutorialHud` and any user-facing strings that mention right-click boost to describe movement hold / acceleration.

- [x] 2.4 Search project for `GetMouseButton(1)`, “右键”, `IsBoosting`, and `requiresBoost`; fix any remaining references for consistency.



## 3. Content and verification



- [x] 3.1 Assign acceleration audio on the drone prefab or scene instance; set clip to loop if using a short bed, or author ~2s content per design notes.

- [ ] 3.2 Playtest: hold WASD — speed reaches old boost cap at 2s; release keys — audio stops; press again — ramp and audio restart; tutorial gates still work when boost disallowed.


