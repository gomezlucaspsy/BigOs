# 🚀 Unix Browser - Quick Installation Guide

## ⚡ Fastest Way (3 clicks)

### **Windows Users:**
1. **Download the project**
2. **Right-click `install.bat`** → "Run as Administrator"
3. **Done!** Your browser opens automatically

### **Developers:**
1. **Right-click `install.ps1`** → "Run with PowerShell"
2. **Wait for build & installation**
3. **Your browser launches!**

---

## 📦 What Each Installer Does

| File | What it does | For whom |
|------|---|---|
| `install.bat` | Launches PowerShell installer with admin rights | Anyone (easiest) |
| `install.ps1` | Builds, publishes, installs, creates shortcuts | Developers/Tech users |
| `installer.iss` | Inno Setup script → creates `UnixBrowser-Setup.exe` | Distribution |

---

## 🎯 Installation Paths

After installation, you'll have:

| Location | What |
|----------|------|
| `C:\Program Files\UnixBrowser\` | All app files |
| `%AppData%\UnixBrowser\` | Your favorites (favorites.json) |
| **Desktop** | Shortcut to launch the app |
| **Start Menu** | Windows Start → Unix Browser |

---

## 🔧 Creating a Professional Installer

If you want to share UnixBrowser as a standalone `.exe`:

1. **Install Inno Setup:** https://jrsoftware.org/isdl.php
2. **Right-click `installer.iss`** → "Compile with Inno Setup"
3. **Done!** Creates `dist/UnixBrowser-Setup.exe`
   - Users don't need .NET or PowerShell
   - Professional, signed installer
   - Ready to distribute!

---

## 🛑 Uninstalling

### Method 1 (Easiest):
- Settings → Apps → Apps & features
- Search "Unix Browser" → Uninstall

### Method 2 (Manual):
- Delete `C:\Program Files\UnixBrowser`
- Delete desktop shortcut
- Delete Start Menu folder

---

## ❓ Troubleshooting

**"PowerShell won't run the script"**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**".NET not found"**
→ Download: https://dotnet.microsoft.com/download/dotnet/8.0

**"Access Denied" error**
→ Right-click the installer and select "Run as Administrator"

---

## 📋 System Requirements

✅ Windows 10 or later
✅ 200MB disk space
✅ ~300MB RAM while running
✅ That's it! (WebView2 comes with Windows)

---

## 🎓 For Developers

**Manual Release Build:**
```batch
dotnet publish -c Release --self-contained -o "C:\Program Files\UnixBrowser"
```

**Portable Build (USB):**
```batch
dotnet publish -c Release --self-contained -o "D:\UnixBrowser"
```
Then copy the entire folder to USB — it runs anywhere!

---

## 📖 More Info

- **Full installation guide:** `INSTALL.md`
- **Feature overview:** `README.md`
- **Configuration options:** `CONFIGURATION.md`

**Questions?** Check the docs or open an issue on GitHub!

---

**Unix Browser v1.0** — Fast, Free, Terminal-Style Web Browser for Windows ✨
