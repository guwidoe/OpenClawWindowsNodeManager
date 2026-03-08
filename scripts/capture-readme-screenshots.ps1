param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\docs\images'),
    [string]$WorkDir = (Join-Path $PSScriptRoot '..\artifacts\readme-screenshots')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class ScreenshotNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
}
"@

function Write-TextFile {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Initialize-DemoState {
    param(
        [string]$StateDir,
        [string]$FakeCliDir
    )

    $logsDir = Join-Path $StateDir 'logs'
    [System.IO.Directory]::CreateDirectory($StateDir) | Out-Null
    [System.IO.Directory]::CreateDirectory($logsDir) | Out-Null
    [System.IO.Directory]::CreateDirectory($FakeCliDir) | Out-Null

    $config = @'
{
  "gatewayHost": "demo-gateway.openclaw.local",
  "gatewayPort": 443,
  "useTls": true,
  "tlsFingerprint": "sha256:3A:9F:16:72:4C:12:AA:DE:77:11:4E:2B:09:91:DE:7A",
  "displayName": "OpenClaw Desk",
  "controlUiUrl": "https://demo-gateway.openclaw.local/ui",
  "relayPort": 18792,
  "pollIntervalSeconds": 5,
  "autoStartTray": false,
  "captureNodeHostOutput": true,
  "themePreference": "dark",
  "enableTrayNotifications": false,
  "enableSystemNotifications": false,
  "execApprovalPolicy": "prompt",
  "sshHost": "demo-gateway",
  "sshUser": "openclaw",
  "sshPort": 22,
  "sshCommand": "openclaw dashboard --no-open"
}
'@
    Write-TextFile -Path (Join-Path $StateDir 'config.json') -Content $config

    $nodeHostLog = @'
[2026-03-07 19:37:11] booting hidden node host
[2026-03-07 19:37:12] relay listening on 127.0.0.1:18792
[2026-03-07 19:37:14] gateway handshake ok
[2026-03-07 19:37:15] screen capture ready
[2026-03-07 19:37:16] canvas snapshot worker ready
[2026-03-07 19:37:17] connected as OpenClaw Desk
'@
    Write-TextFile -Path (Join-Path $logsDir 'node-host.log') -Content $nodeHostLog
    Write-TextFile -Path (Join-Path $logsDir 'app.log') -Content "[info] readme screenshot session`r`n"
    Write-TextFile -Path (Join-Path $logsDir 'node.log') -Content "[info] node session ok`r`n"

    $approvalHistory = @'
{"id":"req-1001","command":"system.run","arguments":"npm run build","requestedBy":"Canvas","requestedAt":"2026-03-07T19:35:02Z","decidedAt":"2026-03-07T19:35:07Z","decision":"approved","policy":"prompt","reason":"Update desktop shell"}
{"id":"req-1002","command":"system.run","arguments":"git push origin feature/ui-snapshots","requestedBy":"Automation","requestedAt":"2026-03-07T19:36:11Z","decidedAt":"2026-03-07T19:36:20Z","decision":"denied","policy":"prompt","reason":"Manual review required"}
'@
    Write-TextFile -Path (Join-Path $StateDir 'exec-approvals.log') -Content $approvalHistory

    $fakeCli = @'
@echo off
set args=%*
if /I "%args%"=="node status --json" (
  echo {"installed":true,"running":true,"connected":true,"gatewayHost":"demo-gateway.openclaw.local","gatewayPort":443,"displayName":"OpenClaw Desk","lastConnectedAt":"2026-03-07T19:37:17Z"}
  exit /b 0
)
if /I "%args%"=="nodes status --json" (
  echo {"nodes":[{"id":"win-node-01","displayName":"OpenClaw Desk","connected":true}]}
  exit /b 0
)
echo {"ok":true}
exit /b 0
'@
    Write-TextFile -Path (Join-Path $FakeCliDir 'openclaw.cmd') -Content $fakeCli
}

function Wait-ForMainWindow {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "OpenClaw.Win.App exited before the window appeared."
        }

        if ($Process.MainWindowHandle -ne 0) {
            return $Process.MainWindowHandle
        }

        Start-Sleep -Milliseconds 250
    }

    throw 'Timed out waiting for the OpenClaw window.'
}

function Capture-Window {
    param(
        [IntPtr]$Handle,
        [string]$Path
    )

    $DWMWA_EXTENDED_FRAME_BOUNDS = 9
    $rect = New-Object ScreenshotNative+RECT
    [void][ScreenshotNative]::ShowWindow($Handle, 9)
    [void][ScreenshotNative]::SetWindowPos($Handle, [IntPtr]::Zero, 80, 80, 1180, 820, 0)
    [void][ScreenshotNative]::SetForegroundWindow($Handle)
    Start-Sleep -Milliseconds 1200

    $dwmResult = [ScreenshotNative]::DwmGetWindowAttribute(
        $Handle,
        $DWMWA_EXTENDED_FRAME_BOUNDS,
        [ref]$rect,
        [System.Runtime.InteropServices.Marshal]::SizeOf([type][ScreenshotNative+RECT]))

    if ($dwmResult -ne 0) {
        if (-not [ScreenshotNative]::GetWindowRect($Handle, [ref]$rect)) {
            throw 'Failed to read window bounds.'
        }
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::White)
    $hdc = $graphics.GetHdc()

    try {
        $PW_RENDERFULLCONTENT = 2
        $printed = [ScreenshotNative]::PrintWindow($Handle, $hdc, $PW_RENDERFULLCONTENT)
    }
    finally {
        $graphics.ReleaseHdc($hdc)
    }

    if (-not $printed) {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    }

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Start-DemoRelayServer {
    param([int]$Port)

    return Start-Job -ArgumentList $Port -ScriptBlock {
        param($Port)

        $listener = [System.Net.Sockets.TcpListener]::Create($Port)
        $listener.Start()

        try {
            while ($true) {
                $client = $listener.AcceptTcpClient()
                try {
                    $stream = $client.GetStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    while ($true) {
                        $line = $reader.ReadLine()
                        if ($null -eq $line -or $line.Length -eq 0) {
                            break
                        }
                    }

                    $response = "HTTP/1.1 200 OK`r`nContent-Type: text/plain`r`nContent-Length: 2`r`nConnection: close`r`n`r`nOK"
                    $buffer = [System.Text.Encoding]::ASCII.GetBytes($response)
                    $stream.Write($buffer, 0, $buffer.Length)
                    $stream.Flush()
                }
                finally {
                    $client.Close()
                }
            }
        }
        finally {
            $listener.Stop()
        }
    }
}

function Stop-UiProcesses {
    Get-Process OpenClaw.Win.App -ErrorAction SilentlyContinue | Stop-Process -Force
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputDirPath = (Resolve-Path $OutputDir -ErrorAction SilentlyContinue)
if ($null -eq $outputDirPath) {
    [System.IO.Directory]::CreateDirectory((Resolve-Path $repoRoot).Path + '\docs\images') | Out-Null
    $OutputDir = (Resolve-Path (Join-Path $repoRoot 'docs\images')).Path
}
else {
    $OutputDir = $outputDirPath.Path
}

$WorkDir = (Resolve-Path $WorkDir -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })
if ([string]::IsNullOrWhiteSpace($WorkDir)) {
    $WorkDir = (Join-Path $repoRoot 'artifacts\readme-screenshots')
}

$stateDir = Join-Path $WorkDir 'state'
$fakeCliDir = Join-Path $WorkDir 'fake-cli'
$cliExe = Join-Path $repoRoot 'artifacts\bin\OpenClaw.Win.Cli\release\openclaw-win.exe'
$appExe = Join-Path $repoRoot 'artifacts\bin\OpenClaw.Win.App\release\OpenClaw.Win.App.exe'
$fakeCliPath = Join-Path $fakeCliDir 'openclaw.cmd'

if (-not (Test-Path $cliExe)) {
    throw "CLI not found at $cliExe"
}

if (-not (Test-Path $appExe)) {
    throw "App not found at $appExe"
}

Initialize-DemoState -StateDir $stateDir -FakeCliDir $fakeCliDir
Stop-UiProcesses

$originalStateDir = $env:OPENCLAW_COMPANION_STATE_DIR
$originalAppPath = $env:OPENCLAW_APP_PATH
$originalCliPath = $env:OPENCLAW_CLI_PATH
$originalPath = $env:PATH
$relayJob = $null

try {
    $env:OPENCLAW_COMPANION_STATE_DIR = $stateDir
    $env:OPENCLAW_APP_PATH = $appExe
    $env:OPENCLAW_CLI_PATH = $fakeCliPath
    $env:PATH = "$fakeCliDir;$originalPath"

    [void](& $cliExe configure --token 'demo-token-for-readme')
    $relayJob = Start-DemoRelayServer -Port 18792
    Start-Sleep -Milliseconds 500

    $captures = @(
        @{ Tab = 'connection'; File = 'ui-connection-dark.png'; DelayMilliseconds = 1000 },
        @{ Tab = 'node-host'; File = 'ui-node-host-dark.png'; DelayMilliseconds = 1000 },
        @{ Tab = 'canvas'; File = 'ui-canvas-dark.png'; DelayMilliseconds = 2500 },
        @{ Tab = 'approvals'; File = 'ui-approvals-dark.png'; DelayMilliseconds = 1000 },
        @{ Tab = 'chrome-relay'; File = 'ui-chrome-relay-dark.png'; DelayMilliseconds = 1200 },
        @{ Tab = 'logs'; File = 'ui-logs-dark.png'; DelayMilliseconds = 1000 }
    )

    foreach ($capture in $captures) {
        Stop-UiProcesses
        [void](& $cliExe ui show --tab $capture.Tab)
        Start-Sleep -Milliseconds 750
        $process = Get-Process OpenClaw.Win.App -ErrorAction Stop | Sort-Object StartTime -Descending | Select-Object -First 1
        $handle = Wait-ForMainWindow -Process $process
        Start-Sleep -Milliseconds $capture.DelayMilliseconds
        $targetPath = Join-Path $OutputDir $capture.File
        Capture-Window -Handle $handle -Path $targetPath
        Stop-UiProcesses
    }
}
finally {
    $env:OPENCLAW_COMPANION_STATE_DIR = $originalStateDir
    $env:OPENCLAW_APP_PATH = $originalAppPath
    $env:OPENCLAW_CLI_PATH = $originalCliPath
    $env:PATH = $originalPath
    if ($null -ne $relayJob) {
        Stop-Job $relayJob -ErrorAction SilentlyContinue
        Remove-Job $relayJob -ErrorAction SilentlyContinue
    }
    Stop-UiProcesses
}

Write-Host "Saved screenshots to $OutputDir"
