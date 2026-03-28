# 🚀 How to Install Unix Browser - Step by Step

## Quick Start (30 seconds)

### **Option A: Using Batch Installer (EASIEST)**
```
1. Right-click install.bat
2. Select "Run as Administrator"
3. Wait ~1 minute
4. Browser opens automatically!
```

### **Option B: Using PowerShell**
```
1. Right-click install.ps1
2. Select "Run with PowerShell" or "Run with PowerShell ISE"
3. Wait ~1 minute
4. Browser opens automatically!
```

### **Option C: Using Menu**
```
1. Right-click menu.bat
2. Select "Run as Administrator"
3. Choose option [1] to install
4. Done!
```

---

## What Gets Installed

After running the installer:

✅ **C:\Program Files\UnixBrowser\**
- Complete browser executable
- All dependencies included
- Ready to run

✅ **Desktop Shortcut**
- Named "Unix Browser.lnk"
- Click to launch instantly

✅ **Start Menu**
- Windows Start → Type "Unix Browser"
- Click to launch

✅ **Favorites Storage**
- `%AppData%\UnixBrowser\favorites.json`
- Auto-saves your bookmarks

---

## Uninstalling

### **Method 1: Settings (Cleanest)**
1. Go to Settings → Apps → Apps & features
2. Search "Unix Browser"
3. Click → Uninstall
4. Done!

### **Method 2: Manual**
1. Delete folder: `C:\Program Files\UnixBrowser`
2. Delete desktop shortcut
3. Delete from Start Menu: `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Unix Browser`

---

## Troubleshooting

### ❌ "PowerShell says it won't run"
**Solution:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```
Then try again.

### ❌ ".NET 8.0 not found"
**Solution:**
Download from: https://dotnet.microsoft.com/download/dotnet/8.0

### ❌ "Access Denied"
**Solution:**
- Right-click the installer
- Select "Run as Administrator"
- Click "Yes" if prompted

### ❌ "Shortcut doesn't work"
**Solution:**
1. Check file exists: `C:\Program Files\UnixBrowser\UnixBrowser.exe`
2. Try launching directly from that folder
3. Try reinstalling

---

## Advanced: Creating a Professional Installer

Want to create a single `.exe` that anyone can run?

### **Step 1: Download Inno Setup**
https://jrsoftware.org/isdl.php

### **Step 2: Compile the Installer**
```
Right-click installer.iss → "Compile with Inno Setup"
```

### **Step 3: Done!**
- Creates: `dist/UnixBrowser-Setup.exe`
- Users can run it with a double-click
- No need for .NET, PowerShell, or anything else
- Professional install/uninstall experience

---

## System Requirements

| Component | Required |
|-----------|----------|
| **OS** | Windows 10 or later |
| **Architecture** | 64-bit (x64) |
| **Disk Space** | 200MB minimum |
| **RAM** | 300MB while running |
| **WebView2** | Automatic (built into Windows) |

---

## What's Included

The installer includes:

✅ Unix Browser executable
✅ WebView2 rendering engine
✅ .NET 8.0 runtime (self-contained)
✅ All required libraries
✅ Desktop shortcut
✅ Start Menu entry

**That's it!** No additional downloads or setup needed after installation.

---

## First Launch

When you first launch Unix Browser:

1. ✅ Address bar loads empty
2. ✅ Type a URL or search term
3. ✅ Press Enter to navigate
4. ✅ Browser displays the page
5. ✅ Click ★ to save favorites
6. ✅ Click + to open new tabs

---

## Features You Get

🌐 **Web Browsing**
- Full Chromium rendering engine
- Modern JavaScript support
- WebGL/Video support

⭐ **Favorites**
- Click ★ button to save
- Right-click to remove
- Auto-saves to JSON file

📑 **Tabs**
- Click + to open new tab
- Click tab to switch
- Click ✕ to close
- At least 1 tab always stays open

🌐 **PWA Support**
- Automatic PWA detection
- Install web apps to Start Menu
- Service Worker support

⚛️ **React Native**
- Runs React Native web apps
- Supports apps like Personaforge
- Proper viewport configuration

⚙️ **Terminal Aesthetic**
- Black/green Unix-style theme
- Monospace font (Courier New)
- Minimal, distraction-free interface

---

## Next Steps

### **Option 1: Regular User**
- Just run the installer
- Start using the browser
- Enjoy!

### **Option 2: Developer**
- Modify the source code
- Run `menu.bat` for options
- Build custom versions

### **Option 3: Distributor**
- Create `dist/UnixBrowser-Setup.exe`
- Share with others
- They run it once and it's installed

---

## Support

📖 **Documentation:**
- `QUICKSTART.md` - Quick reference
- `INSTALL.md` - Full installation guide
- `CONFIGURATION.md` - Customization options
- `PROJECT_SUMMARY.md` - Architecture overview

🐛 **Issues?**
- Check the docs first
- Try reinstalling
- Check System Requirements
- Open an issue on GitHub

---

## Version Info

**Unix Browser v1.0**
- Built with .NET 8.0
- WebView2 (Chromium) rendering
- Windows 10/11 only
- Free and Open Source

---

**Ready to install? Start with the installer that fits your style! 🎉**

Questions? Check the documentation in the project folder.
