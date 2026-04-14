## Drone Sci-Fi Audio Validation Checklist

### Target maneuvers
- Accelerate: hold `W` and verify acceleration layer ramps in.
- Decelerate: release `W` or reverse and verify deceleration layer blends without pops.
- Cruise: maintain steady speed and verify base loop dominates.
- Turn: tap/hold `A` or `D` and verify sci-fi turn layer is clearly emphasized.
- Ascend: hold `Space` and verify ascend layer rises smoothly.
- Descend: hold `LeftControl` and verify descend layer is distinct from ascend.

### Tuning presets (initial)
- `accelerationThreshold`: 0.8
- `decelerationThreshold`: -0.8
- `turnRateThreshold`: 55 deg/s (keeps A/D quick-turn very responsive)
- `stateBlendSpeed`: 6.0
- `minStateHoldSeconds`: 0.08
- `sciFiPitchBoost`: 0.35
- `globalLimiter`: 0.92

### Known limitations
- Current implementation depends on assigning layer clips in `DroneSciFiAudioProfile`.
- `tsl audio` timeline defaults are preset values and should be refined with real waveform review.
- Final balance still requires in-scene listening pass for camera distance and environment reverb.
