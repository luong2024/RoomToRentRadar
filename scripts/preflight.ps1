<#
.SYNOPSIS
    Pre-flight verification script for Group 5 Developers & AI Agents before committing or opening a PR.
    Mirrors the 3-stage GitHub Actions CI pipeline locally.
#>
param(
    [switch]$FullMauiBuild
)

$ErrorActionPreference = "Stop"

Write-Host "=== [1/4] Checking Git Branch & Activating Hooks ===" -ForegroundColor Cyan
git config core.hooksPath .githooks
$branch = (git branch --show-current).Trim()
Write-Host "Current branch: $branch"

if ($branch -eq "main" -or $branch -eq "master") {
    Write-Error "FORBIDDEN: You are on '$branch'. Switch to feature/group-5/Txx.x-..."
    exit 1
}

Write-Host "=== [2/4] Static Scan for UI Deadlocks (.Result / .Wait()) & Service Locator ===" -ForegroundColor Cyan
$featureFiles = Get-ChildItem -Path "RoomToRentRadar\Features","RoomToRentRadar.Infrastructure\Repositories" -Recurse -Include *.cs -ErrorAction SilentlyContinue
if ($featureFiles) {
    $deadlocks = $featureFiles | Select-String -Pattern '\.(Result\b|Wait\s*\()'
    if ($deadlocks) {
        $deadlocks | ForEach-Object { Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" -ForegroundColor Red }
        Write-Error "Detected synchronous blocking calls (.Result or .Wait()) which can deadlock MAUI UI thread. Use async/await."
        exit 1
    }

    $locators = Get-ChildItem -Path "RoomToRentRadar\Features" -Recurse -Include *.cs -ErrorAction SilentlyContinue | Select-String -Pattern 'IServiceProvider|\.GetService<'
    if ($locators) {
        $locators | ForEach-Object { Write-Host "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" -ForegroundColor Red }
        Write-Error "Detected Service Locator (IServiceProvider.GetService) in Features/. Inject interfaces via constructor instead."
        exit 1
    }
}

Write-Host "=== [3/4] Building Core, Infrastructure & Running Contract/API Tests (30s Hang Timeout) ===" -ForegroundColor Cyan
dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj --nologo --blame-hang-timeout 30s
if ($LASTEXITCODE -ne 0) {
    Write-Error "Contract / Unit tests failed or timed out!"
    exit $LASTEXITCODE
}

if ($FullMauiBuild) {
    Write-Host "=== [4/4] Building Full MAUI Solution (Android & Windows + XAML SourceGen) ===" -ForegroundColor Cyan
    dotnet build RoomToRentRadar.slnx --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "MAUI solution build failed!"
        exit $LASTEXITCODE
    }
} else {
    Write-Host "=== [4/4] Skipping full MAUI build (pass -FullMauiBuild to verify Android/Windows XAML compilation) ===" -ForegroundColor Yellow
}

Write-Host "ALL CHECKS PASSED!" -ForegroundColor Green
