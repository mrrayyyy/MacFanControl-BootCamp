@echo off
setlocal
echo ==========================================================
echo   Building MacFanControl BootCamp for MBP 16 2019 (i9)
echo ==========================================================

where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET 8 SDK is not installed or not in PATH.
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/2] Publishing Single-File Portable Executable (win-x64)...
dotnet publish src/MacFanControl.UI/MacFanControl.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/2] Build completed successfully!
echo Executable generated at: ./publish/MacFanControl.UI.exe
echo.
pause
