# OpenClaw Windows Node Companion

A native Windows companion for running an OpenClaw **node host** with a friendly tray experience and a scriptable CLI. The app wraps the upstream `openclaw` CLI (it does not replace it).

## Highlights
- System tray toggle with clear **Connected / Disconnected / Degraded / Error** states.
- Settings UI for gateway configuration, token storage, and Chrome relay setup.
- CLI for automation: `status`, `connect`, `disconnect`, `toggle`, `configure`, `install`, `uninstall`, `logs`, `doctor`.

## Repo Layout
- `src/OpenClaw.Win.Core` Core services (config, token storage, node status, gateway tests).
- `src/OpenClaw.Win.Cli` CLI entry (`openclaw-win.exe`).
- `src/OpenClaw.Win.App` WPF tray app and settings UI.

## Prerequisites
- Windows 10/11
- .NET 8 SDK
- `openclaw` CLI installed and available on PATH

## Build
```
dotnet build OpenClawWindowsNodeManager.sln -c Release
```

## Run
- Tray app: `src/OpenClaw.Win.App/bin/Release/net8.0-windows/OpenClaw.Win.App.exe`
- CLI: `src/OpenClaw.Win.Cli/bin/Release/net8.0-windows/openclaw-win.exe`

## CLI Usage
```
openclaw-win status
openclaw-win configure --host <gateway-host> --port 443 --tls --token <gateway-token> --display-name "My Windows Node"
openclaw-win connect
openclaw-win disconnect
openclaw-win toggle
openclaw-win logs --tail --lines 200
openclaw-win doctor
```

Exit codes:
- `0` connected/success
- `2` disconnected
- `3` degraded (running but not connected)
- `10` config missing
- `11` openclaw missing
- `12` auth/token error
- `13` pairing required

## Settings & Data Locations
The companion stores data under:
`%AppData%\OpenClaw\WindowsCompanion`

Files:
- `config.json` connection and UI settings
- `token.dat` gateway token (DPAPI protected)
- `logs/app.log` and `logs/node.log`
- `diagnostics/` exported diagnostic bundles

## Security Notes
- Gateway token is stored using Windows DPAPI (CurrentUser scope).
- Tokens are never written to logs.
- Diagnostics export redacts secrets.

## Status Definitions
- **Connected**: node host is running and connected to the gateway.
- **Degraded**: node host is running but gateway is unreachable or disconnected.
- **Disconnected**: node host is stopped.
- **Error**: configuration or authentication error detected.

## OpenClaw Integration Notes
The companion uses the upstream CLI for lifecycle management:
- `openclaw node install` to set up background runner
- `openclaw node restart` to connect
- `openclaw node stop` to disconnect
- `openclaw node status --json` for status

## Troubleshooting
- Run `openclaw-win doctor` for a quick environment check.
- Ensure `openclaw` is on PATH (`where openclaw`).
- If status shows pairing required, approve the node in the gateway UI or via `openclaw nodes pending` / `openclaw nodes approve`.

## Roadmap (MVP Focus)
- Harden JSON parsing against upstream schema changes.
- Optional: direct Scheduled Task management if needed.
- UX polish for pairing and Chrome relay setup.
