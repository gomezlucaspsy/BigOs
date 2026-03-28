
# Unix Browser - Installer Script
# Run as Administrator

param(
    [string]$InstallPath = "C:\Program Files\UnixBrowser"
)

$ErrorActionPreference = "Stop"

# Colors
$Green  = "`e[32m"
$Red    = "`e[31m"
$Yellow = "`e[33m"
$Reset  = "`e[0m"

Write-Host @"
$Green
╔════════════════════════════════════════╗
║   Unix Browser - Installation Wizard   ║
║         Terminal Edition v1.0          ║
╚════════════════════════════════════════╝
$Reset
"@

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "$Red[✗] Please run this script as Administrator$Reset"
    Write-Host "Right-click PowerShell and select 'Run as administrator'"
    exit 1
}

Write-Host "$Yellow[1/4]$Reset Building application in Release mode..."
& dotnet build -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "$Red[✗] Build failed!$Reset"
    exit 1
}
Write-Host "$Green[✓] Build successful$Reset"

Write-Host "$Yellow[2/4]$Reset Publishing application..."
$publishPath = "$PSScriptRoot\bin\Release\net8.0-windows\publish"
if (Test-Path $publishPath) { Remove-Item $publishPath -Recurse -Force }
& dotnet publish -c Release --self-contained -o $publishPath -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "$Red[✗] Publish failed!$Reset"
    exit 1
}
Write-Host "$Green[✓] Publish successful$Reset"

Write-Host "$Yellow[3/4]$Reset Installing to $InstallPath..."
if (Test-Path $InstallPath) { Remove-Item $InstallPath -Recurse -Force }
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Copy-Item "$publishPath\*" -Destination $InstallPath -Recurse -Force
Write-Host "$Green[✓] Files copied$Reset"

Write-Host "$Yellow[4/4]$Reset Creating shortcuts..."

# Desktop Shortcut
$DesktopPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
$ShortcutPath = Join-Path $DesktopPath "Unix Browser.lnk"
$ExePath = Join-Path $InstallPath "UnixBrowser.exe"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = $ExePath
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "Fast PWA & React Native Web Browser"
$shortcut.WindowStyle = 1
$shortcut.Save()

# Start Menu Shortcut
$StartMenuPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$AppFolder = Join-Path $StartMenuPath "Unix Browser"
New-Item -ItemType Directory -Path $AppFolder -Force | Out-Null

$StartMenuShortcut = Join-Path $AppFolder "Unix Browser.lnk"
$shortcut = $shell.CreateShortcut($StartMenuShortcut)
$shortcut.TargetPath = $ExePath
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "Fast PWA & React Native Web Browser"
$shortcut.WindowStyle = 1
$shortcut.Save()

Write-Host "$Green[✓] Shortcuts created$Reset"

Write-Host @"

$Green╔════════════════════════════════════════╗
║    Installation Complete! ✓             ║
╚════════════════════════════════════════╝$Reset

📍 Installed to: $InstallPath
🖥️  Desktop shortcut: Unix Browser.lnk
📋 Start Menu: Start → Unix Browser

To uninstall:
  1. Delete folder: $InstallPath
  2. Remove desktop shortcut
  3. Remove Start Menu shortcut

Ready to launch? Press Enter to open Unix Browser now...
"@

Read-Host

& $ExePath
