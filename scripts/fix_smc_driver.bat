@echo off
:: Batch script to fix Apple SMC driver startup mode and start the service
echo ==========================================================
echo   Apple SMC Kernel Driver Fix for Boot Camp (MBP 16 2019)
echo ==========================================================
echo.

:: Check for Administrative privileges
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] This script must be run as Administrator!
    echo Please right-click this file and select 'Run as administrator'.
    echo.
    pause
    exit /b 1
)

echo [1/3] Configuring AppleSMC service to start automatically on Windows boot...
:: Ensure permanent driver exists in System32
if not exist "%SystemRoot%\System32\drivers\applesmc.sys" (
    if exist "%~dp0..\publish\applesmc.sys" (
        copy /y "%~dp0..\publish\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys" >nul 2>&1
    ) else if exist "%~dp0..\src\MacFanControl.UI\applesmc.sys" (
        copy /y "%~dp0..\src\MacFanControl.UI\applesmc.sys" "%SystemRoot%\System32\drivers\applesmc.sys" >nul 2>&1
    )
)

if exist "%SystemRoot%\System32\drivers\applesmc.sys" (
    sc config applesmc binPath= "%SystemRoot%\System32\drivers\applesmc.sys" start= auto
) else (
    sc config applesmc start= auto
)

if %ERRORLEVEL% neq 0 (
    echo [WARNING] Service does not exist yet. Attempting to register service...
    if exist "%SystemRoot%\System32\drivers\applesmc.sys" (
        sc create applesmc binPath= "%SystemRoot%\System32\drivers\applesmc.sys" type= kernel start= auto error= normal displayName= "Apple SMC service"
    ) else if exist "%~dp0..\publish\applesmc.sys" (
        sc create applesmc binPath= "%~dp0..\publish\applesmc.sys" type= kernel start= auto error= normal displayName= "Apple SMC service"
    )
)

echo.
echo [2/3] Starting AppleSMC kernel service...
net start applesmc

echo.
echo [3/3] Checking service status...
sc query applesmc

echo.
echo ==========================================================
echo [SUCCESS] Apple SMC driver is now active and set to AUTO!
echo You can now open MacFanControl.UI.exe to control fans.
echo ==========================================================
echo.
pause
