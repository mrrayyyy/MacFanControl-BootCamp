# MacFanControl BootCamp (MBP 16" 2019)

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20x64%20(Boot%20Camp)-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/Framework-.NET%208.0%20(WPF)-purple?style=flat-square" alt="Framework">
  <img src="https://img.shields.io/badge/Hardware-MacBookPro16%2C1%20%7C%20Apple%20T2-orange?style=flat-square" alt="Target Hardware">
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat-square" alt="License">
</p>

A dedicated, modern hardware monitoring and fan control utility built specifically for the **16-inch MacBook Pro 2019** (`MacBookPro16,1` / `MacBookPro16,4`) running **Windows 10 Boot Camp**.

Equipped with an Intel Core i9 processor, an AMD Radeon Pro GPU, and the **Apple T2 Security Chip**, this utility solves the notorious overheating and thermal throttling issues on Boot Camp by enabling proactive, custom-curved fan cooling.

---

## 🌟 Key Features

* **Real-time Hardware Monitoring**:
  * **CPU**: Package temperature, max core temperature, power consumption (Watts), and utilization percentage via Intel DTS / MSR.
  * **GPU**: AMD Radeon Pro core temperature, hotspot temperature, GPU power, and load percentage via AMD ADL API.
  * **Fans**: Real-time RPM tracking and percentage gauges for both **Left Fan (CPU)** and **Right Fan (GPU)**.
* **Intelligent Fan Control**:
  * **Custom Fan Curves**: Smooth temperature-to-RPM curves with built-in **Hysteresis** (3°C) to prevent erratic fan speed oscillations and noise pulsing.
  * **Fixed Manual Mode**: Precision slider control from minimum (~1,800 RPM) to maximum (~5,616 RPM).
  * **100% Turbo Mode**: Instant full-blast cooling for intensive gaming, 3D rendering, or video exports.
  * **Apple Auto Mode**: One-click restore to Apple's native SMC thermal management.
  * **Fail-Safe Protection**: Automatically restores Apple default fan management upon application exit or crash.
* **User Experience & System Integration**:
  * **Fluent Dark UI**: Modern dark theme matching macOS aesthetics, built with WPF on .NET 8.
  * **Dynamic System Tray Icon**: Real-time CPU temperature drawn directly onto the Windows taskbar icon with adaptive color coding (Green < 60°C, Orange 60–78°C, Red > 78°C).
  * **Silent Windows Startup (UAC Bypass)**: Automatically registers a task in Windows Task Scheduler with `HighestAvailable` privileges, enabling the app to boot minimized to tray without annoying UAC prompts.

---

## 💻 System Requirements

* **Computer Model**: MacBook Pro (16-inch, 2019)
  * Model Identifiers: `MacBookPro16,1` or `MacBookPro16,4`
  * CPU: Intel Core i7-9750H, i9-9880H, or i9-9980HK
  * GPU: AMD Radeon Pro 5300M, 5500M, or 5600M
  * Controller: **Apple T2 Security Chip**
* **Operating System**: Windows 10 64-bit (or Windows 11) installed via Boot Camp.
* **Prerequisites**:
  * Apple Boot Camp Support Software drivers installed (specifically `AppleSMC.sys`).
  * [.NET 8.0 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0) (not required if using the self-contained build).
  * **Administrator Privileges**: Required to interface with low-level hardware sensors and the SMC kernel driver.

---

## ⚙️ Architecture & Technical Design

On 2018+ Intel Macs, Apple replaced the legacy x86 I/O port `0x300` SMC interface with the **Apple T2 Security Chip**. The T2 runs bridgeOS and controls the fans over an internal PCIe mailbox.

```text
┌────────────────────────────────────────────────────────┐
│           MacFanControl.UI (WPF .NET 8)               │
│    (Fluent Dark Theme Dashboard + Dynamic Tray Icon)   │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                    MacFanControl.Core                  │
│    (Models, Hysteresis Fan Curve Engine, Settings)     │
└─────────────┬────────────────────────────┬─────────────┘
              │                            │
┌─────────────▼─────────────┐┌─────────────▼─────────────┐
│   MacFanControl.Hardware  ││     MacFanControl.SMC     │
│   (LibreHardwareMonitor)  ││    (Apple T2 SMC Engine)  │
│  - Intel Core i9 (DTS)    ││  - IOCTL DeviceIoControl  │
│  - AMD Radeon Pro (ADL)   ││  - FourCC Keys (F0Ac,FS!) │
└───────────────────────────┘└───────────────────────────┘
```

### SMC FourCC Key Mapping (MBP 16" 2019)

| Key | Type | Description |
| :--- | :--- | :--- |
| `FNum` | `ui8` | Total fan count (returns `2`) |
| `FS! ` | `ui16` | Fan status manual bitmask (Bit 0: Fan 0 Manual, Bit 1: Fan 1 Manual) |
| `F0Ac` | `fpe2` | Left Fan (CPU) actual speed in RPM |
| `F0Mn` | `fpe2` | Left Fan minimum speed (~1,800 RPM) |
| `F0Mx` | `fpe2` | Left Fan maximum speed (~5,616 RPM) |
| `F0Tg` | `fpe2` | Left Fan target speed in RPM |
| `F1Ac` | `fpe2` | Right Fan (GPU) actual speed in RPM |
| `F1Mn` | `fpe2` | Right Fan minimum speed (~1,800 RPM) |
| `F1Mx` | `fpe2` | Right Fan maximum speed (~5,616 RPM) |
| `F1Tg` | `fpe2` | Right Fan target speed in RPM |

---

## 🚀 Installation & Building Guide

### Option 1: Quick Build (Double-Click Batch File)

1. Boot into **Windows 10 Boot Camp**.
2. Clone this repository:
   ```cmd
   git clone https://github.com/mrrayyyy/MacFanControl-BootCamp.git
   cd MacFanControl-BootCamp
   ```
3. Double-click `scripts\build.bat` (or run it in Command Prompt).
4. The script will automatically restore NuGet packages and build a **single-file self-contained executable** at:
   ```text
   publish\MacFanControl.UI.exe
   ```
5. Right-click `publish\MacFanControl.UI.exe` and select **Run as administrator**.

---

### Option 2: Build via PowerShell

1. Open **PowerShell** as Administrator:
   ```powershell
   cd path\to\MacFanControl-BootCamp
   .\scripts\build.ps1
   ```
2. The output executable will be placed in `./publish/MacFanControl.UI.exe`.

---

### Option 3: Manual .NET CLI Build

If you prefer building manually with the .NET SDK:

```bash
# 1. Restore dependencies
dotnet restore src/MacFanControl.UI/MacFanControl.UI.csproj -r win-x64

# 2. Publish as single-file self-contained executable
dotnet publish src/MacFanControl.UI/MacFanControl.UI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

---

## 🖥️ Usage Guide

1. **Dashboard Overview**:
   * Watch live CPU Package and AMD GPU temperatures.
   * View live Left & Right fan speeds and percentages.
2. **Selecting a Mode**:
   * **Apple Default**: Lets Windows Boot Camp firmware control fans.
   * **Custom Curve**: Automatically throttles fans up and down based on target temperatures.
   * **Fixed Manual**: Drag the RPM slider to lock in a specific fan speed.
   * **100% Turbo**: Instantly sets both fans to maximum speed (~5,616 RPM).
3. **Profiles**:
   * **Aggressive (Gaming & Rendering)**: Begins ramping up at 45°C; hits 90% at 80°C and 100% at 85°C.
   * **Quiet (Office & Media)**: Stays quiet until 65°C; reaches 80% at 85°C.
4. **System Tray**:
   * Closing the application window (`X`) minimizes it directly to the system tray.
   * Hover over the tray icon or right-click to switch profiles on the fly.
   * To close completely, right-click the tray icon and select **Exit MacFanControl**.

---

## 🛡️ Safety & Failsafe Mechanism

* When the application is closed or the machine enters Sleep/Hibernate, `AppleSmcService.RestoreAppleDefaults()` is called immediately to clear manual control bits (`FS! = 0`).
* If the application crashes unexpectedly, Apple's hardware watchdog inside the T2 chip automatically reverts fan control to firmware defaults when heartbeat commands cease.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
