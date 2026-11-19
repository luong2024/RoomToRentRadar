# RoomToRentRadar - One-Click PowerShell Dev Setup
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   RoomToRentRadar - Khoi Tao Moi Truong Cho Nguoi Moi   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Cai dat User Secrets voi JWT Secret ngau nhien cho moi may (Chi tao moi khi chua ton tai)
Write-Host "[1/4] Kiem tra va cai dat User Secrets cho Backend (.NET Secrets)..." -ForegroundColor Yellow
$existingSecrets = dotnet user-secrets list --project src/RoomToRentRadar.Bootstrap 2>&1
$jwtExists = $existingSecrets -match "Jwt:Secret\s*="

if (-not $jwtExists) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $secretBytes = New-Object byte[] 32
    $rng.GetBytes($secretBytes)
    $generatedJwtSecret = [Convert]::ToBase64String($secretBytes)

    dotnet user-secrets set "Jwt:Secret" "$generatedJwtSecret" --project src/RoomToRentRadar.Bootstrap | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      -> Da tao ngau nhien JWT Secret (32 bytes cryptographically secure) va luu User Secrets." -ForegroundColor Green
    } else {
        Write-Host "      -> [CANH BAO] Khong the luu Jwt:Secret (Exit code: $LASTEXITCODE)." -ForegroundColor Yellow
    }
} else {
    Write-Host "      -> Jwt:Secret da ton tai trong User Secrets (giu nguyen gia tri de tranh mat hieu luc phien dang nhap)." -ForegroundColor Green
}

$cosmosExists = $existingSecrets -match "CosmosDb:RoomsContainerName\s*="
if (-not $cosmosExists) {
    dotnet user-secrets set "CosmosDb:RoomsContainerName" "RoomDetails" --project src/RoomToRentRadar.Bootstrap | Out-Null
}

# 2. Thiet lap file secrets.props cho Google Maps API Key
Write-Host ""
Write-Host "[2/4] Kiem tra cau hinh Google Maps API Key (secrets.props)..." -ForegroundColor Yellow
$secretsPropsPath = "RoomToRentRadar\secrets.props"
$secretsExamplePath = "RoomToRentRadar\secrets.props.example"

if (-not (Test-Path $secretsPropsPath)) {
    if (Test-Path $secretsExamplePath) {
        Copy-Item $secretsExamplePath $secretsPropsPath
        Write-Host "      -> Da tu dong tao file 'RoomToRentRadar\secrets.props' tu file mau." -ForegroundColor Green
        Write-Host "      -> [CHU Y] Hay mo 'RoomToRentRadar\secrets.props' va thay YOUR-ACTUAL-GOOGLE-MAPS-API-KEY-HERE bang API Key that de ban do Google Maps hien thi tren Android." -ForegroundColor Magenta
    } else {
        Write-Host "      -> [CANH BAO] Khong tim thay file mau '$secretsExamplePath'." -ForegroundColor Yellow
    }
} else {
    $content = Get-Content $secretsPropsPath -Raw
    if ($content -match "YOUR-ACTUAL-GOOGLE-MAPS-API-KEY-HERE" -or $content -match "<GoogleMapsApiKey>\s*</GoogleMapsApiKey>") {
        Write-Host "      -> [CHU Y] File 'RoomToRentRadar\secrets.props' dang chua key mac dinh. Hay dien Google Maps API Key that vao file nay." -ForegroundColor Magenta
    } else {
        Write-Host "      -> File 'RoomToRentRadar\secrets.props' da ton tai va duoc cau hinh." -ForegroundColor Green
    }
}

# 3. Thiet lap adb reverse cho Backend (port 49966)
Write-Host ""
Write-Host "[3/4] Kiem tra va thiet lap cau noi USB cho thiet bi Android (adb reverse)..." -ForegroundColor Yellow
$adbCommand = Get-Command adb -ErrorAction SilentlyContinue
$adbPath = if ($adbCommand) { $adbCommand.Source } else { $null }
if (-not $adbPath -and (Test-Path "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe")) {
    $adbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
}

if ($adbPath) {
    $devicesOutput = & $adbPath devices 2>&1
    $connectedDevices = $devicesOutput | Where-Object { $_ -match "\s+(device|emulator)$" }

    if ($connectedDevices) {
        & $adbPath reverse tcp:49966 tcp:49966 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      -> Da thiet lap cau noi qua adb port 49966 thanh cong." -ForegroundColor Green
        } else {
            Write-Host "      -> Lenh 'adb reverse' that bai voi ma loi $LASTEXITCODE." -ForegroundColor Yellow
        }
    } else {
        Write-Host "      -> Khong tim thay thiet bi Android nao dang ket noi qua ADB." -ForegroundColor Yellow
        Write-Host "         (Luu y: Khi chay app tren may that/may ao, hay chay lenh: adb reverse tcp:49966 tcp:49966)" -ForegroundColor Gray
    }
} else {
    Write-Host "      -> Chua phat hien adb tren he thong (Neu chi chay tren Windows thi khong can buoc nay)." -ForegroundColor Gray
}

# 4. Kiem tra trang thai cau hinh User Secrets (Bao mat: Khong in truc tiep gia tri secret ra terminal)
Write-Host ""
Write-Host "[4/4] Kiem tra trang thai cau hinh User Secrets:" -ForegroundColor Yellow
$verifySecrets = dotnet user-secrets list --project src/RoomToRentRadar.Bootstrap 2>&1
if ($verifySecrets -match "Jwt:Secret\s*=") {
    Write-Host "      -> Jwt:Secret: [DA CAU HINH] (Gia tri duoc an vi ly do an toan)" -ForegroundColor Green
} else {
    Write-Host "      -> Jwt:Secret: [CHUA CAU HINH]" -ForegroundColor Red
}
if ($verifySecrets -match "CosmosDb:RoomsContainerName\s*=") {
    Write-Host "      -> CosmosDb:RoomsContainerName: [DA CAU HINH]" -ForegroundColor Green
} else {
    Write-Host "      -> CosmosDb:RoomsContainerName: [CHUA CAU HINH]" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  HOAN TAT! " -ForegroundColor Green
Write-Host "  Ban chi can mo Visual Studio: "
Write-Host "  1. Chon Startup Project la 'RoomToRentRadar.Bootstrap' -> Bam F5 chay Backend."
Write-Host "  2. Chay Mobile App 'RoomToRentRadar' tren May ao hoac Dien thoai."
Write-Host "========================================================" -ForegroundColor Cyan
