# Agent Testability Standard

## Purpose
Ensure any agent can verify a feature works **without** requiring a specific host setup (no real `openclaw`, no scheduled tasks, no UI clicking).

## Definition
A feature is **agent‑testable** when there is at least one deterministic, automated verification path that can run on a clean CI runner.

## Hard Rules
- No tests may depend on the real `openclaw` CLI, scheduled tasks, or GUI automation.
- Every feature must ship with **at least one automated test** that fails before the change and passes after.
- If a feature cannot be automated, add a **manual verification checklist** and explain why automation is blocked.

## Test Tiers (Required Coverage)
1. **Unit** – Pure logic: parsing, mapping, config, redaction, run‑settings.
2. **Service** – Core services using **fake runners** (no external processes).
3. **CLI** – Exit codes and output using faked service dependencies.
4. **UI Logic** – ViewModels or service‑level decision logic (no UI automation).

## Required Test Harnesses
- **Process Runner Abstraction**: inject a fake runner for CLI calls.
- **Fake OpenClaw Fixtures**: canned JSON/text outputs for status, pairing, errors.
- **Temp State Directory**: override `OPENCLAW_STATE_DIR` / `APPDATA` in tests.

## What Counts As Evidence
- A passing `dotnet test` in CI.
- Test names that clearly describe the feature.
- When blocked: a manual checklist in the feature spec.

## Non‑Goals
- Full end‑to‑end UI automation.
- Tests that require network access.

## Checklist For Agents
- [ ] Add/update unit tests for new logic.
- [ ] Add/update service tests using fake CLI responses.
- [ ] Add/update CLI tests for exit codes/output if behavior changed.
- [ ] Update CI if new test projects are added.
- [ ] If automation blocked, add manual checklist to the spec.
