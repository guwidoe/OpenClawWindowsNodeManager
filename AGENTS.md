# AGENTS.md

@C:/Repositories/vibe-setup/AGENTS.windows.md

## Project: OpenClaw Windows Node Companion

A native Windows companion for running an OpenClaw node host with a tray app + scriptable CLI. Wraps (does not replace) the upstream `openclaw` CLI.

## Stack

- C# / .NET 8 (`net8.0-windows`)
- WPF (tray app + settings UI)
- xUnit (+ `Microsoft.NET.Test.Sdk`, `coverlet.collector`)
- External dependency: upstream `openclaw` CLI on `PATH`

## Structure

- `src/OpenClaw.Win.Core/` - core services (config, token storage, status, gateway tests)
- `src/OpenClaw.Win.App/` - WPF tray app + settings UI
- `src/OpenClaw.Win.Cli/` - CLI entry (`openclaw-win.exe`)
- `src/OpenClaw.Win.Core.Tests/` - unit tests
- `dist/` - packaged release artifacts
- `.github/workflows/` - CI

## Commands

```powershell
# Restore
dotnet restore .\\OpenClawWindowsNodeManager.sln

# Gate (build + tests) - must pass after each completed working packet
.\scripts\gate.ps1

# Build (Release)
dotnet build .\\OpenClawWindowsNodeManager.sln -c Release

# Test
dotnet test .\\OpenClawWindowsNodeManager.sln -c Release

# Run tray app (after building)
.\\src\\OpenClaw.Win.App\\bin\\Release\\net8.0-windows\\OpenClaw.Win.App.exe

# Run CLI (after building)
.\\src\\OpenClaw.Win.Cli\\bin\\Release\\net8.0-windows\\openclaw-win.exe status
```

## Key Notes

- Companion data lives under `%AppData%\\OpenClaw\\WindowsCompanion` (DPAPI-protected token storage).
- `openclaw` integration uses: `openclaw node install|restart|stop|status --json`.
