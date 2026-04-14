## Why

The current drone flight stack has rich movement states (manual thrust, boost ramp, vertical climb/descent, yaw turning, and autocruise), but audio feedback is fragmented and does not consistently represent motor load. A unified EV-motor-based drone sound system is needed now to improve control feel, readability of flight state, and player immersion while reusing existing `ys.wav` source material.

## What Changes

- Add a runtime drone audio controller that maps rigidbody speed to base engine pitch/volume and keeps steady-state sound stable at cruise speed.
- Add thrust-reactive pitch boost logic for active input (W/A/S/D/Space/Ctrl), with fast attack and slower release smoothing to avoid abrupt clicks.
- Add vertical motion sound shaping: lift increases energy/presence, descent reduces brightness and loudness.
- Add turn-linked stereo panning driven by horizontal input with smoothing.
- Integrate the audio controller with existing flight systems (`PlaneController`, tutorial restrictions, and autocruise virtual input behavior) so audio remains coherent across manual and assisted modes.
- Define inspector-exposed tuning parameters for pitch range, smoothing windows, thrust boost amount, panning depth, and vertical behavior to support iterative sound design.

## Capabilities

### New Capabilities
- `drone-flight-audio-state-mixer`: Defines real-time audio state blending from speed, thrust input, and vertical intent with smooth transitions.
- `ev-source-to-drone-audio-profile`: Defines how EV motor source clips (including `ys.wav`) are analyzed/sliced/looped and exposed as drone-ready audio profiles.
- `drone-controller-audio-binding`: Defines integration points between drone control telemetry/input and the audio runtime controller across manual, tutorial-restricted, and autocruise states.

### Modified Capabilities
- None.

## Impact

- Affected code: `Assets/Scripts/Audio/` runtime driver/profile scripts and integration touchpoints in `PlaneController` / `DroneAutocruiseController`.
- Affected assets: EV source audio (`Assets/ys.wav`) plus drone loop/one-shot routing assets.
- Affected systems: flight feedback UX, tutorial-limited input behavior, autocruise state continuity.
- No external package dependency is required; uses Unity built-in `AudioSource`, optional `AudioMixer`, and existing project scripts.
