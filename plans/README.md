# Feature Implementation Phases

This folder tracks product specs for the Windows Companion roadmap. Each phase is a **spec sheet** (what + why + acceptance), not an implementation guide.

## Priority Lens
We prioritize features that are **not available in the OpenClaw dashboard** and that benefit from being **native to Windows**. Dashboard‑overlap features are postponed unless they directly unblock core usage.

## Phases
- **Phase 1 – Core Stability & Management** (`plans/phase-1-core-stability.md`)
- **Phase 2 – Trust, Approvals & Notifications** (`plans/phase-2-trust-notify.md`)
- **Phase 3 – Canvas + Screen Capture + Browser Parity** (`plans/phase-3-canvas-browser.md`)
- **Phase 4 – Camera & Location** (`plans/phase-4-capture-sensors.md`)
- **Phase 5 – Optional / Not Applicable (SMS)** (`plans/phase-5-optional-sms.md`)

## Feature Coverage Map (from comparison list)
- **Basic**: connect/disconnect, status, gateway config/token, pairing flow → Phase 1 (hardened)
- **System**: `system.run`, `system.notify`, exec approvals UI → Phase 2
- **Canvas**: display, snapshot, eval, A2UI → Phase 3
- **Screen**: screen.record / screenshot → Phase 3
- **Browser**: relay integration (beyond settings) → Phase 3
- **Camera**: snap/clip → Phase 4 (hardware-dependent)
- **Location**: location.get → Phase 4 (permission + device-dependent)
- **Messaging**: sms.send → Phase 5 (not applicable on Windows)
