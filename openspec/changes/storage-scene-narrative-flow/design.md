## Context

The **storage** scene already has image-friendly patterns (`StartScreenController` for click-through loading) and a **decrypt / 取件码** flow (`DecryptPuzzleSystem` + `DecryptPuzzleUI` with `OnDecryptPuzzleSolved`). The **minimap** is driven by `MinimapUiController`, which toggles between corner minimap and fullscreen on the configured key (default **M**). This change adds a **linear narrative shell** (full-screen images + staged one-shot audio) before normal play, without replacing existing gameplay systems.

## Goals / Non-Goals

**Goals:**

- Drive a fixed order of **full-screen image panels**: initial loading → background story → second loading → hide overlays and enable play.
- Play **non-looping, once-per-run** audio at each stage as specified (counts: 1 + 1 + 2 + 2 clips across the four pre-play phases).
- After **correct 取件码**, play one success clip and **invoke the same view transition as a single M key press** on `MinimapUiController` (see Decisions).
- Centralize sequencing in one coordinator to avoid scattered `AudioSource.Play()` calls in the scene.

**Non-Goals:**

- Cinematic Timeline, video cutscenes, or localization.
- Changing decrypt rules, puzzle generation, or mission objectives.
- Persisting “already heard” audio across application restarts (session/run scope is enough unless product asks otherwise).

## Decisions

1. **Coordinator pattern**  
   **Choice**: Add a `StorageNarrativeController` (or similarly named) `MonoBehaviour` on a scene root / Canvas that holds references to panel roots, `AudioSource`(s) or clip lists, and optional `PlaneController`/input disabler.  
   **Rationale**: Keeps `DecryptPuzzleUI` and `MinimapUiController` changes minimal—subscribe to existing events instead of embedding story logic in every subsystem.  
   **Alternatives**: Only Inspector wiring with Unity Timeline (heavier) or per-panel scripts (duplicated transition logic).

2. **One-shot audio**  
   **Choice**: Use dedicated clips with **Play One Shot** or non-looping `AudioSource` with a **bool gate per phase** so each clip fires at most once per storage scene load; use a small queue/coroutine for the “two clips in order” steps.  
   **Rationale**: Matches “只放一次” and avoids repeat on panel revisit.  
   **Alternatives**: `PlayableGraph` (unnecessary complexity for linear cues).

3. **Minimap “one M press”**  
   **Choice**: Add a **public method** on `MinimapUiController`, e.g. `PerformToggle()` or `SimulateToggleKeyPress()`, that runs the **same branch** as `Input.GetKeyDown(toggleKey)` in `Update` (toggle `MapViewMode` + `ApplyMode()`). Narrative / puzzle success calls this once.  
   **Rationale**: Guarantees behavior matches manual **M** even if toggle logic later gains side effects.  
   **Alternatives**: Send synthetic input (platform-dependent, brittle) or duplicate visibility logic (drift risk).

4. **Pickup code success hook**  
   **Choice**: Subscribe to `DecryptPuzzleUI.OnDecryptPuzzleSolved` (already public) from the coordinator or a thin `StoragePickupFeedback` helper.  
   **Rationale**: Event already exists; avoids modifying puzzle internals.

5. **Input during narrative**  
   **Choice**: Default to **disabling drone movement** (`PlaneController` or project equivalent) until the last pre-play panel completes; re-enable before decrypt interaction if the UI is already on-screen. Exact gating is adjusted per scene layout in tasks.  
   **Rationale**: Prevents flying during story; can be relaxed if designers want skip.

## Risks / Trade-offs

- **[Risk]** Default minimap mode is already “minimap visible”; one **M** toggles to **fullscreen**, which might not match colloquial “小地图”.  
  **→ Mitigation**: Document in QA; if product wants “ensure corner minimap visible” only, add a dedicated `ShowMinimapOnly()` instead of toggle—tracked as an open product question.

- **[Risk]** Double subscription or scene reload duplicates audio.  
  **→ Mitigation**: Coordinator uses `OnDestroy` unsubscribe; single instance guard if needed.

- **[Risk]** Large full-screen images increase build size.  
  **→ Mitigation**: Use appropriate texture import settings (acceptable trade-off for art-driven UI).

## Migration Plan

1. Implement coordinator + minimap API + audio wiring in **development** branch.  
2. Author place **placeholder** images/audio in `Assets/` until final art.  
3. **Rollback**: Disable narrative root GameObject or remove component from storage scene; no data migration.

## Open Questions

- Should players be able to **skip** the narrative (button/key)? **Implemented today:** per-step advance only (Space / Return / full-screen `Button` → `AdvanceNarrative`). There is no “skip entire sequence” key; add later if product requires it.
- After correct 取件码, should success audio **block** input until finished? (Default: non-blocking unless specified.)
