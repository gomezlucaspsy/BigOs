# Unix Browser - Installation Guide

## Installation Methods

### Method 1: PowerShell Installer (Recommended)
**Easiest for developers**

1. Right-click `install.ps1` → Select "Run with PowerShell"
2. Click "Run" if prompted by security warning
3. The installer will:
   - Build the project in Release mode
   - Publish as self-contained application
   - Install to `C:\Program Files\UnixBrowser`
   - Create Desktop shortcut
   - Create Start Menu entry

### Method 2: Batch File Installer
**Easiest for regular users**

1. Right-click `install.bat` → Select "Run as Administrator"
2. Follow the prompts
3. Same installation as Method 1

### Method 3: Inno Setup (Professional Installer)
**Creates a standalone .exe installer**

1. Download Inno Setup: https://jrsoftware.org/isdl.php
2. Install Inno Setup
3. Right-click `installer.iss` → Select "Compile with Inno Setup"
4. An `.exe` installer will be created in the `dist/` folder
5. Share this `.exe` with others — they don't need .NET or PowerShell!

### Method 4: Manual Installation
1. Open Command Prompt/PowerShell as Administrator
2. Navigate to the project directory
3. Run:
```batch
dotnet publish -c Release --self-contained -o "C:\Program Files\UnixBrowser"
```
4. Create a shortcut manually:
   - Right-click Desktop → New → Shortcut
   - Target: `C:\Program Files\UnixBrowser\UnixBrowser.exe`
   - Name: `Unix Browser`

---

## What Gets Installed

```
C:\Program Files\UnixBrowser\
├── UnixBrowser.exe          (Main application)
├── *.dll                    (Dependencies)
├── web.config              (WebView2 config)
└── [other runtime files]

%AppData%\UnixBrowser\
└── favorites.json          (Saved favorites)
```

**Desktop:** `Unix Browser.lnk` shortcut
**Start Menu:** `Start → Unix Browser`

---

## System Requirements

- **OS:** Windows 10 or later (x64)
- **.NET:** 8.0 Runtime (included in Release build with `--self-contained`)
- **WebView2:** Automatically installed with Windows 10/11
- **RAM:** ~150-300MB baseline
- **Disk Space:** ~200MB for installation

---

## Uninstalling

### If installed via PowerShell/Batch:
1. Delete: `C:\Program Files\UnixBrowser`
2. Delete: Desktop shortcut `Unix Browser.lnk`
3. Delete: Start Menu folder `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Unix Browser`
4. Delete: `%AppData%\UnixBrowser` (to remove favorites)

### If installed via Inno Setup:
1. Go to Settings → Apps → Apps & features
2. Search for "Unix Browser"
3. Click → Uninstall
4. Follow prompts

---

## Troubleshooting

### "PowerShell execution policy" error
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### ".NET 8.0 not found"
Download from: https://dotnet.microsoft.com/download/dotnet/8.0

### Application won't start
1. Check: `C:\Program Files\UnixBrowser\UnixBrowser.exe` exists
2. Try right-clicking → Run as Administrator
3. Check Windows Event Viewer for errors

### WebView2 issues
Install WebView2 Runtime: https://developer.microsoft.com/en-us/microsoft-edge/webview2/

---

## Creating a Portable Version

For a USB/portable installation:
```powershell
dotnet publish -c Release --self-contained -o "D:\UnixBrowser"
```
Then copy the entire `UnixBrowser` folder. It will run from any location.

---

## Advanced: Creating Distribution Package

To create a professional installer for distribution:

1. **Update version in `installer.iss`:**
```
#define MyAppVersion "1.0.0"
```

2. **Compile with Inno Setup** → Creates `dist/UnixBrowser-Setup.exe`

3. **Share the `.exe`** — users can run it directly without .NET or PowerShell!

---

## Support

For issues or questions:
- Check README.md
- Review CONFIGURATION.md
- Open an issue on GitHub
