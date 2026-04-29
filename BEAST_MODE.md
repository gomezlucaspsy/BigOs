# Beast Mode Configuration

## Overview
The **Beast Mode** is a simplified, lightweight version of Unix Browser with minimal features - like a basic TODO list with only essential CRUD operations.

## What's Disabled in Beast Mode
- ❌ PWA Support (Progressive Web Apps)
- ❌ Service Workers
- ❌ Offline Mode
- ❌ React Native Detection
- ❌ History Tracking
- ❌ Hardware Acceleration
- ❌ JIT Compilation

## What's Enabled in Beast Mode
- ✅ Basic Navigation (Back, Forward, Refresh)
- ✅ Tab Management (Create, Close tabs)
- ✅ Favorites (Add, Remove favorites)
- ✅ Address Bar (Browse URLs)

## Performance
- **Cache**: 50MB (vs 100MB standard)
- **User Agent**: `Mozilla/5.0 (Windows NT 10.0; Win64; x64) UnixBrowser/1.0-Beast`
- **Startup**: Faster due to fewer features

## How to Run Beast Mode

### Via Command Line
```bash
# PowerShell
dotnet run --beast

# Command Prompt
dotnet run -- --beast

# After building
UnixBrowser.exe --beast
```

### Via start.ps1 Script
```powershell
.\start.ps1 -Beast
```

## Config Files
- **Standard Mode**: `Config/BrowserConfig.cs`
- **Beast Mode**: `Config/BrowserConfig.cs` (BeastConfig class)

## Use Cases
- Lightweight browsing
- Minimal system requirements
- Distraction-free interface
- Testing core functionality
- Old hardware compatibility

## Related Files
- `App.xaml.cs` - Detects `--beast` flag
- `MainWindow.xaml.cs` - Applies Beast Mode settings
- `Config/BrowserConfig.cs` - Configuration definitions
