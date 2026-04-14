## Why

The high-rise window mission already establishes fire suppression and narrative beats, but it does not yet give players a structured, in-world moment to practice safe evacuation choices (elevator versus slide) across multiple trapped occupants. A dedicated fullscreen facade rescue minigame closes that loop with clear sequencing, audio feedback, and a repeatable trigger aligned with mission completion.

## What Changes

- Add a fullscreen 2D facade rescue minigame that mirrors the integration style of the existing built-in route-planning minigame (overlay UI, deterministic state machine, mission-owned trigger).
- Gate entry on a single compound condition: window fire extinguished, two configured post-extinguish audio clips finished, and the drone overlapping a dedicated `minigame trigger` volume.
- Present a building facade background image with three window hotspots; each window maps to one trapped person and one rescue decision.
- On start, animate an elevator graphic from the top of the facade down to the first window’s floor height; player interacts only with the active window in order.
- Per window: tap window → overlay with an informational image and an action image-button → modal with trapped-person details → button reveals a bottom choice bar with three options (left slide, center elevator, right slide). Choosing slide plays a slide sound effect; any valid choice dismisses overlays and the bottom bar.
- After a rescue, show the rescued person’s portrait on the elevator graphic, then auto-advance the elevator to the next window height; repeat until three people are rescued.
- After the third rescue, continue animating the elevator downward until it leaves the visible facade, then play a final system prompt (voice or UI-backed audio per existing dialog patterns).

## Capabilities

### New Capabilities

- `facade-rescue-minigame`: Fullscreen facade-based rescue interaction covering triggers, sequential window flow, elevator motion, choice UI, audio cues, and completion handoff.

### Modified Capabilities

- None.

## Impact

- Affected systems: mission/window-fire flow wiring, fullscreen UI canvas or equivalent overlay, collider trigger for drone proximity, audio clip sequencing, and optional integration with existing system dialog or one-shot audio helpers if reused.
- Scene and asset dependencies: facade background art, per-window art, elevator composite art, three person profiles, slide SFX, post-minigame system prompt audio, and serialized references for the two prerequisite audio clips.
- Player input and drone control may need the same style of gating used by other fullscreen minigames to avoid conflicting controls while the overlay is active.
