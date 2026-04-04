## Why

The `storage` scene needs a guided narrative introduction so players understand context before flying, without building complex 3D UI—full-screen image panels are enough. Audio must play once per step so repeats do not annoy on revisit, and the correct pickup code should feel rewarding with a cue plus an automatic minimap reveal (same as pressing **M**).

## What Changes

- Add a **sequential image-based UI flow** when entering the storage scene: initial loading screen → background story screen → second loading screen → then normal scene control.
- Wire **one-shot AudioSource clips** at each transition: first loading (1 clip), story (1 clip), second loading (2 clips in order), after entering playable scene (2 clips in order).
- On **correct pickup code entry** from the drone/UI: play one success clip (once per successful entry session or once per run—see spec), then **programmatically trigger the same minimap open action** as the **M** key.
- Optionally **block drone/input** until the narrative completes, depending on design (default: block until flow finishes).

## Capabilities

### New Capabilities

- `storage-narrative-flow`: Image panels, transition order, gating of gameplay input/camera until the sequence completes, and integration points for audio cues per phase (initial load, story, second load, post-enter scene).
- `storage-pickup-code-feedback`: Behavior when the player enters the correct pickup code—success audio and invoking minimap toggle to match manual **M** behavior.

### Modified Capabilities

- (none—no existing `openspec/specs/` baseline in this repo)

## Impact

- **Unity**: `storage` scene hierarchy (Canvas/Image panels), AudioSources or a small narrative controller script, references to assets under `Assets/` (images, clips). Existing scripts handling pickup code validation and minimap toggle need a hook or a thin coordinator (e.g. new `StorageNarrativeController` + events). **Input**: ensure simulated **M** uses the same code path as `Input.GetKeyDown(KeyCode.M)` for the map.
