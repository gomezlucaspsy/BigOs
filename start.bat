@echo off
REM Unix Browser - Quick Start Script
REM This script builds and runs the Unix Browser

echo.
echo ╔═══════════════════════════════════════╗
echo ║     Unix Browser - Terminal Edition   ║
echo ║    Fast PWA & React Native Support    ║
echo ╚═══════════════════════════════════════╝
echo.

REM Check if .NET is installed
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: .NET SDK is not installed
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [*] Restoring dependencies...
dotnet restore

echo [*] Building project...
dotnet build -c Release

if %errorlevel% neq 0 (
    echo Error: Build failed
    pause
    exit /b 1
)

echo [*] Launching Unix Browser...
dotnet run -c Release

pause
