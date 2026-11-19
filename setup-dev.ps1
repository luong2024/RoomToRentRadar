# RoomToRentRadar - One-Click PowerShell Dev Setup
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   RoomToRentRadar - Khoi Tao Moi Truong Cho Nguoi Moi   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Cai dat User Secrets voi JWT Secret ngau nhien cho moi may
Write-Host "[1/3] Cai dat User Secrets cho Backend (.NET Secrets)..." -ForegroundColor Yellow
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$secretBytes = New-Object byte[] 32
$rng.GetBytes($secretBytes)
$generatedJwtSecret = [Convert]::ToBase64String($secretBytes)

dotnet user-secrets set "Jwt:Secret" "$generatedJwtSecret" --project src/RoomToRentRadar.Bootstrap
dotnet user-secrets set "CosmosDb:RoomsContainerName" "RoomDetails" --project src/RoomToRentRadar.Bootstrap
Write-Host "      -> Da tao ngau nhien JWT Secret (32 bytes cryptographically secure) va luu secrets cuc bo." -ForegroundColor Green

# 2. Thiet lap adb reverse
Write-Host ""
Write-Host "[2/3] Kiem tra va thiet lap cau noi USB cho thiet bi Android (adb reverse)..." -ForegroundColor Yellow
$adbCommand = Get-Command adb -ErrorAction SilentlyContinue
$adbPath = if ($adbCommand) { $adbCommand.Source } else { $null }
if (-not $adbPath -and (Test-Path "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe")) {
    $adbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
}

if ($adbPath) {
    & $adbPath reverse tcp:5000 tcp:5000 2>$null
    & $adbPath reverse tcp:5000 tcp:49966 2>$null
    & $adbPath reverse tcp:49966 tcp:49966 2>$null
    Write-Host "      -> Da thiet lap cau noi qua adb port 5000 va 49966." -ForegroundColor Green
} else {
    Write-Host "      -> Chua phat hien adb (Neu chay tren Windows thi khong can buoc nay)." -ForegroundColor Gray
}

# 3. Kiem tra cau hinh
Write-Host ""
Write-Host "[3/3] Kiem tra cau hinh User Secrets da luu:" -ForegroundColor Yellow
dotnet user-secrets list --project src/RoomToRentRadar.Bootstrap

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  HOAN TAT! " -ForegroundColor Green
Write-Host "  Ban chi can mo Visual Studio: "
Write-Host "  1. Chon Startup Project la 'RoomToRentRadar.Bootstrap' -> Bam F5 chay Backend."
Write-Host "  2. Chay Mobile App 'RoomToRentRadar' tren May ao hoac Dien thoai."
Write-Host "========================================================" -ForegroundColor Cyan
