@echo off
setlocal
title RoomToRentRadar - One-Click Dev Setup
echo ========================================================
echo    RoomToRentRadar - Khoi Tao Moi Truong Cho Nguoi Moi
echo ========================================================
echo.
echo [1/3] Cai dat User Secrets cho Backend (.NET Secrets)...
for /f "delims=" %%s in ('powershell -NoProfile -Command "$b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)"') do set "JWT_SECRET=%%s"
dotnet user-secrets set "Jwt:Secret" "%JWT_SECRET%" --project src/RoomToRentRadar.Bootstrap
dotnet user-secrets set "CosmosDb:RoomsContainerName" "RoomDetails" --project src/RoomToRentRadar.Bootstrap
echo       -> Da tao ngau nhien JWT Secret (32 bytes) va luu secrets cuc bo (Khong bi day len Git).
echo.
echo [2/3] Kiem tra va thiet lap cau noi USB cho thiet bi Android (adb reverse)...
where adb >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    adb reverse tcp:5000 tcp:5000 >nul 2>nul
    adb reverse tcp:5000 tcp:49966 >nul 2>nul
    adb reverse tcp:49966 tcp:49966 >nul 2>nul
    echo       -> Da thiet lap cau noi qua adb port 5000 va 49966.
) else (
    if exist "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" (
        "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:5000 tcp:5000 >nul 2>nul
        "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:5000 tcp:49966 >nul 2>nul
        "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:49966 tcp:49966 >nul 2>nul
        echo       -> Da thiet lap cau noi qua Android SDK platform-tools.
    ) else (
        echo       -> Chua phat hien adb (Neu chay tren Windows thi khong can buoc nay).
    )
)
echo.
echo [3/3] Kiem tra cau hinh User Secrets da luu:
dotnet user-secrets list --project src/RoomToRentRadar.Bootstrap
echo.
echo ========================================================
echo   HOAN TAT!
echo   Ban chi can mo Visual Studio:
echo   1. Chon Startup Project la 'RoomToRentRadar.Bootstrap' -> Bam F5 chay Backend.
echo   2. Chay Mobile App 'RoomToRentRadar' tren May ao hoac Dien thoai.
echo ========================================================
echo.
pause
