@echo off
REM Unix Browser Launcher
REM This script sets up WebView2 data directory and launches the browser

set "WEBVIEW2_USER_DATA_FOLDER=%AppData%\UnixBrowser\WebView2"
"%~dp0UnixBrowser.exe"
