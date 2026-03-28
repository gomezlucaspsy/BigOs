param([string]$Install = "C:\Program Files\UnixBrowser")
$Project = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjFile = Join-Path $Project "UnixBrowser.csproj"
Write-Host ""
Write-Host "========================================"
Write-Host "  Unix Browser Installation"
Write-Host "========================================"
Write-Host ""
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole("Administrator")
if (-not $isAdmin) { Write-Host "ERROR: Run as Administrator"; exit 1 }
if (-not (Test-Path $ProjFile)) { Write-Host "ERROR: Project not found"; exit 1 }
Write-Host "[1/4] Building..."
dotnet build -c Release --project "$ProjFile" --nologo
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "[2/4] Publishing..."
$pub = Join-Path $Project "bin\Release\net8.0-windows\publish"
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish -c Release --self-contained --project "$ProjFile" -o "$pub" --nologo
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "[3/4] Installing..."
if (Test-Path $Install) { Remove-Item $Install -Recurse -Force }
New-Item -ItemType Directory -Path $Install -Force | Out-Null
Copy-Item "$pub\*" -Destination $Install -Recurse -Force
Copy-Item (Join-Path $Project "UnixBrowser.bat") -Destination $Install
Write-Host "[4/4] Shortcuts..."
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut([Environment]::GetFolderPath("Desktop") + "\Unix Browser.lnk")
$s.TargetPath = Join-Path $Install "UnixBrowser.bat"
$s.WorkingDirectory = $Install
$s.Save()
Write-Host ""
Write-Host "========================================"
Write-Host "Installation Complete!"
Write-Host "========================================"
Write-Host ""
Read-Host "Press Enter to launch"
& (Join-Path $Install "UnixBrowser.bat")

