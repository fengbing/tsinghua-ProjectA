## Why

The training scenario needs a believable high-rise fire presentation and a guided drone mission: players must notice burning windows, receive clear voice and on-screen prompts, extinguish fire with a sprinkler interaction, then proceed to a thermal-imaging phase in another scene. Today this end-to-end flow is not specified or wired in the project.

## What Changes

- Place smoke and flame visual effects on designated windows of the existing high-rise facade (data-driven or prefab-based window anchors).
- Use the player drone as the focal actor: when it enters a proximity volume near a target window, show a two-box HUD (key hint `F` + instruction text in Chinese).
- Play narrator audio when each prompt phase becomes active; avoid overlapping narration with unclear state.
- On first `F`: show water/sprinkler spray effect, stop fire (and optionally smoke) at that window, play follow-up narration, then show the second prompt pair (`F` + “快启用双光热成像相机！”).
- On second `F`: load a separate Unity scene (target scene configurable in editor or data).
- Input uses the standard keyboard `F` (project may map to Input System or legacy input consistently with existing drone controls).

## Capabilities

### New Capabilities

- `building-window-fire-vfx`: Placement and lifecycle of smoke and flame effects on high-rise window anchors; tuning hooks for intensity and cleanup when fire is suppressed.
- `drone-window-mission-flow`: Proximity detection from drone to window, dual-box interaction UI, phased narrator clips, first-`F` sprinkler/water VFX and fire shutdown, second-`F` scene transition to the thermal-imaging level.

### Modified Capabilities

- None.

## Impact

- Unity scenes/prefabs: high-rise facade, drone, trigger volumes, UI canvas or world-space prompts, particle systems or VFX Graph assets, AudioSource or AudioMixer routes for narration.
- Possible new C# scripts: window mission controller, UI prompt controller, simple state machine for Phase A (extinguish) vs Phase B (thermal camera / load scene).
- Build settings: ensure the destination scene is included in the Editor build list if using `SceneManager.LoadScene`.
