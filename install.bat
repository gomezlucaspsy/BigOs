@echo off
REM Unix Browser - Installer Batch Wrapper
REM This launches the PowerShell installer with admin rights

setlocal enabledelayedexpansion

echo.
echo ╔════════════════════════════════════════╗
echo ║   Unix Browser - Installation Wizard   ║
echo ║         Terminal Edition v1.0          ║
echo ╚════════════════════════════════════════╝
echo.

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [×] This script requires Administrator privileges
    echo Right-click this file and select "Run as Administrator"
    pause
    exit /b 1
)

REM Default install path
set "INSTALL_PATH=C:\Program Files\UnixBrowser"

REM Check if .NET is installed
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [×] .NET 8.0 SDK is not installed
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Run the PowerShell installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -InstallPath "%INSTALL_PATH%"

pause
