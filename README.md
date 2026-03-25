# BigOs - Native C++ Browser Shell

BigOs is now C++-first, focused on a native desktop browser shell with a Unix/FreeBSD visual style.

Current implementation target:

- Windows desktop app in C++20
- Native WebView2 rendering engine
- CMake build system
- Shared C++ core for URL and tab state

## Features (current MVP)

- Native Win32 window
- Embedded WebView2 browser engine
- FreeBSD-like terminal chrome injected into pages
- Shared C++ tab manager and URL normalizer

## Project layout

```
BigOs/
├── CMakeLists.txt
├── cpp/
│   ├── core/
│   │   ├── include/bigos/core/
│   │   └── src/
│   └── apps/
│       └── bigos-desktop/
│           ├── CMakeLists.txt
│           └── src/main.cpp
└── .github/workflows/
    └── release-desktop.yml
```

## Build locally (Windows)

### 1) Install prerequisites

- Visual Studio 2022 Build Tools (Desktop development with C++)
- CMake 3.24+
- WebView2 SDK

Install Build Tools with winget:

```powershell
winget install Microsoft.VisualStudio.2022.BuildTools
```

### 2) Set WebView2 SDK path

Set an environment variable that points to the SDK root:

```powershell
$env:WEBVIEW2_SDK_PATH="C:\path\to\Microsoft.Web.WebView2.<version>"
```

The build expects:

- `build/native/include/WebView2.h`
- `build/native/x64/WebView2LoaderStatic.lib`

### 3) Configure and build

```powershell
cmake -S . -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release
```

### 4) Run

```powershell
.\build\cpp\apps\bigos-desktop\Release\bigos-desktop.exe
```

## GitHub Releases

Tagging `v*` triggers desktop CI and publishes a Windows x64 zip in GitHub Releases.

## React Native app compatibility

BigOs uses the system WebView2 engine, so React Native Web apps (including localhost dev servers such as PersonaForge) run with modern browser APIs.

