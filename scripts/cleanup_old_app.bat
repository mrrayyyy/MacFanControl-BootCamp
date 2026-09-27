@echo off
setlocal
echo ==========================================================
echo   Cleanup Old Macs Fan Control ^& Relocate Driver to System32
echo ==========================================================
echo.

:: Check Admin
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] This script requires Administrator privileges!
    echo Please right-click this file and select 'Run as administrator'.
    echo.
    pause
    exit /b 1
)

echo [1/5] Stopping current AppleSMC driver instance...
sc stop applesmc >nul 2>&1
timeout /t 2 /nobreak >nul

echo [2/5] Ensuring permanent driver exists in System32...
if not exist "%SystemRoot%\System32\drivers\applesmc.sys" (
    if exist "%~dp0..\publish\applesmc.sys" (
        copy /y "%~dp0..\publish\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys"
    ) else if exist "%~dp0..\publish_new\applesmc.sys" (
        copy /y "%~dp0..\publish_new\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys"
    ) else if exist "%~dp0..\src\MacFanControl.UI\applesmc.sys" (
        copy /y "%~dp0..\src\MacFanControl.UI\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys"
    ) else if exist "%ProgramFiles(x86)%\Macs Fan Control\applesmc.sys" (
        copy /y "%ProgramFiles(x86)%\Macs Fan Control\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys"
    )
)

echo [3/5] Reconfiguring AppleSMC service to use System32 driver (Auto-start)...
sc config applesmc binPath= "%SystemRoot%\System32\drivers\applesmc.sys" start= auto
if %ERRORLEVEL% neq 0 (
    sc create applesmc binPath= "%SystemRoot%\System32\drivers\applesmc.sys" type= kernel start= auto error= normal displayName= "Apple SMC service"
)

echo [4/5] Deleting old 'C:\Program Files (x86)\Macs Fan Control' folder...
del /f /q "%ProgramFiles(x86)%\Macs Fan Control\*" >nul 2>&1
rd /s /q "%ProgramFiles(x86)%\Macs Fan Control" >nul 2>&1

if exist "%ProgramFiles(x86)%\Macs Fan Control" (
    echo [WARNING] Folder could not be deleted immediately. It will be removed upon reboot.
) else (
    echo [SUCCESS] Folder 'C:\Program Files (x86)\Macs Fan Control' has been completely removed!
)

echo [5/5] Starting AppleSMC service from System32...
sc start applesmc
timeout /t 1 /nobreak >nul
sc query applesmc

echo.
echo ==========================================================
echo [DONE] Cleanup completed successfully!
echo The driver is now cleanly running from Windows System32.
echo ==========================================================
echo.
pause
