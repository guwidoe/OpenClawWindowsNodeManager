# Phase 4 – Screen, Camera & Location

## Goal
Enable full desktop capture and sensor capabilities where they make sense on Windows, with explicit user consent and clear privacy controls.

## Scope (What Users Get)
- **screen.record** and/or screenshot capture for Windows.
- **camera.snap / camera.clip** when hardware is available.
- **location.get** using Windows location services (if enabled).
- Clear in‑app permission UI for all capture/sensor features.

## User Experience Notes
- All capture actions must be explicit and clearly indicated.
- Users can revoke permissions at any time.
- If hardware isn’t present or permissions are disabled, show a simple explanation.

## Out of Scope (Explicitly Deferred)
- SMS send (not applicable on Windows)

## Acceptance Criteria
- Screen capture works on multi‑monitor setups.
- Camera and location features gracefully degrade when unavailable.
- Users are always aware when capture is active.

## Risks / Dependencies
- OS permission prompts and group policy restrictions.
- Multi‑monitor edge cases and performance.

## Open Questions
- Should capture be limited to a specific window by default?
- How should we indicate “recording in progress” globally (tray badge, toast, UI banner)?
