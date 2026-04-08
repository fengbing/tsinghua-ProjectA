## 1. UI Foundation

- [x] 1.1 Create or update a bottom-anchored dialog panel prefab/canvas element with full-width black background.
- [x] 1.2 Add centered white text component configuration for dialog content and expose style fields for tuning.
- [x] 1.3 Implement show/hide behavior for the dialog panel with proper initial hidden state.

## 2. Dialog Data and Controller

- [x] 2.1 Define a dialog line data model containing sentence text, optional `AudioClip`, and optional per-line settings.
- [x] 2.2 Implement a dialog controller API to start a dialog session from a line sequence and to cancel/hide current session.
- [x] 2.3 Add completion events/callbacks for per-line completion and full-session completion.

## 3. Typewriter and Voice Synchronization

- [x] 3.1 Implement coroutine-based typewriter rendering that reveals characters one by one at configurable speed.
- [x] 3.2 Implement skip behavior to complete the current line text immediately without breaking flow state.
- [x] 3.3 Integrate per-line voice playback so each line starts its own clip and previous line audio is stopped on transition.
- [x] 3.4 Ensure audio and coroutine cleanup on session end, cancellation, and object disable/destroy.

## 4. Integration and Validation

- [x] 4.1 Wire one existing gameplay trigger (tutorial/story/event) to call the new dialog controller.
- [x] 4.2 Validate repeated triggering, scene transition, and pause/resume behavior for text and audio consistency.
- [x] 4.3 Add lightweight usage notes (inspector setup and API usage) for future content scripting.
