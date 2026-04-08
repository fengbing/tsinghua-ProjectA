## Context

`PlaneController` currently sets `effectiveMaxSpeed` to `maxSpeed * boostSpeedMultiplier` only while **mouse button 1** is held (`Input.GetMouseButton(1)`), with `_boostEnabled` gating tutorials. Movement toward that cap is handled by existing acceleration / Lerp-to-desired-velocity logic. Tutorial code (`TutorialInputRestriction`, `TutorialRing`, `TutorialHud`) assumes “boost” means **right mouse**.

## Goals / Non-Goals

**Goals:**

- Replace mouse-driven boost with a **time-based ramp** tied to **active movement intent** on the **Horizontal / Vertical input axes** (WASD / arrow keys as configured in Unity Input Manager)—i.e. the same `x` / `z` raw axes already read in `FixedUpdate`, outside deadzones.
- Reach the former “boost” top speed (`maxSpeed * boostSpeedMultiplier`) after **2 seconds** of continuous qualifying input (duration serialized, default 2f).
- Play a **configurable acceleration clip** on the drone (dedicated `AudioSource` recommended) for the ramp window: **start** when qualifying movement begins (or resumes after idle), **stop immediately** when qualifying movement ends; **restart** on the next press/hold cycle.
- Expose a small query API so tutorials can require “fully ramped” or “movement active” instead of RMB.

**Non-Goals:**

- Changing core hover physics, joint handling, or camera control beyond what is needed for the new speed cap and tutorial checks.
- New input bindings or a separate “sprint” key.
- **Space / LeftControl** vertical keys as triggers for the 2s ramp (unless product later decides otherwise; default keeps ramp aligned with planar “direction keys” only).

## Decisions

1. **Speed interpolation** — Maintain two reference speeds: `maxSpeed` (base) and `maxSpeed * boostSpeedMultiplier` (peak). While movement input is active, advance a normalized ramp value `t ∈ [0,1]` over `boostRampDuration` (default 2s) using **delta-time in `FixedUpdate`** for determinism with physics. `effectiveMaxSpeed = Mathf.Lerp(maxSpeed, peak, t)`. On idle, reset `t` to 0 (or decay quickly—**default: reset** so the next press replays the full 2s and audio, matching the user story).

2. **Audio** — Use `AudioSource.Play()` for a **looping** clip or a **one-shot** that is **stopped** via `Stop()` when input idles; if the clip is non-looping and shorter than 2s, either loop the source or document that designers should use a looping bed. Implementation should support **stop on idle** cleanly (no orphaned playback).

3. **Tutorial gating** — Repurpose `_allowBoost` / `SetBoostAllowed` to mean **“allow automatic ramp to peak speed”** (when false, cap stays at `maxSpeed`). `TutorialInputRestriction.IsBoosting()` becomes misleading: replace with something like `IsMovementInputActive()` and `IsAtFullAcceleration()` (or ask `PlaneController`), and update `TutorialRing` / `TutorialHud` copy from “右键加速” to movement-based wording.

4. **Right mouse** — No longer affects speed. Optionally ignore RMB entirely or leave unbound (document in tasks).

## Risks / Trade-offs

- **[Risk] Tutorial rings that required RMB** now need a clear rule (e.g. require `t >= 1` or speed ≥ threshold) → **Mitigation**: align `requiresBoost` semantics with “reached full ramp” and update HUD strings.
- **[Risk] Short taps never hear full SFX** → **Mitigation**: expected; audio stops with input—matches spec.
- **[Risk] Diagonal input magnitude** → **Mitigation**: ramp is keyed off the same `isIdle` test as hover logic (per-axis deadzone on x/z only for “qualifying movement” if we exclude vertical).

## Migration Plan

- Implement in `PlaneController`, assign clip on prefab/scene, playtest one tutorial scene.
- Rollback: revert `PlaneController` speed/audio block and restore RMB check; revert tutorial API changes.

## Open Questions

- Should **W/S forward/back** alone count without A/D? **Default yes** (Vertical axis is “direction” input).
- Should designers use a **looping** acceleration loop or a single **2s** authored clip? Left to content; code supports stop-on-idle either way.
