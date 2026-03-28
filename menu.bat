@echo off
REM Unix Browser - Installation & Development Menu

:MENU
cls
echo.
echo ╔════════════════════════════════════════════════════════════════╗
echo ║                   Unix Browser - Main Menu                     ║
echo ║              Fast PWA ^& React Native Web Browser               ║
echo ╚════════════════════════════════════════════════════════════════╝
echo.
echo [1] Install Unix Browser (Desktop + Start Menu shortcuts)
echo [2] Build Project (Debug)
echo [3] Build Project (Release - Optimized)
echo [4] Run Browser (Development)
echo [5] Publish Release (Self-Contained Executable)
echo [6] Open Documentation
echo [7] Exit
echo.
set /p CHOICE=Select option [1-7]: 

if "%CHOICE%"=="1" goto INSTALL
if "%CHOICE%"=="2" goto BUILD_DEBUG
if "%CHOICE%"=="3" goto BUILD_RELEASE
if "%CHOICE%"=="4" goto RUN
if "%CHOICE%"=="5" goto PUBLISH
if "%CHOICE%"=="6" goto DOCS
if "%CHOICE%"=="7" goto EXIT
goto INVALID

:INSTALL
cls
echo.
echo [*] Starting Installation Wizard...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
goto MENU

:BUILD_DEBUG
cls
echo.
echo [*] Building in Debug mode...
dotnet build --nologo
echo.
pause
goto MENU

:BUILD_RELEASE
cls
echo.
echo [*] Building in Release mode (optimized)...
dotnet build -c Release --nologo
echo.
pause
goto MENU

:RUN
cls
echo.
echo [*] Launching Unix Browser (Development)...
dotnet run
goto MENU

:PUBLISH
cls
echo.
echo [*] Publishing as self-contained executable...
set "PUBLISH_PATH=%~dp0bin\Release\net8.0-windows\publish"
if exist "%PUBLISH_PATH%" rmdir /s /q "%PUBLISH_PATH%"
dotnet publish -c Release --self-contained -o "%PUBLISH_PATH%"
echo.
echo [✓] Published to: %PUBLISH_PATH%
echo [*] You can now copy this folder to C:\Program Files\UnixBrowser\
echo.
pause
goto MENU

:DOCS
cls
echo.
echo [*] Available Documentation:
echo.
echo [1] Quick Start Guide (QUICKSTART.md)
echo [2] Full Installation Guide (INSTALL.md)
echo [3] Installation System (INSTALLATION_SYSTEM.md)
echo [4] Configuration Guide (CONFIGURATION.md)
echo [5] Project Summary (PROJECT_SUMMARY.md)
echo [6] Main README (README.md)
echo [0] Back to main menu
echo.
set /p DOCHOICE=Select documentation [0-6]: 

if "%DOCHOICE%"=="1" start notepad "%~dp0QUICKSTART.md"
if "%DOCHOICE%"=="2" start notepad "%~dp0INSTALL.md"
if "%DOCHOICE%"=="3" start notepad "%~dp0INSTALLATION_SYSTEM.md"
if "%DOCHOICE%"=="4" start notepad "%~dp0CONFIGURATION.md"
if "%DOCHOICE%"=="5" start notepad "%~dp0PROJECT_SUMMARY.md"
if "%DOCHOICE%"=="6" start notepad "%~dp0README.md"
if "%DOCHOICE%"=="0" goto MENU
goto MENU

:INVALID
echo.
echo [×] Invalid option. Please select 1-7.
echo.
pause
goto MENU

:EXIT
exit /b 0
