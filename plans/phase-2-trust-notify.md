# Phase 2 – Trust, Approvals & Notifications

## Goal
Provide desktop‑grade security UX and feedback so users can safely approve actions and receive meaningful notifications without opening a terminal.

## Scope (What Users Get)
- **Exec approvals UI**: a clear allow/deny workflow for `system.run` approvals.
- **Approval history**: a visible log of approved/denied commands with timestamps.
- **System notifications**: Windows toasts for key events (connect/disconnect, approvals requested, errors).
- **Policy clarity**: display current exec policy and how to change it.

## User Experience Notes
- Approvals are front‑and‑center and interruptible, but not overly disruptive.
- Notifications are helpful, rate‑limited, and can be disabled.
- Clear indication when approvals are pending.

## Out of Scope (Explicitly Deferred)
- Canvas/WebView and A2UI rendering
- Screen capture/recording
- Camera/location/SMS

## Acceptance Criteria
- When an approval is requested, the UI surfaces it within 3 seconds.
- Users can approve/deny and see immediate feedback.
- Notifications can be toggled in settings.
- Exec approval actions are visible in a local history log.

## Risks / Dependencies
- Requires a stable upstream approval API surface.
- Notification permissions and registration on Windows.

## Open Questions
- Should approvals support “approve once”, “approve for session”, and “always approve”? If so, how is this displayed to users?
- What is the default approval policy for new installs?
