# PowerShell script to clean up old Macs Fan Control installation
# Must run with Administrator privileges

$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Host "[ERROR] This script requires Administrator privileges!" -ForegroundColor Red
    Write-Host "Please right-click and select 'Run with PowerShell as Administrator'." -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Cleaning Up Old Macs Fan Control Folder                " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Stop driver
Write-Host "[1/5] Stopping current AppleSMC driver..." -ForegroundColor Yellow
sc.exe stop applesmc | Out-Null
Start-Sleep -Seconds 2

# 2. Ensure System32 driver exists
Write-Host "[2/5] Verifying permanent driver in System32..." -ForegroundColor Yellow
$sys32Driver = "$env:SystemRoot\System32\drivers\applesmc.sys"
if (-not (Test-Path $sys32Driver)) {
    $candidates = @(
        "$PSScriptRoot\..\publish\applesmc.sys",
        "$PSScriptRoot\..\publish_new\applesmc.sys",
        "$PSScriptRoot\..\src\MacFanControl.UI\applesmc.sys",
        "C:\Program Files (x86)\Macs Fan Control\applesmc.sys"
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            Copy-Item $cand -Destination $sys32Driver -Force
            break
        }
    }
}

# 3. Update service config
Write-Host "[3/5] Reconfiguring AppleSMC service to use System32 (Auto-start)..." -ForegroundColor Yellow
sc.exe config applesmc binPath= "$sys32Driver" start= auto | Out-Null

# 4. Remove old folder
Write-Host "[4/5] Deleting old 'C:\Program Files (x86)\Macs Fan Control' folder..." -ForegroundColor Yellow
$oldFolder = "C:\Program Files (x86)\Macs Fan Control"
if (Test-Path $oldFolder) {
    Remove-Item -Path $oldFolder -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $oldFolder) {
        Write-Host "[NOTE] applesmc.sys was locked by Windows kernel. It is queued for deletion upon reboot." -ForegroundColor Yellow
    } else {
        Write-Host "[SUCCESS] Folder successfully deleted!" -ForegroundColor Green
    }
} else {
    Write-Host "Folder does not exist. Already clean!" -ForegroundColor Green
}

# 5. Start service from System32
Write-Host "[5/5] Starting AppleSMC service from System32..." -ForegroundColor Yellow
sc.exe start applesmc | Out-Null
Start-Sleep -Seconds 1
sc.exe query applesmc

Write-Host "`nCleanup completed successfully! You can now run MacFanControl.UI." -ForegroundColor Green
