## 1. Audio Runtime Foundation

- [x] 1.1 Create `DroneAudioController.cs` under `Assets/Scripts/Audio/` and bind required references (`AudioSource`, `Rigidbody`, optional `PlaneController`, optional `AudioMixer`).
- [x] 1.2 Implement speed-normalized base mapping (`velocity.magnitude / maxSpeed`) to pitch and volume ranges.
- [x] 1.3 Implement smoothed parameter transitions using `Mathf.SmoothDamp`/`Lerp` with configurable attack/release timings.

## 2. Thrust and Vertical State Logic

- [x] 2.1 Implement pushing-intent detection from `Horizontal`/`Vertical` axes plus `Space`/`LeftControl` keys.
- [x] 2.2 Add thrust boost overlay (+pitch bias) with fast engage and slower release behavior.
- [x] 2.3 Add lift behavior (Space) with presence/volume enhancement and configurable mixer parameter drive.
- [x] 2.4 Add drop behavior (LeftControl) with volume reduction and high-frequency cutoff reduction.

## 3. Turning Spatial Cue and Autocruise Compatibility

- [x] 3.1 Implement horizontal-input-to-`panStereo` mapping with smoothing and configurable pan depth.
- [x] 3.2 Add compatibility path for autocruise and input-locked states so audio follows telemetry and virtual movement intent.
- [x] 3.3 Ensure transitions across planner/tutorial/narrative lock states decay overlays smoothly without pops.

## 4. EV Source Profile and Asset Wiring

- [x] 4.1 Define a profile asset/data container for EV-derived drone audio parameters and default mappings.
- [x] 4.2 Onboard `Assets/ys.wav` into the runtime profile and configure loop-ready playback settings.
- [x] 4.3 Implement safe fallback when optional mixer parameters are absent or unset.

## 5. Verification and Tuning

- [ ] 5.1 Validate flight-state behavior in manual mode: accelerate, decelerate, cruise, turn, lift, and descent.
- [ ] 5.2 Validate behavior in autocruise mode and confirm no unintended idle/mute collapse.
- [ ] 5.3 Validate behavior with tutorial/planner input restrictions and confirm smooth recovery.
- [x] 5.4 Tune default inspector/profile values to achieve stable EV-like drone motor character without clipping or abrupt jumps.
