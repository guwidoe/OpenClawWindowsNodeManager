# Phase 3 – Canvas, Screen Capture & Browser Parity

## Goal
Deliver the visual and capture capabilities that make OpenClaw feel “present” on Windows: display content, render A2UI, capture the screen, and enable browser relay workflows beyond simple setup.

## Scope (What Users Get)
- **Canvas display** in an embedded WebView.
- **canvas.snapshot** support (screenshots of canvas output).
- **canvas.eval** support (run JS in the canvas context).
- **A2UI rendering**: agent UI output appears in the embedded view.
- **Screen capture/record** for Windows.
- **Browser relay integration** beyond settings: clear connected/disconnected state, one-click verify, and guidance when the extension isn’t attached to a tab.

## User Experience Notes
- Canvas should open quickly and not block connect/disconnect.
- If WebView runtime is missing, the UI should guide installation.
- Screen capture must show a clear user-visible indicator when active.
- Browser relay should feel “setup once, then just works.”

## Out of Scope (Explicitly Deferred)
- Camera/location/SMS

## Acceptance Criteria
- Canvas view renders agent output reliably.
- Canvas snapshots can be triggered and returned successfully.
- Screen capture works on multi-monitor setups.
- Browser relay is easy to verify and its state is visible in UI.

## Risks / Dependencies
- WebView runtime availability and versioning.
- Performance impact of rendering, snapshots, and screen capture.

## Open Questions
- Should Canvas be always-on in a dedicated tab or opened on demand?
- How should multiple canvas sessions be handled?
- Should screen capture be limited to a specific monitor or window by default?
