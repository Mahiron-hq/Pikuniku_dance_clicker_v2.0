@echo off
chcp 65001 >nul
echo ========================================
echo Universal Build - Pikuniku v2.0
echo Building for x86 AND x64
echo ========================================
echo.

REM Очистка всех предыдущих сборок
echo [0/7] Cleaning all previous builds...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "Pikuniku_dance_clicker_x64" rmdir /s /q "Pikuniku_dance_clicker_x64"
if exist "Pikuniku_dance_clicker_x86" rmdir /s /q "Pikuniku_dance_clicker_x86"
echo Done.
echo.

REM ========================================
REM СБОРКА x64 ВЕРСИИ
REM ========================================
echo ========================================
echo Building x64 (64-bit) version...
echo ========================================
echo.

echo [1/7] Restoring dependencies for x64...
dotnet restore -r win-x64
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: x64 restore failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo [2/7] Building x64 single-file executable...
dotnet publish -c Release -r win-x64 --self-contained true ^
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
    echo ERROR: x64 build failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo [3/7] Creating x64 release folder...
mkdir Pikuniku_dance_clicker_x64

REM Проверяем существование .exe файла
if not exist "bin\Release\net8.0-windows\win-x64\publish\*.exe" (
    echo ERROR: x64 .exe file not found!
    echo Looking in: bin\Release\net8.0-windows\win-x64\publish\
    dir "bin\Release\net8.0-windows\win-x64\publish\" /B
    pause
    exit /b 1
)

REM Копируем exe с любым именем
for %%F in ("bin\Release\net8.0-windows\win-x64\publish\*.exe") do (
    copy "%%F" "Pikuniku_dance_clicker_x64\" >nul
    echo   Copied: %%~nxF
)

copy "logsettings.json" "Pikuniku_dance_clicker_x64\" >nul
xcopy "Assets" "Pikuniku_dance_clicker_x64\Assets\" /E /I /Y >nul
echo Done.
echo.

REM ========================================
REM СБОРКА x86 ВЕРСИИ
REM ========================================
echo ========================================
echo Building x86 (32-bit) version...
echo ========================================
echo.

echo [4/7] Cleaning intermediate files...
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "obj" rmdir /s /q "obj"
echo Done.
echo.

echo [5/7] Restoring dependencies for x86...
dotnet restore -r win-x86
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: x86 restore failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo [6/7] Building x86 single-file executable...
dotnet publish -c Release -r win-x86 --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:PlatformTarget=x86 ^
    /p:RuntimeIdentifier=win-x86 ^
    /p:PublishTrimmed=false ^
    /p:DebugType=none ^
    /p:DebugSymbols=false

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: x86 build failed!
    pause
    exit /b 1
)
echo Done.
echo.

echo [7/7] Creating x86 release folder...
mkdir Pikuniku_dance_clicker_x86

REM Проверяем существование .exe файла
if not exist "bin\Release\net8.0-windows\win-x86\publish\*.exe" (
    echo ERROR: x86 .exe file not found!
    echo Looking in: bin\Release\net8.0-windows\win-x86\publish\
    dir "bin\Release\net8.0-windows\win-x86\publish\" /B
    pause
    exit /b 1
)

REM Копируем exe с любым именем
for %%F in ("bin\Release\net8.0-windows\win-x86\publish\*.exe") do (
    copy "%%F" "Pikuniku_dance_clicker_x86\" >nul
    echo   Copied: %%~nxF
)

copy "logsettings.json" "Pikuniku_dance_clicker_x86\" >nul
xcopy "Assets" "Pikuniku_dance_clicker_x86\Assets\" /E /I /Y >nul
echo Done.
echo.

REM ========================================
REM СОЗДАНИЕ README ФАЙЛОВ
REM ========================================
echo Creating README files...

REM README для x64
(
echo ========================================
echo Pikuniku Dance Clicker v2.0 ^(x64^)
echo ========================================
echo.
echo SYSTEM REQUIREMENTS:
echo • Windows 10/11 ^(64-bit only^)
echo • Screen resolution: 1920x1080 or higher
echo • No installation required!
echo.
echo HOW TO USE:
echo 1. Extract the folder to any location
echo 2. If your system is weak, close all third-party applications
echo 3. Run Pikuniku_dance_clicker_v2.exe
echo 4. Only after that, launch the game and enter the dance mode. When the frog appears, do NOT press Z
echo 5. Press START or Ctrl+Shift+A so the program can detect the center
echo 6. After that, press Z in the game to start the dance
echo 7. Observe how the program works
echo.
echo HOTKEYS:
echo • Ctrl+Shift+A: Start/Stop
echo • Ctrl+Shift+Q: Exit
echo.
echo IMPORTANT:
echo • DO NOT delete the Assets folder!
echo • All libraries are embedded in the .exe
echo • Logs are saved in the logs/ folder
echo.
echo TROUBLESHOOTING:
echo • If "Access Denied" – run as Administrator
echo • If antivirus blocks it – add to exclusions
echo • Check the logs/app-*.txt files for errors
echo • If the issue persists, send the log file to the Telegram chat via the link: https://t.me/mahiron_hq
echo.
echo ========================================
echo Version: x64 ^(64-bit^) for modern Windows
echo ========================================
) > "Pikuniku_dance_clicker_x64\README.txt"

REM README для x86
(
echo ========================================
echo Pikuniku Dance Clicker v2.0 ^(x86^)
echo ========================================
echo.
echo SYSTEM REQUIREMENTS:
echo • Windows 10/11 ^(32-bit or 64-bit^)
echo • Screen resolution: 1920x1080 or higher
echo • No installation required!
echo.
echo HOW TO USE:
echo 1. Extract the folder to any location
echo 2. If your system is weak, close all third-party applications
echo 3. Run Pikuniku_dance_clicker_v2.exe
echo 4. Only after that, launch the game and enter the dance mode. When the frog appears, do NOT press Z
echo 5. Press START or Ctrl+Shift+A so the program can detect the center
echo 6. After that, press Z in the game to start the dance
echo 7. Observe how the program works
echo.
echo HOTKEYS:
echo • Ctrl+Shift+A: Start/Stop
echo • Ctrl+Shift+Q: Exit
echo.
echo IMPORTANT:
echo • DO NOT delete the Assets folder!
echo • All libraries are embedded in the .exe
echo • Logs are saved in the logs/ folder
echo.
echo TROUBLESHOOTING:
echo • If "Access Denied" – run as Administrator
echo • If antivirus blocks it – add to exclusions
echo • Check the logs/app-*.txt files for errors
echo • If the issue persists, send the log file to the Telegram chat via the link: https://t.me/mahiron_hq
echo.
echo ========================================
echo Version: x86 ^(32-bit^) for older Windows
echo ========================================
) > "Pikuniku_dance_clicker_x86\README.txt"

echo Done.
echo.

REM ========================================
REM ВЫВОД СТАТИСТИКИ
REM ========================================
setlocal enabledelayedexpansion

echo ========================================
echo BUILD COMPLETED SUCCESSFULLY!
echo ========================================
echo.

REM Размер x64
for %%A in ("Pikuniku_dance_clicker_x64\*.exe") do (
    set size64=%%~zA
    set /a sizeMB64=!size64! / 1048576
    set exeName64=%%~nxA
)

REM Размер x86
for %%A in ("Pikuniku_dance_clicker_x86\*.exe") do (
    set size86=%%~zA
    set /a sizeMB86=!size86! / 1048576
    set exeName86=%%~nxA
)

echo x64 VERSION:
echo   Location: Pikuniku_dance_clicker_x64\
echo   EXE name: !exeName64!
echo   EXE size: !sizeMB64! MB
echo   Contents:
dir /B Pikuniku_dance_clicker_x64 | findstr /V /C:"logs"
echo.

echo x86 VERSION:
echo   Location: Pikuniku_dance_clicker_x86\
echo   EXE name: !exeName86!
echo   EXE size: !sizeMB86! MB
echo   Contents:
dir /B Pikuniku_dance_clicker_x86 | findstr /V /C:"logs"
echo.

echo ========================================
echo DISTRIBUTION:
echo ========================================
echo.
echo 1. Pikuniku_dance_clicker_x64\
echo    - For most users (Windows 10/11 64-bit)
echo    - Recommended
echo.
echo 2. Pikuniku_dance_clicker_x86\
echo    - For older systems (32-bit Windows)
echo.

echo ========================================
echo Both builds ready for distribution!
echo ========================================
echo.
pause