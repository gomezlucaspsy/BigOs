@echo off
setlocal enabledelayedexpansion
echo.
echo ========================================
echo   Unix Browser Installation
echo ========================================
echo.
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: Run as Administrator
    pause
    exit /b 1
)
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET 8.0 not installed
    pause
    exit /b 1
)
set "INSTALL=%ProgramFiles%\UnixBrowser"
set "PROJECT=%~dp0"
echo [1/4] Building...
cd /d "%PROJECT%"
dotnet build -c Release --nologo
if !errorlevel! neq 0 exit /b 1
echo [2/4] Publishing...
dotnet publish -c Release --self-contained -o "bin\Release\net8.0-windows\publish" --nologo
if !errorlevel! neq 0 exit /b 1
echo [3/4] Installing...
if exist "%INSTALL%" rmdir /s /q "%INSTALL%"
mkdir "%INSTALL%"
xcopy "bin\Release\net8.0-windows\publish\*" "%INSTALL%\" /e /i /y >nul
copy "UnixBrowser.bat" "%INSTALL%\" >nul
echo [4/4] Creating shortcuts...
set "VBS=%temp%\mkshortcut.vbs"
(
  echo Set s=CreateObject^("WScript.Shell"^)
  echo Set l=s.CreateShortcut^(s.SpecialFolders^("Desktop"^)^&"\Unix Browser.lnk"^)
  echo l.TargetPath="%INSTALL%\UnixBrowser.bat"
  echo l.WorkingDirectory="%INSTALL%"
  echo l.Save
) > "%VBS%"
cscript //nologo "%VBS%"
del "%VBS%"
echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo Installed to: %INSTALL%
echo.
pause
"%INSTALL%\UnixBrowser.bat"

