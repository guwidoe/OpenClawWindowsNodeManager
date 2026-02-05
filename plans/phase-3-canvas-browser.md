# Phase 3 – Canvas, WebView & Browser Parity

## Goal
Deliver the visual capabilities that make OpenClaw feel “present” on Windows: display content, render A2UI, and enable browser relay workflows beyond simple setup.

## Scope (What Users Get)
- **Canvas display** in an embedded WebView.
- **canvas.snapshot** support (screenshots of canvas output).
- **canvas.eval** support (run JS in the canvas context).
- **A2UI rendering**: agent UI output appears in the embedded view.
- **Browser relay integration** beyond settings:
  - Clear connected/disconnected state
  - One‑click verify
  - Guidance when extension isn’t attached to a tab

## User Experience Notes
- Canvas should open quickly and not block connect/disconnect.
- If WebView runtime is missing, the UI should guide installation.
- Browser relay should feel “setup once, then just works.”

## Out of Scope (Explicitly Deferred)
- Screen recording/capture (beyond canvas snapshots)
- Camera/location/SMS

## Acceptance Criteria
- Canvas view renders agent output reliably.
- Canvas snapshots can be triggered and returned successfully.
- Browser relay is easy to verify and its state is visible in UI.

## Risks / Dependencies
- WebView runtime availability and versioning.
- Performance impact of rendering and snapshot capture.

## Open Questions
- Should Canvas be always-on in a dedicated tab or opened on demand?
- How should multiple canvas sessions be handled?
