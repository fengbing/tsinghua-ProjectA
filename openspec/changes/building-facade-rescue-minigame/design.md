## Context

The Unity project already ships a fullscreen built-in minigame pattern (`RoutePlanningMiniGameController`) that uses a dedicated root `GameObject`, blocks conflicting UI shortcuts via a static “overlay open” flag, and optionally coordinates with `PlaneController` for input. Window fire missions (`WindowFireMission` and related zones) can represent extinguishing completion and narrative audio. This change adds a second fullscreen minigame: a facade rescue flow with a strict state machine, elevator motion in UI space, and mission-gated entry.

## Goals / Non-Goals

**Goals:**

- Implement a deterministic, sequential rescue experience (three windows, one active at a time) with clear UI phases and elevator motion tied to facade layout.
- Reuse the established integration style for fullscreen overlays: a single controller, serialized references for art and audio, explicit enter/exit, and drone or UI input gating consistent with existing minigames.
- Make trigger conditions data-driven in the inspector: fire cleared, two prerequisite clips finished, drone inside a trigger volume.

**Non-Goals:**

- Physics-based rope rescue, pathfinding, or free camera movement inside the minigame.
- Branching narrative beyond the elevator versus slide decision (no alternate endings per person in this iteration).
- Networked or saved per-profile progression beyond normal scene state.

## Decisions

1. **Dedicated controller + explicit state machine**

   - **Decision:** Implement `FacadeRescueMiniGameController` (name illustrative) with an enum-driven state machine (`Idle`, `Entering`, `ElevatorToWindow`, `WindowIntro`, `PersonDetails`, `Choosing`, `Resolving`, `BetweenFloors`, `Exiting`, `Complete`).
   - **Rationale:** The UX is a fixed pipeline; a state machine keeps UI and animation transitions testable and avoids race conditions from overlapping coroutines.
   - **Alternative considered:** Pure Unity Animator-driven flow; rejected because UI popups and audio completion are easier to orchestrate in code for this product.

2. **Layout as normalized facade coordinates**

   - **Decision:** Store per-window and per-elevator-stop positions as vertical anchors (0–1 along facade height) or explicit `RectTransform` references parented under a facade root; elevator moves by tweening `anchoredPosition.y` (or localPosition) between configured stops.
   - **Rationale:** Matches “picture of a building” workflow and survives resolution changes better than hard-coded pixels alone.
   - **Alternative considered:** World-space billboard facade; rejected to stay aligned with the existing fullscreen Canvas minigame approach.

3. **Trigger gating owned by mission flow**

   - **Decision:** Extend or compose with the window-fire mission controller so it tracks: extinguished, both post-audio clips completed, and `OnTriggerStay`/enter against a tagged drone collider on a `minigame trigger` volume. Only then call `FacadeRescueMiniGameController.Open()`.
   - **Rationale:** Keeps gameplay truth in the mission layer; the minigame remains a view/controller that assumes “entry is allowed.”
   - **Alternative considered:** Minigame polls mission flags every frame; rejected to avoid hidden coupling and duplicate logic.

4. **Audio policy**

   - **Decision:** Slide choice plays a dedicated one-shot SFX; elevator choice does not require an extra SFX unless designers add one later. Final “system prompt” is a serialized `AudioClip` or routed through the existing system dialog voice path if that is already standardized in the scene.
   - **Rationale:** Matches the user’s stated behavior (sound only on slide).
   - **Alternative considered:** Always play a confirm sound; rejected as out of spec.

5. **Portrait stacking on elevator**

   - **Decision:** Use a small horizontal stack of `Image` slots on the elevator graphic; each successful rescue activates the next slot with the person’s portrait sprite.
   - **Rationale:** Simple, readable, and avoids dynamic texture composition.
   - **Alternative considered:** Single image replaced each time; rejected because the spec expects visible accumulation of rescued people on the elevator.

6. **Static overlay guard flag**

   - **Decision:** Expose `public static bool IsFacadeRescueOpen` (or similar) mirroring `RoutePlanningMiniGameController.IsPlanningUiOpen` so global UI and pause logic can ignore conflicting shortcuts while the rescue UI is active.
   - **Rationale:** Proven pattern already in the codebase.

## Risks / Trade-offs

- **[Risk]** Player opens pause or other UI while the minigame expects exclusive focus → **Mitigation:** hook the same global pause / `GameUi` rules as route planning; block duplicate overlays.
- **[Risk]** Drone leaves trigger between checks → **Mitigation:** require continuous overlap (trigger stay) or latch “eligible once true” depending on design review; default to latched eligibility after first valid trigger to reduce frustration.
- **[Risk]** Audio clip lengths change, delaying entry → **Mitigation:** drive completion from `AudioSource` end events or explicit durations per serialized clip reference.
- **[Trade-off]** Strict linear flow is easier to build but less exploratory → **Accept** for training clarity.

## Migration Plan

- Land controller and UI prefab disabled by default; wire references in the Level 2 (or target) scene where the window mission already exists.
- Validate in editor play mode: extinguish → audio → trigger → minigame → exit → return control to drone.
- Rollback: disable `fullscreenRoot` GameObject and remove mission hook call; no data migration.

## Open Questions

- Whether “two audio clips” are always sequential one-shots from the mission or sometimes a single merged clip (affects completion detection API).
- Exact copy and art aspect ratio for the facade and whether safe areas (notch) must be padded for mobile builds if applicable.
- Whether wrong educational choices (always choosing elevator) should show corrective copy; current product input implies any choice advances flow—confirm with design.
