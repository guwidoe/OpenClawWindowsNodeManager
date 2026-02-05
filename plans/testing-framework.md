# Testing Framework Plan

## Goal
Provide a repeatable, host‑independent testing framework so any agent can verify new features in CI.

## Scope
- Unit tests for parsing and decision logic.
- Service tests with fake process runner.
- CLI tests for output and exit codes.
- Lightweight UI‑logic tests (no UI automation).

## Non‑Goals
- Full end‑to‑end UI automation.
- Real `openclaw` execution in CI.
- Windows Scheduled Task integration tests in CI.

## Test Types
- **Unit**: `OpenClaw.Win.Core.Tests`
- **Service**: faked CLI runner + fixtures
- **CLI**: output/exit code validation
- **Manual**: short checklist only when automation isn’t feasible

## Fixtures & Fakes
- Canned JSON/text samples for:
  - node status (running/stopped/connected)
  - pairing required
  - token invalid
  - gateway unreachable
- Fake runner to inject expected process results.
- Temp state dir for `node.json` and config fixtures.

## CI Gates
- `dotnet test` on every push/PR.
- Tests must be deterministic and run under `windows-latest`.
- No network access required.

## Acceptance Criteria
- Any feature PR includes at least one automated test.
- CI reliably passes on a clean runner.
- Tests cover parsing, error mapping, and exit codes for core flows.

## Risks
- Upstream output changes can break parsers; mitigate with fixture updates.
- Too much logic in UI layer; mitigate by pushing decisions into services/ViewModels.

## Open Questions
- Should we add a dedicated `OpenClaw.Win.Cli.Tests` project for CLI behavior?
- Should we version fixtures to reflect upstream CLI versions?
