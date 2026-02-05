# Phase 4 – Camera & Location

## Goal
Enable camera and location capabilities where they make sense on Windows, with explicit user consent and clear privacy controls.

## Scope (What Users Get)
- **camera.snap / camera.clip** when hardware is available.
- **location.get** using Windows location services (if enabled).
- Clear in-app permission UI for all camera/location features.

## User Experience Notes
- All capture actions must be explicit and clearly indicated.
- Users can revoke permissions at any time.
- If hardware isn’t present or permissions are disabled, show a simple explanation.

## Out of Scope (Explicitly Deferred)
- SMS send (not applicable on Windows)

## Acceptance Criteria
- Camera features work when hardware is present.
- Location features work when Windows location services are enabled.
- Features gracefully degrade when unavailable.

## Risks / Dependencies
- OS permission prompts and group policy restrictions.
- Hardware variability on desktops.

## Open Questions
- Should camera access be limited to a single default device by default?
- How should the UI indicate “camera active” or “location active” globally?
