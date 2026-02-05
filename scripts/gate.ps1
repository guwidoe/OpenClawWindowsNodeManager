param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Gate: build + tests ($Configuration)"
dotnet build .\OpenClawWindowsNodeManager.sln -c $Configuration
dotnet test .\OpenClawWindowsNodeManager.sln -c $Configuration
