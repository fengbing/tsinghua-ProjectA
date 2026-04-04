## 1. Scene controller and audio wiring

- [x] 1.1 Add a `MonoBehaviour` director (e.g. `PlaneGameNarrativeDirector`) with serialized clips: BGM loop, proximity stinger, three voice lines; two `AudioSource` fields (BGM + one-shots/voice) or documented single-source strategy.
- [x] 1.2 On `Start`/`OnEnable`, start looping BGM; expose optional volume mixer refs if the project uses them.
- [x] 1.3 Serialize a reference to `IDistanceHudSource` (or concrete `DistanceToTargetSource`) used by `DistanceHudStrip` in PlaneGame; poll `GetDistanceMeters()` and implement ≤ 50 m transition with `_proximityCuePlayed` guard.

## 2. Proximity behavior

- [x] 2.1 On first threshold crossing: stop BGM source, play stinger exactly once; log once if distance source is missing.
- [x] 2.2 Confirm no replay while staying inside 50 m (and document in inspector whether leaving/re-entering resets—default off).

## 3. Delivery detection

- [x] 3.1 Inspect PlaneGame for an existing pad/receiver; if present, subscribe or invoke director from its success path.
- [x] 3.2 If none, add a small trigger (or reuse collider) on the platform: detect grabbable/package + “released on pad” (e.g. not parented to drone, velocity below threshold) and call `NotifyDeliveryComplete()` once guarded by `_outroStarted`.

## 4. Voice sequence and scene transition

- [x] 4.1 Implement coroutine (or equivalent) that plays voice 1 → wait duration → voice 2 → wait → voice 3 → wait.
- [x] 4.2 After third clip ends, call `SceneManager.LoadScene` with `"Level 2"` (or verified build index); ensure Level 2 is in Build Settings.

## 5. PlaneGame scene integration

- [x] 5.1 Instantiate/wire director GameObject in `PlaneGame.unity`: assign sources, clips, distance source, platform trigger or success hook.
- [x] 5.2 Play Mode test: BGM on enter → crossing 50 m stops BGM + one stinger → deliver on platform → three lines → Level 2 loads.
