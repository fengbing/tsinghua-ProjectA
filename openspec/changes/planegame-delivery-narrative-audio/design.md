## Context

PlaneGame already uses the bottom distance strip (`DistanceHudView` + `IDistanceHudSource`, commonly `DistanceToTargetSource` measuring from `measureFrom` to `target`). Drone carry/release is handled by `DroneGripper`; Level 2 exists at `Assets/Scenes/Level 2.unity` and appears in `EditorBuildSettings`. There is no centralized scene narrative controller for PlaneGame yet; `StorageNarrativeController` is a pattern elsewhere but not required to duplicate verbatim.

## Goals / Non-Goals

**Goals:**

- Start looping BGM when PlaneGame loads.
- Use the **same** distance-to-target signal the HUD uses (≤ 50 m) to stop BGM and fire a one-shot cue **once** per run (until an explicit reset policy—default: never repeat in-session after fired).
- Detect **successful package placement on the designated platform** and then play three voice clips **in order**, blocking overlapping playback.
- After the third voice completes, load **Level 2** via `SceneManager`.

**Non-Goals:**

- Changing how `DistanceHudView` draws the strip or its color mapping.
- Rewriting `DroneGripper` core grab logic (only optional thin hooks or separate trigger components).
- Full cinematic timeline tooling; a small `MonoBehaviour` + coroutines or `PlayOneShot` + clip-length wait is enough.

## Decisions

1. **Single director component on PlaneGame**  
   **Rationale**: Keeps audio state (BGM off, stinger played, outro started) in one place and avoids scattering `AudioSource` flags across systems.  
   **Alternatives**: Multiple scripts per concern—harder to enforce “play voices once” and ordering.

2. **Distance source: reference `IDistanceHudSource` or `DistanceToTargetSource` in inspector**  
   Poll `GetDistanceMeters()` each frame or on a short interval (e.g. `Update`). Threshold **50f** meters, **≤** triggers the proximity phase.  
   **Rationale**: Matches the user-visible HUD without duplicating `Transform` wiring; if the HUD object is the same GameObject, a `GetComponent`-style reference is trivial.  
   **Alternatives**: Duplicate `Vector3.Distance` with serialized transforms—risks drift if designers change only one side.

3. **Proximity one-shot semantics**  
   On first frame where distance ≤ 50 m: stop BGM (`AudioSource.Stop()` or mute loop source), play stinger **once**, set a bool `_proximityCuePlayed` so re-entering the band does not replay.  
   **Rationale**: Matches “只放一次”.  
   **Alternatives**: Replay when leaving and re-entering—user did not ask for this; document as optional tweak.

4. **Placement success detection**  
   Prefer a **dedicated trigger or placement zone** (e.g. `OnTriggerStay`/`OnCollisionStay` + tags/layers + “package released and resting on pad” checks) that raises a single C# event or calls `PlaneGameNarrativeDirector.NotifyDeliveryComplete()`.  
   If an existing pad/receiver exists (e.g. builtin minigame), **prefer reusing** its “success” callback instead of inventing parallel rules.  
   **Rationale**: “Success” must be unambiguous (box on platform, not merely flying nearby).

5. **Audio routing**  
   - BGM: one `AudioSource` with `loop = true`, `playOnAwake = false`, started from `Start`/`OnEnable`.  
   - Stinger + voice: second source(s) or same with `PlayOneShot` for stinger; voices sequential—either one source with `Play()` and wait `clip.length` in a coroutine, or three chained one-shots with explicit waits.  
   **Rationale**: Avoid voice stacking; simplest QA.

6. **Scene load**  
   Use `SceneManager.LoadScene` with **serialized scene name** `"Level 2"` (or build index) to match project naming; verify name matches file `Level 2.unity`.

## Risks / Trade-offs

- **[Risk] Double-firing delivery** if both trigger enter and release fire twice → **Mitigation**: `_outroStarted` guard on director; zone script calls director once.  
- **[Risk] Distance source null in bad prefab setup** → **Mitigation**: log clear warning once; never advance proximity state.  
- **[Risk] Voice + scene load if user reloads PlaneGame** → **Mitigation**: reset static/flags on `OnEnable` if hot-reload; no static state unless needed.

## Migration Plan

- Add script + wire references in PlaneGame; no data migration.  
- **Rollback**: disable director GameObject or revert scene diff.

## Open Questions

- Exact platform success rule (trigger volume only vs. velocity threshold vs. existing minigame receiver)—**resolve in implementation** by inspecting PlaneGame hierarchy for an existing success pad.
- Whether BGM should **resume** if designer wants leave/re-enter threshold (current spec: one-shot stinger, BGM stays off).
