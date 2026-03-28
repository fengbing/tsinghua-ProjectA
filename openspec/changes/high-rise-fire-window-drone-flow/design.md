## Context

The project is a Unity-based experience with a drone as the player vehicle and an existing high-rise asset. The desired tutorial-style beat adds fire presentation at windows, then gates progression on proximity, UI prompts, narrator lines, two `F` interactions, and a scene change. No prior OpenSpec capability covered this flow; implementation will touch scene layout, VFX, UI, audio, and input.

## Goals / Non-Goals

**Goals:**

- Author smoke and flame effects per window in a way level designers can duplicate or configure without code changes for every floor.
- Drive a deterministic two-phase interaction: extinguish → narrative/UI refresh → load thermal-imaging scene.
- Keep narration and UI text synchronized with phase state (no stale prompts while audio plays, unless explicitly designed).

**Non-Goals:**

- Full building procedural fire spread, damage systems, or multiplayer replication.
- Localization beyond the specified Chinese prompt strings (structure should still allow future localization).
- Replacing the drone flight model or camera rig (only integration hooks such as tags or layer checks).

## Decisions

1. **Window anchor pattern**  
   Each interactive window is represented by an empty GameObject (or small prefab) with: child positions for fire/smoke VFX, optional water spray spawn point, and a **trigger collider** (set as trigger, sized for approach from the drone side).  
   *Rationale:* Triggers are cheap, editable in-editor, and avoid per-frame distance checks unless triggers prove insufficient (e.g. very fast drone).  
   *Alternative considered:* Pure `Vector3.Distance` from drone to window — simpler code but harder to tune per facade geometry.

2. **Drone identification**  
   Use a dedicated **layer** or **tag** on the drone root (e.g. `Player` / `Drone`) checked in `OnTriggerEnter`/`OnTriggerStay`.  
   *Rationale:* Matches common Unity patterns and avoids hard references to a specific prefab type if the drone is swapped.

3. **Mission state machine**  
   A single coordinator component on the window anchor (or a small `WindowFireMission` MonoBehaviour) owns enum-like phases: `Idle` → `PromptSprinkler` → `AwaitSprinklerF` → `SprinklerActive` → `PromptThermal` → `AwaitThermalF` → `LoadScene`.  
   *Rationale:* Clear ordering prevents double `F` handling and simplifies testing.  
   *Alternative considered:* Separate scripts per phase — more files for a linear flow.

4. **UI presentation**  
   **Screen-space overlay** canvas with a reusable two-panel widget (key badge + text). World-space billboards were considered for parallax but add sorting and scaling issues for drone altitude; overlay keeps readability.  
   *Rationale:* Consistent legibility; anchor position can be optional world-to-screen follow later if art requests it.

5. **Input**  
   Prefer the project’s existing input path (**Input System** `Keyboard.current.fKey` or existing `PlayerInput` actions). If the repo still uses legacy `Input.GetKeyDown`, match that for consistency.  
   *Rationale:* One code path for “interact” avoids duplicate subscriptions.

6. **Audio**  
   Use **AudioClip** references on the mission component (or a nested serializable struct) for “enter prompt”, “after extinguish”, etc. Play via `AudioSource` on the window or a central UI audio source; **no** overlapping play for the same phase (skip or stop previous if re-entered — see risks).

7. **Scene load**  
   Use `SceneManager.LoadScene` with scene name or build index from a serialized field; mode **Single** unless the thermal scene must preserve this scene’s objects (then **Additive** + unload policy — default to Single for clarity).

8. **Fire/smoke shutdown**  
   Disable particle emission/stop systems and optionally disable renderers rather than destroying GameObjects, so designers can reset the scene in-editor without re-instantiating.

## Risks / Trade-offs

- **[Risk] Re-triggering the volume** resets state awkwardly → **Mitigation:** After phase completion for sprinkler, ignore re-entry for extinguished window or use a `completed` flag; document behavior in inspector tooltips.
- **[Risk] Narrator and `F` spam** → **Mitigation:** While a narrator clip for the current phase is playing, ignore `F` **or** queue input after clip ends — pick one and match spec (spec will require ignoring `F` until sprinkler prompt is fully shown after proximity).
- **[Risk] Destination scene not in build** → **Mitigation:** Document in tasks to add scene to Build Settings; optional editor validation script.
- **[Trade-off] Screen-space UI** does not “stick” to the window in world — acceptable for training clarity; can iterate to world-space later.

## Migration Plan

1. Add new prefabs/scripts and wire one pilot window in the main fire scene.
2. Add destination scene to **File → Build Settings**.
3. Play-mode test full sequence; duplicate window prefab across floors as needed.

**Rollback:** Remove mission component and triggers from windows; disable VFX objects. No data migration.

## Open Questions

- Exact **destination scene** asset name and whether any objects must persist (Singletons, drone) — confirm before final wiring.
- Whether **smoke** should remain after sprinkler or fade with fire (default: reduce/stop both for visual clarity).
- Project-specific **input action** name if Input Actions asset already defines “Interact” instead of raw `F`.
