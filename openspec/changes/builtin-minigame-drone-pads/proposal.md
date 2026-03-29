## Why

`BuiltinMiniGame` is currently a placeholder additive scene; we need a self-contained 2D interaction loop for training or demonstration: a large background the user can pan and zoom like a map, a visible drone that responds to clicks and receiver targets, and paired “buffer pad” objects that appear and disappear in sync with receiver use.

## What Changes

- Replace or extend the mini-game world in `BuiltinMiniGame` with a **2D playfield**: background from an image, camera (or root transform) **pan following the mouse** and **zoom via scroll wheel** (local magnification of the view).
- **Custom mouse cursor** using a designer-specified texture/sprite (replacing or overlaying the default cursor in this scene).
- A **drone** object that **moves to the world position under the cursor** on each primary click.
- **Click feedback**: play a **one-shot audio clip** on every click (same clip each time unless configured otherwise).
- Several **receiver** objects sharing one sprite/image; each receiver is **paired** with a **buffer pad** object that uses another **shared** sprite; **relative offsets** between receiver and pad are fixed per pair.
- **First click on a receiver**: move the drone to that receiver’s position, then **show** the paired buffer pad. **Click on the visible buffer pad**: **hide** that pad (toggle off). Re-clicking the receiver can follow the same rule (drone moves, pad shows again) unless design specifies one-way behavior—default: **receiver always queues drone + shows pad if hidden; pad click only hides**.

## Capabilities

### New Capabilities

- `builtin-minigame-view-zoom`: Pan and zoom of the 2D background (mouse position drives pan; scroll wheel zoom); orthographic or equivalent 2D framing.
- `builtin-minigame-drone-pointer`: Custom cursor graphic; map screen/world picking for clicks; drone movement to target; per-click sound.
- `builtin-minigame-receiver-pairs`: Receiver and buffer-pad prefabs or instances, shared art per category, fixed per-pair relative transforms, click routing between “move drone + show pad” and “hide pad”.

### Modified Capabilities

- _(none — no existing `openspec/specs/` baseline in this repo.)_

## Impact

- **Scene**: `Assets/Scenes/BuiltinMiniGame.unity` — add/configure sprites, audio, colliders or UI raycast targets, and any camera rig.
- **Scripts**: new C# under `Assets/Scripts/MiniGame/` (or a subfolder) for view controller, input, drone movement, receiver/pad pairing; optional small editor wiring in `BuiltinMiniGameSceneSetup` for initial hierarchy.
- **Assets**: background texture, cursor texture, drone sprite, receiver sprite, buffer-pad sprite, click SFX; existing `MiniGameLauncher` / `MiniGameAdditiveFlow` remain entry points unless scene name changes.
