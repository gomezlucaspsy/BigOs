@echo off
setlocal

set "DEST=%LOCALAPPDATA%\Programs\BigOs"
if not exist "%DEST%" mkdir "%DEST%"

copy /Y "%~dp0bigos-desktop.exe" "%DEST%\BigOs.exe" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "$desk=[Environment]::GetFolderPath('Desktop');$w=New-Object -ComObject WScript.Shell;$lnk=$w.CreateShortcut($desk + '\BigOs.lnk');$lnk.TargetPath='%LOCALAPPDATA%\Programs\BigOs\BigOs.exe';$lnk.WorkingDirectory='%LOCALAPPDATA%\Programs\BigOs';$lnk.IconLocation='%LOCALAPPDATA%\Programs\BigOs\BigOs.exe,0';$lnk.Save()"

start "" "%DEST%\BigOs.exe"
echo BigOs installed at: %DEST%
exit /b 0
