@echo off
chcp 65001 >nul
echo ========================================
echo Building SINGLE FILE - Pikuniku v2.0
echo ========================================
echo.

REM Очистка
echo [1/4] Cleaning...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "Pikuniku_dance_clicker" rmdir /s /q "Pikuniku_dance_clicker"
echo Done.
echo.

REM Восстановление
echo [2/4] Restoring...
dotnet restore -r win-x64
if %ERRORLEVEL% NEQ 0 (
    echo Restore failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Сборка SINGLE FILE с явным указанием платформы
echo [3/4] Building single-file executable...
dotnet publish -c Pikuniku_dance_clicker -r win-x64 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:PlatformTarget=x64 ^
    /p:RuntimeIdentifier=win-x64 ^
    /p:PublishTrimmed=false ^
    /p:DebugType=none ^
    /p:DebugSymbols=false

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Копирование
echo [4/4] Creating Pikuniku_dance_clicker folder...
mkdir Pikuniku_dance_clicker
copy "bin\Pikuniku_dance_clicker\net8.0-windows\win-x64\publish\Pikuniku_dance_clicker_v2.0.exe" "Pikuniku_dance_clicker\" >nul
copy "logsettings.json" "Pikuniku_dance_clicker\" >nul
xcopy "Assets" "Pikuniku_dance_clicker\Assets\" /E /I /Y >nul

REM README
(
echo ========================================
echo Pikuniku Dance Clicker v2.0
echo ========================================
echo.
echo SYSTEM REQUIREMENTS:
echo - Windows 10/11 ^(64-bit ONLY^)
echo - Screen resolution: 1920x1080 or higher
echo - No installations required!
echo.
echo HOW TO USE:
echo 1. Extract folder anywhere
echo 2. Run Pikuniku_dance_clicker_v2.0.exe
echo 3. Start dancing in Pikuniku
echo 4. Press START or Ctrl+Shift+A
echo.
echo HOTKEYS:
echo - Ctrl+Shift+A: Start/Stop
echo - Ctrl+Shift+Q: Exit
echo.
echo IMPORTANT:
echo - DO NOT delete Assets folder!
echo - All libraries embedded in .exe
echo - Logs saved in logs/ folder
echo.
echo TROUBLESHOOTING:
echo - If "Access Denied" - run as Administrator
echo - If antivirus blocks - add to exclusions
echo - Check logs/app-*.txt for errors
echo.
echo ========================================
) > "Pikuniku_dance_clicker\README.txt"

echo Done.
echo.

setlocal enabledelayedexpansion
for %%A in ("Pikuniku_dance_clicker\Pikuniku_dance_clicker_v2.0.exe") do (
    set size=%%~zA
    set /a sizeMB=!size! / 1048576
)

echo ========================================
echo BUILD SUCCESSFUL!
echo ========================================
echo.
echo Location: Pikuniku_dance_clicker\
echo EXE size: !sizeMB! MB
echo.
echo Contents:
dir /B Pikuniku_dance_clicker
echo.
echo Ready for distribution!
pause