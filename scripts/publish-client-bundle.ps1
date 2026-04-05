param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$IncludeApi
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot "artifacts"
$bundleRoot = Join-Path $artifactsRoot "client-bundle"
$desktopOut = Join-Path $bundleRoot "Desktop"
$docsOut = Join-Path $bundleRoot "Docs"

if (Test-Path $bundleRoot) {
    Remove-Item $bundleRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $desktopOut -Force | Out-Null
New-Item -ItemType Directory -Path $docsOut -Force | Out-Null

$desktopProject = Join-Path $repoRoot "src/POS.Desktop/POS.Desktop.csproj"
Write-Host "Publishing desktop executable..."

dotnet publish $desktopProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishReadyToRun=true `
    -o $desktopOut

if ($IncludeApi) {
    $apiOut = Join-Path $bundleRoot "API"
    New-Item -ItemType Directory -Path $apiOut -Force | Out-Null

    $apiProject = Join-Path $repoRoot "src/POS.API/POS.API.csproj"
    Write-Host "Publishing API executable..."

    dotnet publish $apiProject `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:PublishReadyToRun=true `
        -o $apiOut

    Copy-Item (Join-Path $repoRoot "src/POS.API/appsettings.json") (Join-Path $apiOut "appsettings.json") -Force
    Copy-Item (Join-Path $repoRoot "src/POS.API/appsettings.Development.json") (Join-Path $apiOut "appsettings.Development.json") -Force
}

Copy-Item (Join-Path $repoRoot "docs/OFFLINE_EXE_DEPLOYMENT.md") (Join-Path $docsOut "OFFLINE_EXE_DEPLOYMENT.md") -Force
Copy-Item (Join-Path $repoRoot "docs/PRODUCTION_READINESS_CHECKLIST.md") (Join-Path $docsOut "PRODUCTION_READINESS_CHECKLIST.md") -Force
Copy-Item (Join-Path $repoRoot "scripts/configure-pos-client.ps1") (Join-Path $bundleRoot "configure-pos-client.ps1") -Force
Copy-Item (Join-Path $repoRoot "scripts/clear-pos-seed-vars.ps1") (Join-Path $bundleRoot "clear-pos-seed-vars.ps1") -Force

$launcherPath = Join-Path $bundleRoot "Start-POS-Desktop.cmd"
$launcherContent = @"
@echo off
cd /d %~dp0Desktop
start "POS Desktop" POS.Desktop.exe
"@
Set-Content -Path $launcherPath -Value $launcherContent

Write-Host ""
Write-Host "Client bundle created at: $bundleRoot"
Write-Host "Desktop executable: $desktopOut"
if ($IncludeApi) {
    Write-Host "API executable: $(Join-Path $bundleRoot 'API')"
}
