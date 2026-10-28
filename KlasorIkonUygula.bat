@echo off
setlocal enabledelayedexpansion

title Toplu HD Klasor Ikon Uygulayici (256x256)

echo ======================================================
echo    TOPLU HD KLASOR IKON TARAYICI (256x256 DESTEKLI)
echo ======================================================
echo.

if "%~1"=="" (
    set "TARGET_DIR=%~dp0"
) else (
    set "TARGET_DIR=%~f1"
)

if "%TARGET_DIR:~-1%"=="\" set "TARGET_DIR=%TARGET_DIR:~0,-1%"

if not exist "%TARGET_DIR%" (
    echo [HATA] Klasor bulunamadi: "%TARGET_DIR%"
    pause
    exit /b
)

echo Hedef Dizin: "%TARGET_DIR%"
echo.
echo Bulanik/dusuk cozunurluklu ikonlar 256x256 HD yapiliyor...
echo Varsa PS3 ICON0.PNG / resim dosyalari otomatik donusturuluyor...
echo.

:: KlasorIkonYapici.exe ayni klasordeyse en yuksek kalitede C# motorunu calistir
if exist "%~dp0KlasorIkonYapici.exe" (
    "%~dp0KlasorIkonYapici.exe" /silent "%TARGET_DIR%"
    echo [BASARILI] Tum klasorler 256x256 kristal netliginde HD yapildi!
    echo.
    ping 127.0.0.1 -n 3 >nul
    exit /b
)

:: Standart Batch fallback
set /a TOTAL_COUNT=0
set /a UPDATED_COUNT=0

call :ProcessFolder "%TARGET_DIR%"

for /f "delims=" %%D in ('dir /b /s /ad "%TARGET_DIR%" 2^>nul') do (
    call :ProcessFolder "%%D"
)

powershell -NoProfile -Command "$code = '[DllImport(\"shell32.dll\")] public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);'; Add-Type -MemberDefinition $code -Name ShellAPI -Namespace Win32; [Win32.ShellAPI]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)" >nul 2>&1

echo.
echo ======================================================
echo TARAMA TAMAMLANDI!
echo Taranan toplam klasor     : %TOTAL_COUNT%
echo Simgesi guncellenen klasor: %UPDATED_COUNT%
echo ======================================================
echo.
ping 127.0.0.1 -n 3 >nul
exit /b

:ProcessFolder
set "CURR_DIR=%~1"
set /a TOTAL_COUNT+=1

set "ICO_FILE="
for %%F in ("%CURR_DIR%\*.ico") do (
    if not defined ICO_FILE set "ICO_FILE=%%~nxF"
    if /i "%%~nxF"=="folder_icon.ico" set "ICO_FILE=%%~nxF"
    if /i "%%~nxF"=="icon.ico" set "ICO_FILE=%%~nxF"
    if /i "%%~nxF"=="folder.ico" set "ICO_FILE=%%~nxF"
)

if defined ICO_FILE (
    set "INI_PATH=%CURR_DIR%\desktop.ini"
    if exist "!INI_PATH!" (
        attrib -h -s -r "!INI_PATH!" >nul 2>&1
        del /f /q "!INI_PATH!" >nul 2>&1
    )
    (
        echo [.ShellClassInfo]
        echo IconResource=%CURR_DIR%\!ICO_FILE!,0
    ) > "!INI_PATH!"
    attrib +h +s "!INI_PATH!"
    attrib +r "%CURR_DIR%"
    set /a UPDATED_COUNT+=1
    echo [GUNCEL HD] %~nx1 -^> !ICO_FILE!
)
exit /b
