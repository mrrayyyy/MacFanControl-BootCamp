# Build and Publish script for MacFanControl BootCamp
# Run this script in Windows PowerShell on Boot Camp

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Building MacFanControl BootCamp for MBP 16 2019 (i9)    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ProjectPath = "src/MacFanControl.UI/MacFanControl.UI.csproj"
$OutputDir = "./publish"

# Check if dotnet is installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] .NET 8 SDK is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/3] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $ProjectPath -r win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] NuGet restore failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[2/3] Publishing Single-File Portable Executable (win-x64)..." -ForegroundColor Yellow
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[3/3] Build completed successfully!" -ForegroundColor Green
Write-Host "Executable generated at: $OutputDir/MacFanControl.UI.exe" -ForegroundColor Green
Write-Host "Note: Right-click and 'Run as administrator' for full SMC fan control." -ForegroundColor Cyan
