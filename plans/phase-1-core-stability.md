# Phase 1 – Core Stability & Management

## Goal
Deliver a reliable, no‑surprises Windows node companion that makes connect/disconnect safe, obvious, and repeatable for daily use, without relying on the dashboard.

## Scope (What Users Get)
- One‑click connect/disconnect that consistently works without opening terminals.
- Clear status that does not flap on transient errors.
- Hidden background runner that survives the settings window closing.
- Minimal pairing/token flow required for connectivity (not a full dashboard replacement).
- Built‑in logs and diagnostics suitable for support requests.
- CLI parity for status/connect/disconnect/diagnostics (scriptable).
- Optional dark theme aligned with OpenClaw visual vibes (native look, easy on eyes).

## User Experience Notes
- Default posture remains **disconnected** until the user explicitly connects.
- Status must be clear across tray + settings UI (Connected/Disconnected/Degraded/Error).
- Error feedback is actionable and non‑spammy (rate limited notifications).
- Pairing and token flows are present but intentionally minimal; deep management stays in the dashboard.
- Autostart is user‑controlled and discoverable (already in Settings).

## Out of Scope (Explicitly Deferred)
- Exec approvals UI
- System notifications from the gateway
- Canvas/WebView
- Screen recording/capture
- Camera/location/SMS

## Acceptance Criteria
- Connect/disconnect succeeds without visible terminals.
- No “silent failure” states: all failures show a reason and next step.
- Status stabilizes and does not oscillate when the gateway is temporarily slow.
- Diagnostics bundle includes logs + redacted config + status.
- CLI commands match UI behavior and exit codes.
- Dark theme can be enabled and stays consistent across tray + settings UI.

## Risks / Dependencies
- Upstream `openclaw` CLI output changes may affect status parsing.
- User environments with multiple node instances (foreground + scheduled task).

## Open Questions
- Should the app always prefer hidden node mode when enabled, even if the node is already connected by another process?
- Should “connected” mean running only, or running **and** confirmed gateway connectivity? (Currently: running + connected.)
