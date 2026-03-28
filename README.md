README: Unix Browser
====================

A fast, minimalist web browser for Windows built in C# with:
- Black/grey terminal aesthetic inspired by Unix/BSD
- Progressive Web App (PWA) installation support
- React Native web app compatibility
- Service Worker support for offline functionality
- Chromium-based rendering via WebView2

FEATURES
--------
✓ Modern Chromium rendering engine
✓ PWA installation prompts
✓ Service Worker support
✓ React Native web app detection
✓ Offline mode capability
✓ Unix/BSD-inspired terminal UI
✓ Minimal, distraction-free interface
✓ Fast performance with hardware acceleration

REQUIREMENTS
-----------
- Windows 10 or later
- .NET 8.0 SDK or runtime
- WebView2 Runtime (automatically installed with most Windows systems)

BUILD
-----
dotnet build -c Release

RUN
---
dotnet run

USAGE
-----
1. Type URLs in the address bar and press Enter
2. Use navigation buttons: ← (Back), → (Forward), ⟳ (Refresh), ⌂ (Home)
3. The browser automatically detects and supports PWA installations
4. React Native web apps are automatically detected and configured

SUPPORTED APPS
--------------
- Personaforge (https://github.com/gomezlucaspsy/personaforge)
- Any React Native web app built with Expo Web
- PWA-compliant Progressive Web Apps
- Standard web applications

CUSTOMIZATION
-------------
Edit Config/BrowserConfig.cs to customize:
- Theme colors
- Cache size
- Performance settings
- PWA features

LICENSE
-------
Free and open source.
