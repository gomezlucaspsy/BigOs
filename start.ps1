#!/bin/bash
# Unix Browser - Quick Start Script for PowerShell

Write-Host @"
╔═══════════════════════════════════════╗
║     Unix Browser - Terminal Edition   ║
║    Fast PWA & React Native Support    ║
╚═══════════════════════════════════════╝
"@ -ForegroundColor Green

# Check if dotnet is installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK is not installed" -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download"
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "[*] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore

Write-Host "[*] Building project in Release mode..." -ForegroundColor Yellow
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "[*] Launching Unix Browser..." -ForegroundColor Green
dotnet run -c Release

Read-Host "Press Enter to exit"
