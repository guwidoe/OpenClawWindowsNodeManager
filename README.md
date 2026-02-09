# OpenClaw Windows Node Companion

A native Windows companion for running an OpenClaw **node host** with a friendly tray experience and a scriptable CLI. The app wraps the upstream `openclaw` CLI (it does not replace it).

## Features

### System Tray
- One-click connect/disconnect toggle (double-click to toggle)
- Color-coded status icons: Connected (green) / Disconnected (gray) / Degraded (yellow) / Error (red)
- Balloon notifications on status changes (with cooldown to avoid spam)
- Quick access menu: Connect, Disconnect, Open Settings, Open Logs, Open Control UI, Quit

### Settings UI
- **Connection tab**: Gateway host/port, TLS settings, display name, Control UI URL
- **Secure token storage**: Gateway token encrypted with Windows DPAPI
- **Gateway connectivity test**: Checks DNS, TCP, TLS, HTTP, and WebSocket connectivity
- **Auto-start at login**: Registers itself in Windows startup
- **Theme**: Follows system setting by default, or set to dark/light manually

### Node Host Management
- Install/Uninstall the node host service
- Start/Stop with proper process lifecycle handling
- Option to hide the console window and capture output to a local log file

### Exec Approvals
- In-app UI for reviewing and approving exec requests
- Local history and policy controls

### Canvas
- Embedded Canvas via WebView2
- Snapshots and JS eval support

### Screen Capture
- Screen capture with lightweight recording
- Visible indicator when recording is active

### Chrome Relay
- Configure relay port
- Verify relay connectivity with guided verification
- Quick access to chrome://extensions and extension folder

### Logs & Diagnostics
- View node host output directly in the app (last 200 lines)
- Open logs folder
- **Export diagnostics**: Creates a ZIP bundle with redacted config and all log files

### Context-Aware Banners
- Token missing? Banner prompts you to add it in settings
- Pairing required? Banner links to Control UI for device approval

### CLI
```
openclaw-win status
openclaw-win configure --host <gateway-host> --port 443 --tls --token <gateway-token> --display-name "My Windows Node"
openclaw-win configure --system-theme | --dark-theme | --light-theme
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

## Repo Layout
- `src/OpenClaw.Win.Core` — Core services (config, token storage, node status, gateway tests)
- `src/OpenClaw.Win.Cli` — CLI entry (`openclaw-win.exe`)
- `src/OpenClaw.Win.App` — WPF tray app and settings UI

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

## Settings & Data Locations
The companion stores data under:
`%AppData%\OpenClaw\WindowsCompanion`

Files:
- `config.json` — connection and UI settings
- `token.dat` — gateway token (DPAPI protected)
- `logs/app.log` and `logs/node.log`
- `logs/node-host.log` — captured node host output (when enabled)
- `diagnostics/` — exported diagnostic bundles

### Preconfigured settings
If `personal-config.json` exists alongside the tray app binary, the app will seed
`config.json` on first launch. Use `personal-config.example.json` as a template
and keep your personal `personal-config.json` untracked.

## Security Notes
- Gateway token is stored using Windows DPAPI (CurrentUser scope)
- Tokens are never written to logs
- Diagnostics export redacts secrets

## Status Definitions
- **Connected**: node host is running and connected to the gateway
- **Degraded**: node host is running but gateway is unreachable or disconnected
- **Disconnected**: node host is stopped
- **Error**: configuration or authentication error detected

## OpenClaw Integration Notes
The companion uses the upstream CLI for lifecycle management:
- `openclaw node install` to set up background runner
- `openclaw node restart` to connect
- `openclaw node stop` to disconnect
- `openclaw node status --json` for status

## Troubleshooting
- Run `openclaw-win doctor` for a quick environment check
- Ensure `openclaw` is on PATH (`where openclaw`)
- If status shows pairing required, approve the **device** in the gateway UI or via `openclaw devices list` / `openclaw devices approve <requestId>`
- Pairing for the Control UI does **not** automatically pair the Windows CLI device — the app needs its own device approval

## What's Next
- Capture sensors (camera/location/SMS) once permissions UX is defined
