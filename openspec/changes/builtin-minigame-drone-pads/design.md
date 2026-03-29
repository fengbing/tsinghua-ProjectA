## Context

`BuiltinMiniGame` is loaded additively today with an orthographic camera, EventSystem, and a simple UI panel (`BuiltinMiniGameSceneSetup`). The product owner wants a map-like 2D interaction layer: image background, zoom/pan, custom cursor, a drone that travels to click targets, click SFX, and paired receiver / buffer-pad objects with shared art per type.

## Goals / Non-Goals

**Goals:**

- **View**: Background image fills the playable 2D area; user can **zoom** with mouse wheel and **pan** so the view follows **mouse screen position** (interpreted consistently in world or normalized space).
- **Pointer**: Replace the software cursor (or draw overlaid cursor) with a **configurable sprite/texture** while this scene is active.
- **Drone**: On **mouse down or click** over the playfield (excluding UI consume if needed), compute **world position** and **move** the drone there (instant or short lerp — configurable).
- **Audio**: Every qualifying click plays a **configured** one-shot clip via `AudioSource.PlayOneShot` (or equivalent).
- **Receivers / pads**: N receivers share sprite A; N pads share sprite B; each receiver references one pad and a **fixed local offset** (pad position = receiver position + offset, or stored as child transform). Click **receiver** → drone moves to receiver → **show** paired pad. Click **visible pad** → **hide** pad. Relative layout per pair stays fixed when receivers are placed in the scene.

**Non-Goals:**

- Physics-based drone flight, pathfinding obstacles, or multiplayer.
- Persisting mini-game state across sessions (unless trivial `PlayerPrefs` — out of scope).
- Changing main mission flow beyond ensuring additive load/unload still works.

## Decisions

1. **Coordinate space — orthographic camera rig**  
   - **Choice**: Keep **one orthographic camera**; implement pan/zoom by adjusting camera `transform.position` (XY) and `orthographicSize` (or `Camera.fieldOfView` if perspective — prefer ortho). Background is a **Quad** or **SpriteRenderer** child of a **root** that scales with design resolution, or a large sprite at Z=0.  
   - **Rationale**: Matches existing scene setup; easy mapping from screen → world with `Camera.ScreenToWorldPoint`.  
   - **Alternative**: UI-only (`RawImage`) with `RectTransform` scale/anchoredPosition — more awkward for mixed world sprites (drone, receivers) unless everything is UI.

2. **Pan follows mouse**  
   - **Choice**: Each frame (or on mouse move), set camera XY so that **screen cursor position maps to a stable world point** (e.g., lerp camera toward offset that keeps “focus” under cursor, or simpler: **camera position tracks inverse of mouse delta mapped to world** — exact formula tuned in implementation). Document a single `MiniGameViewController` so tuning is centralized.  
   - **Rationale**: User asked for pan driven by mouse position and scroll zoom; centralized controller avoids scattered logic.

3. **Input layering**  
   - **Choice**: Use **Physics2D** (`Collider2D` + `OnMouseDown` / raycast) or **IPointerRaycast** on world objects. For empty background clicks, add a **full-screen `BoxCollider2D`** (or transparent `Image` on world-space canvas) behind entities so clicks hit world. **UI**: return button keeps blocking raycasts.  
   - **Rationale**: Clear separation between UI and world picks.

4. **Custom cursor**  
   - **Choice**: `Cursor.SetCursor` with hotspot from inspector, **or** hide system cursor and drive a **world/UI follower** sprite at mouse position. Prefer **hardware cursor** if texture fits OS limits; else **software follower** for large art.  
   - **Rationale**: Designer-specified art may exceed cursor size — software fallback documented.

5. **Drone motion**  
   - **Choice**: Default **smooth damp or lerp** over ~0.2–0.5s to target; expose speed in inspector. Option for instant snap.  
   - **Rationale**: Readable motion for training demos.

6. **Receiver / pad pairing**  
   - **Choice**: `MiniGameReceiverPair` component on receiver: references pad `GameObject`, optional `Vector3` offset if pad is not child. On activate: position pad relative to receiver once in `Start` (or always child). **Pad** has collider and `MiniGameBufferPad` script: toggles `SetActive` or alpha. **State**: pad hidden until first receiver activation; second click on receiver re-shows pad if hidden (per proposal).  
   - **Rationale**: Data-driven pairs without hard-coded IDs.

7. **Click sound**  
   - **Choice**: Single `AudioClip` on a coordinator or on drone object; **do not** play when click hits UI-only elements (return button).

## Risks / Trade-offs

- **[Risk] Cursor APIs differ per platform / texture size** → **Mitigation**: document max size; implement software-cursor fallback.
- **[Risk] Pan + zoom fights with UI** → **Mitigation**: use `GraphicRaycaster` first; only pan/zoom when pointer not over blocking UI, or ignore when `EventSystem.current.IsPointerOverGameObject()`.
- **[Risk] Screen-to-world depth** → **Mitigation**: fix Z plane for all gameplay objects (e.g., Z=0) and use `cam.nearClipPlane` or fixed Z in `ScreenToWorldPoint`.

## Migration Plan

1. Land scripts and prefab structure; assign references in `BuiltinMiniGame` scene.  
2. Run play mode: additive load from existing launcher; verify return flow unchanged.  
3. Rollback: revert scene + new scripts; placeholder scene remains functional.

## Open Questions

- Exact **pan** feel: continuous follow vs. drag-to-pan (user asked “鼠标的位置移动” — interpret as **cursor-dependent pan**, finalize formula during `/opsx:apply`).
- Whether **click on empty map** should move drone **and** count as receiver logic (yes — only receiver/pad scripts react).
