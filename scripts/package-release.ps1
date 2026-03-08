param(
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-VersionFromProps([string]$PropsPath) {
    [xml]$props = Get-Content -Path $PropsPath -Raw
    $version = $props.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "No <Version> found in $PropsPath"
    }

    return $version.Trim()
}

$repoRoot = Get-RepoRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-VersionFromProps -PropsPath (Join-Path $repoRoot "Directory.Build.props")
}

$distRoot = Join-Path $repoRoot "dist"
$publishRoot = Join-Path $repoRoot "artifacts\release"
$releaseName = "OpenClawWindowsCompanion-v$Version-$RuntimeIdentifier"
$stagingDir = Join-Path $distRoot $releaseName
$zipPath = Join-Path $distRoot "$releaseName.zip"
$checksumPath = "$zipPath.sha256"
$appPublishDir = Join-Path $publishRoot "app"
$cliPublishDir = Join-Path $publishRoot "cli"

Write-Host "Packaging $releaseName"

foreach ($path in @($stagingDir, $zipPath, $checksumPath, $appPublishDir, $cliPublishDir)) {
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $stagingDir | Out-Null
New-Item -ItemType Directory -Path $appPublishDir | Out-Null
New-Item -ItemType Directory -Path $cliPublishDir | Out-Null

Push-Location $repoRoot
try {
    dotnet publish .\src\OpenClaw.Win.App\OpenClaw.Win.App.csproj -c $Configuration -r $RuntimeIdentifier --self-contained false -o $appPublishDir
    dotnet publish .\src\OpenClaw.Win.Cli\OpenClaw.Win.Cli.csproj -c $Configuration -r $RuntimeIdentifier --self-contained false -o $cliPublishDir
}
finally {
    Pop-Location
}

Copy-Item -Path (Join-Path $appPublishDir '*') -Destination $stagingDir -Recurse -Force
Copy-Item -Path (Join-Path $cliPublishDir '*') -Destination $stagingDir -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot 'README.md') -Destination $stagingDir -Force
Copy-Item -Path (Join-Path $repoRoot 'LICENSE') -Destination $stagingDir -Force
Copy-Item -Path (Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $stagingDir -Force

Compress-Archive -Path $stagingDir -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $checksumPath -Value "$hash  $([System.IO.Path]::GetFileName($zipPath))"

Write-Host "Created archive: $zipPath"
Write-Host "Created checksum: $checksumPath"
