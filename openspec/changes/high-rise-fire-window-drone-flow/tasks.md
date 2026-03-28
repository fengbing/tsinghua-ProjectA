## 1. Scene and anchor setup

- [x] 1.1 Create or identify empty GameObjects as window anchors on the high-rise; add child slots for fire VFX, smoke VFX, and optional water spray origin.
- [x] 1.2 Add a trigger collider (or agreed primitive) per mission window, sized and positioned for drone approach; set layer matrix so only the drone collides as needed.
- [x] 1.3 Tag or layer the drone root per design so triggers can distinguish the player vehicle.

## 2. Fire and smoke VFX

- [x] 2.1 Assign or create flame and smoke particle (or VFX) assets at each anchor and verify visibility at play from drone distances.
- [x] 2.2 Implement stop/disable hooks for flame and smoke when the sprinkler phase succeeds (per `building-window-fire-vfx` spec).

## 3. Interaction UI

- [x] 3.1 Build a reusable two-box HUD (key hint + instruction `TMP_Text` or `Text`) on a screen-space canvas; wire show/hide from code.
- [x] 3.2 Set default strings: first phase `F` + “快打开喷淋系统灭火！”; second phase `F` + “快启用双光热成像相机！”.

## 4. Narration and mission state

- [x] 4.1 Add serialized `AudioClip` fields (or small config) for first proximity narration and post-extinguish narration; play via `AudioSource` without overlapping duplicates for the same phase.
- [x] 4.2 Implement the window mission state machine: idle → show first prompt + play first VO → wait for `F` → play water VFX + suppress fire/smoke + play second VO + show second prompt → wait for `F` → load next scene.
- [x] 4.3 Hook `F` through the project’s input system (Input System action or legacy) consistently with existing drone controls; ignore `F` when not in the correct phase.

## 5. Water/sprinkler effect and scene transition

- [x] 5.1 Enable or spawn water/sprinkler VFX on first `F` at the configured anchor; tune duration or looping per art direction.
- [x] 5.2 Add serialized destination scene name (or build index) and call `SceneManager.LoadScene` on second `F`; add the scene to **Build Settings**.

## 6. Verification

- [ ] 6.1 Play-mode test: approach window → hear/see first prompt → `F` → water + fire out + second prompt → `F` → next scene loads once.
- [ ] 6.2 Regression check: drone flight and unrelated UI still behave; no errors in console from missing references or missing build scenes.
