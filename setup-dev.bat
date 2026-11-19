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
set "ADB_EXE=adb"
where adb >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    if exist "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" (
        set "ADB_EXE=C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
    ) else (
        set "ADB_EXE="
    )
)

if defined ADB_EXE (
    set "DEVICE_FOUND="
    for /f "tokens=1,2" %%A in ('"%ADB_EXE%" devices 2^>nul') do (
        if "%%B"=="device" set "DEVICE_FOUND=1"
        if "%%B"=="emulator" set "DEVICE_FOUND=1"
    )
    if defined DEVICE_FOUND (
        "%ADB_EXE%" reverse tcp:49966 tcp:49966 >nul 2>nul
        if %ERRORLEVEL% EQU 0 (
            echo       -^> Da thiet lap cau noi qua adb port 49966 thanh cong.
        ) else (
            echo       -^> Lenh 'adb reverse' that bai ^(ma loi %ERRORLEVEL%^).
        )
    ) else (
        echo       -^> Khong tim thay thiet bi hoac may ao Android nao dang ket noi qua ADB.
        echo          ^(Luu y: Khi chay app tren may that, hay chay lenh: adb reverse tcp:49966 tcp:49966^)
    )
) else (
    echo       -^> Chua phat hien adb tren he thong ^(Neu chi chay tren Windows thi khong can buoc nay^).
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
