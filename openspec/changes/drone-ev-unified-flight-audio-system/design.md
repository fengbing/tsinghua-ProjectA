## Context

Current drone movement is driven by `PlaneController` physics and supports manual thrust, vertical control, boost ramp, and autocruise virtual input. Audio behavior is partially covered by acceleration clips but does not model continuous EV-like motor load across full flight states. The project now includes EV source material (`ys.wav`) and requires a deterministic audio pipeline that ties playback to real speed, control intent, and vertical maneuvers without introducing abrupt transitions or clicks.

## Goals / Non-Goals

**Goals:**
- Deliver one unified runtime audio controller that maps flight telemetry/input into stable motor-like drone sound output.
- Guarantee smooth transitions for pitch, volume, pan, and filter values via damped interpolation (`Mathf.SmoothDamp`/`Lerp`).
- Preserve stable cruise timbre at high speed while still expressing active thrust intent (input held) through transient pitch bias.
- Support distinct lift and drop signatures with mixer/filter automation.
- Keep integration compatible with manual control, tutorial restrictions, and autocruise.

**Non-Goals:**
- Building a procedural synthesis engine from scratch.
- Reworking existing flight physics or input bindings.
- Final mastering/mix decisions for all scenes (only provide tunable runtime controls and defaults).

## Decisions

1. **Use a dedicated `DroneAudioController` runtime component under `Assets/Scripts/Audio/`.**  
   - Rationale: isolates audio concerns from movement physics, reduces regression risk in `PlaneController`, and enables independent tuning.  
   - Alternative considered: embedding audio math directly in `PlaneController`; rejected due to coupling and maintainability cost.

2. **Use speed-normalized base mapping for pitch and volume.**  
   - Decision: compute `speed01 = Clamp01(rb.velocity.magnitude / maxSpeed)` and map to base pitch/volume ranges.
   - Rationale: ties continuous motor bed to actual physical velocity and keeps stable steady-state at cruise.
   - Alternative considered: map directly from input magnitude; rejected because it fails when velocity and input diverge (coast/autocruise/drag states).

3. **Apply dual-layer response: physical base + intent boost.**  
   - Decision: add an intent-driven pitch offset when any thrust key is active (`W/A/S/D/Space/LeftCtrl`), with faster attack and slower release.
   - Rationale: restores motor load feedback during constant-speed pushing and prevents "soft" control feel.
   - Alternative considered: one single pitch curve only from speed; rejected due to poor responsiveness while holding thrust at near-constant speed.

4. **Implement vertical character via mixer/filter controls.**  
   - Decision: lift (`Space`) raises presence (mid/high EQ gain and optional volume bonus); drop (`LeftCtrl`) lowers volume and high-pass cutoff to darken timbre.
   - Rationale: vertical thrust has distinct perceived energy profile and must be audible beyond pitch-only changes.
   - Alternative considered: separate one-shot clips only; rejected because one-shots alone do not provide continuous state-following behavior.

5. **Drive turn feel with smoothed stereo pan from horizontal axis.**  
   - Decision: `panTarget = horizontal * panDepth` (default `0.2`) and smooth to avoid abrupt L/R hops.
   - Rationale: provides clear lateral cue during A/D turns with minimal implementation cost.
   - Alternative considered: full 3D spatial emitter rig; rejected for complexity and uncertain gameplay value.

6. **Integrate via shared telemetry contract rather than direct key polling only.**  
   - Decision: support both raw input and `PlaneController` telemetry (`HorizontalInputRaw`, `Throttle01`, `ForwardSpeed`, etc.) so manual and autocruise remain coherent.
   - Rationale: autocruise uses virtual axes and should still drive correct audio behavior.
   - Alternative considered: input polling only; rejected because it under-represents non-manual states.

## Risks / Trade-offs

- **[Risk] Input and telemetry disagreement in edge states (UI lock, cutscene, planner).** → Mitigation: define clear priority rules (input disabled => treat intent as zero unless autocruise active).
- **[Risk] Over-aggressive smoothing causes sluggish audio feedback.** → Mitigation: expose attack/release times in inspector with conservative defaults.
- **[Risk] Mixer parameter names differ per scene/mixer asset.** → Mitigation: make parameter names configurable and fail-safe when missing.
- **[Risk] EV source loop quality issues (clicks/seam artifacts).** → Mitigation: create profile asset with loop region metadata and optional crossfade settings.
- **[Trade-off] More inspector knobs improve tuning but increase setup complexity.** → Mitigation: provide tested default presets and documentation in tasks.

## Migration Plan

1. Add `DroneAudioController` and profile assets without removing existing clips.
2. Route drone loop source (`ys.wav` derived loop) through new controller on the drone object.
3. Wire optional `AudioMixer` parameters for lift/drop shaping.
4. Validate behavior in manual flight, tutorial restrictions, and autocruise route mode.
5. Disable or deprecate overlapping legacy acceleration-loop logic only after parity verification.
6. Rollback strategy: disable `DroneAudioController` component and re-enable previous audio routing.

## Open Questions

- Should descent darkening be driven only by `LeftCtrl` intent, or also by sustained negative vertical velocity?
- Should turning pan be based on horizontal input, yaw rate (`TurnRate`), or a weighted blend?
- Do we need separate profile presets for light payload vs heavy payload flight feel?
