## Why

PlaneGame is the main drone delivery beat, but audio and narrative do not yet reinforce progression: there is no sustained ambient bed, no cue when the player closes within mission range, and no voiced payoff when the package is correctly placed on the platform before advancing. This change wires a small state machine to existing distance HUD data and delivery success so the level feels guided and finishes in Level 2.

## What Changes

- On entering **PlaneGame**, start looping **background music** for the mission.
- When the drone’s distance to the mission target (as computed for the bottom **DistanceHudStrip** / `IDistanceHudSource`) is **≤ 50 m**, **stop** the background music and play a **one-shot** SFX/Stinger **once** (no repeat while still inside the threshold unless reset by design).
- When the drone **successfully places the package on the designated platform**, play **three voice clips in strict sequence**; after the **third** finishes, **load Level 2** (`Assets/Scenes/Level 2.unity`, build index as configured in **Editor Build Settings**).
- Scene wiring: serialized references for clips, `AudioSource`(s) or a small audio helper, and hooks from distance source and placement success (reuse or extend existing gripper / trigger logic as needed).

## Capabilities

### New Capabilities

- `planegame-narrative-audio`: Scene-level narrative audio for PlaneGame—startup BGM, proximity-based BGM stop + one-shot, delivery-success voice chain, and transition to Level 2 after the last line.

### Modified Capabilities

- (None—existing distance HUD behavior stays the same; we only consume its distance signal or equivalent target distance.)

## Impact

- **Scenes**: `Assets/Scenes/PlaneGame.unity` (new controller GameObject / references).
- **Scripts**: Likely a new `MonoBehaviour` (e.g. scene narrative/audio director) plus minimal hooks: read distance from the same target logic as `DistanceToTargetSource` or subscribe to a small event from the HUD source; listen for “package on platform” success (collision/trigger, gripper release-on-pad, or existing mission script).
- **Audio**: `AudioClip` assets referenced in the inspector (BGM loop, proximity one-shot, three voice lines).
- **Build**: Level 2 must remain in `EditorBuildSettings`; load by name or build index consistent with project conventions (`SceneManager`).
