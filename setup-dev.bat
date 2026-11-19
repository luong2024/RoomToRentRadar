@echo off
setlocal EnableDelayedExpansion
title RoomToRentRadar - One-Click Dev Setup
echo ========================================================
echo    RoomToRentRadar - Khoi Tao Moi Truong Cho Nguoi Moi
echo ========================================================
echo.
echo [1/4] Cai dat User Secrets cho Backend (.NET Secrets)...
for /f "delims=" %%s in ('powershell -NoProfile -Command "$b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)"') do set "JWT_SECRET=%%s"
dotnet user-secrets set "Jwt:Secret" "!JWT_SECRET!" --project src/RoomToRentRadar.Bootstrap
set "JWT_RES=!ERRORLEVEL!"
dotnet user-secrets set "CosmosDb:RoomsContainerName" "RoomDetails" --project src/RoomToRentRadar.Bootstrap
set "COSMOS_RES=!ERRORLEVEL!"

if !JWT_RES! EQU 0 if !COSMOS_RES! EQU 0 (
    echo       -^> Da tao ngau nhien JWT Secret (32 bytes) va luu secrets cuc bo thanh cong.
) else (
    echo       -^> [CANH BAO] Khong the luu User Secrets day du (ma loi !JWT_RES! / !COSMOS_RES!).
)

echo.
echo [2/4] Kiem tra cau hinh Google Maps API Key (secrets.props)...
if not exist "RoomToRentRadar\secrets.props" (
    if exist "RoomToRentRadar\secrets.props.example" (
        copy "RoomToRentRadar\secrets.props.example" "RoomToRentRadar\secrets.props" >nul
        echo       -^> Da tu dong tao file 'RoomToRentRadar\secrets.props' tu file mau.
        echo       -^> [CHU Y] Hay mo 'RoomToRentRadar\secrets.props' va thay YOUR-ACTUAL-GOOGLE-MAPS-API-KEY-HERE bang API Key that.
    ) else (
        echo       -^> [CANH BAO] Khong tim thay file mau 'RoomToRentRadar\secrets.props.example'.
    )
) else (
    findstr "YOUR-ACTUAL-GOOGLE-MAPS-API-KEY-HERE" "RoomToRentRadar\secrets.props" >nul 2>nul
    if !ERRORLEVEL! EQU 0 (
        echo       -^> [CHU Y] File 'RoomToRentRadar\secrets.props' dang dung key mau. Hay dien Google Maps API Key that.
    ) else (
        echo       -^> File 'RoomToRentRadar\secrets.props' da san sang.
    )
)

echo.
echo [3/4] Kiem tra va thiet lap cau noi USB cho thiet bi Android (adb reverse)...
set "ADB_EXE=adb"
where adb >nul 2>nul
if !ERRORLEVEL! NEQ 0 (
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
        if !ERRORLEVEL! EQU 0 (
            echo       -^> Da thiet lap cau noi qua adb port 49966 thanh cong.
        ) else (
            echo       -^> Lenh 'adb reverse' that bai ^(ma loi !ERRORLEVEL!^).
        )
    ) else (
        echo       -^> Khong tim thay thiet bi hoac may ao Android nao dang ket noi qua ADB.
        echo          ^(Luu y: Khi chay app tren may that, hay chay lenh: adb reverse tcp:49966 tcp:49966^)
    )
) else (
    echo       -^> Chua phat hien adb tren he thong ^(Neu chi chay tren Windows thi khong can buoc nay^).
)
echo.
echo [4/4] Kiem tra cau hinh User Secrets da luu:
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
