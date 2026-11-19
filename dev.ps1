param (
    [ValidateSet("windows", "android")]
    [string]$Platform = "windows"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ROOMTORENTRADAR FULL-STACK RUNNER" -ForegroundColor Cyan
Write-Host "  Platform: $Platform | API + Client" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Tắt tiến trình Client cũ để tránh xung đột file lock
Stop-Process -Name "RoomToRentRadar" -Force -ErrorAction SilentlyContinue

# 2. Kiểm tra xem Backend API đã chạy sẵn chưa
$apiRunning = $false
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/api/health" -TimeoutSec 2 -ErrorAction Stop
    if ($health.status -eq "Healthy") {
        $apiRunning = $true
        Write-Host "-> [API] RoomToRentRadar.Api ĐÃ ĐANG CHẠY tại http://localhost:5000" -ForegroundColor Green
    }
} catch {
    $apiRunning = $false
}

# Nếu API chưa chạy, tự động bật API ở một cửa sổ Terminal riêng
if (-not $apiRunning) {
    Write-Host "-> [API] Đang tự động khởi động RoomToRentRadar.Api..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '=== BACKEND WEB API (RoomToRentRadar.Api) ===' -ForegroundColor Green; dotnet run --project RoomToRentRadar.Api/RoomToRentRadar.Api.csproj --launch-profile http"
    
    # Chờ API sẵn sàng (Health check)
    $retries = 20
    while ($retries -gt 0) {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:5000/api/health" -TimeoutSec 2 -ErrorAction Stop
            if ($health.status -eq "Healthy") {
                Write-Host "-> [API] Backend đã sẵn sàng (Status: Healthy)!" -ForegroundColor Green
                break
            }
        } catch {
            $retries--
            Write-Host "-> [API] Đang chờ backend khởi động ($retries)..." -ForegroundColor Gray
        }
    }
}

# 3. Khởi chạy Ứng dụng Client MAUI
if ($Platform -eq "windows") {
    Write-Host "-> [CLIENT] Đang khởi chạy ứng dụng Windows App..." -ForegroundColor Cyan
    dotnet run --project RoomToRentRadar/RoomToRentRadar.csproj -f net10.0-windows10.0.19041.0
} else {
    Write-Host "-> [CLIENT] Đang cài đặt và chạy ứng dụng Android..." -ForegroundColor Cyan
    dotnet build -t:Run -f net10.0-android RoomToRentRadar/RoomToRentRadar.csproj
}

