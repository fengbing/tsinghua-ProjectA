## 1. Source Audio Analysis and Asset Preparation

- [x] 1.1 Audit `tsl audio` format (sample rate, channels, length) and document import baseline.
- [x] 1.2 Mark acceleration, cruising, and deceleration regions from `tsl audio` with timestamps.
- [x] 1.3 Export reusable clips for base loop, transient acceleration/deceleration, and maneuver coloration.
- [ ] 1.4 Configure Unity import settings and loop points for all derived clips.
- [x] 1.5 Create metadata/config asset for each clip (role, gain range, pitch range).

## 2. Runtime Mixer and State Detection

- [x] 2.1 Implement telemetry-driven drone audio mixer component with layered `AudioSource` control.
- [x] 2.2 Implement six-behavior state detection (accelerate, decelerate, cruise, turn, ascend, descend).
- [x] 2.3 Add hysteresis, minimum hold time, and smoothing to prevent rapid state flapping.
- [x] 2.4 Implement blend rules for concurrent behaviors (for example turn + accelerate).
- [x] 2.5 Add global limiter/ducking safeguards to avoid loudness stacking artifacts.

## 3. Drone Controller Integration

- [x] 3.1 Define standardized telemetry interface (`forwardSpeed`, `acceleration`, `verticalSpeed`, `turnRate`, `throttle`).
- [x] 3.2 Bridge current drone controller outputs into telemetry interface at stable update timing.
- [x] 3.3 Add safe fallback behavior for missing telemetry values (last valid/default state).
- [x] 3.4 Expose mixer thresholds, gains, and smoothing in inspector or config assets for tuning.
- [x] 3.5 Add integration enable toggle to allow instant rollback to baseline audio mode.

## 4. Validation and Tuning

- [x] 4.1 Build a test checklist for six target maneuvers in representative scenes.
- [ ] 4.2 Verify each maneuver triggers the intended layer response and transition smoothness.
- [ ] 4.3 Tune thresholds and blend ranges to match drone motion feel without audible pops.
- [ ] 4.4 Validate performance impact and confirm no runtime errors in play mode logs.
- [x] 4.5 Document final tuning presets and known limitations for future audio iteration.
