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
  * **CPU**: Package temperature, Core Average (mean across all 8 cores), Max Core temperature, individual Cores #1–#8, power consumption (Watts), and utilization percentage via Intel DTS / MSR.
  * **GPU**: AMD Radeon Pro core temperature, hotspot temperature, GPU power, and load percentage via AMD ADL API.
  * **Fans**: Real-time RPM tracking and percentage gauges for both **Left Fan (CPU)** and **Right Fan (GPU)**.
* **Intelligent Independent Fan Control**:
  * **Dual Independent Tuning**: Configure Left Fan and Right Fan separately, each linked to any desired sensor.
  * **Sensor Selection**:
    * 📊 `CPU Core Average`: Dynamic real-time average across all CPU cores (Default for Left Fan).
    * 🔥 `CPU Max Core`: Automatically tracks the hottest core.
    * ⚡ `Core #1` to `Core #8`: Lock fan response to any specific individual core.
    * 📦 `CPU Package`: Overall processor package temperature.
    * 🎮 `GPU Core` & `GPU Hot Spot`: Dedicated graphics processor sensors.
    * 🔺 `Highest (CPU / GPU)`: Automatically tracks the higher of CPU or GPU temps.
  * **Interactive Numeric Steppers**: Up/Down arrow buttons (▲ / ▼) on every input box for easy 1°C / 5% stepping, with direct keyboard typing supported.
  * **Anti-Jitter Smooth Engine**: EMA (Exponential Moving Average) filtering + asymmetric slew-rate limiters (fast ramp-up, smooth gradual ramp-down) completely eliminates fan noise pulsating.
  * **100% Turbo Mode**: One-click instant maximum cooling (~5,616 RPM) for intensive rendering and gaming.
  * **Apple Auto Mode**: Restores Apple's native SMC firmware fan management.
  * **Fail-Safe Protection**: Automatically restores Apple defaults on app exit or crash.
* **Modern UI & Windows System Integration**:
  * **Fluent Dark UI**: Polished dark theme inspired by macOS design language, built with WPF on .NET 8.
  * **Dual Tray Icon**: Displays both CPU and GPU temperatures side-by-side on the Windows taskbar with color-coded alerts (Green < 60°C, Orange 60–78°C, Red > 78°C).
  * **Silent Windows Startup (UAC Bypass)**: Automatically registers a task in Windows Task Scheduler with `HighestAvailable` privileges, enabling seamless boot minimized to tray.

---

## 💻 System Requirements

* **Computer Model**: MacBook Pro (16-inch, 2019)
  * Model Identifiers: `MacBookPro16,1` or `MacBookPro16,4`
  * CPU: Intel Core i7-9750H, i9-9880H, or i9-9980HK
  * GPU: AMD Radeon Pro 5300M, 5500M, or 5600M
  * Controller: **Apple T2 Security Chip**
* **Operating System**: Windows 10 64-bit (or Windows 11) installed via Boot Camp.
* **Prerequisites**:
  * Apple Boot Camp Support Software drivers installed (`AppleSMC.sys`).
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
│    (Models, Anti-Jitter EMA & Slew-Rate Curve Engine)  │
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
| `F0Mn` | `fpe2` | Left Fan minimum speed (~1,836 RPM) |
| `F0Mx` | `fpe2` | Left Fan maximum speed (~5,616 RPM) |
| `F0Tg` | `fpe2` | Left Fan target speed in RPM |
| `F1Ac` | `fpe2` | Right Fan (GPU) actual speed in RPM |
| `F1Mn` | `fpe2` | Right Fan minimum speed (~1,700 RPM) |
| `F1Mx` | `fpe2` | Right Fan maximum speed (~5,200 RPM) |
| `F1Tg` | `fpe2` | Right Fan target speed in RPM |

---

## 🚀 Installation & Running

### Ready-to-Run Portable Package
The application is pre-packaged in the [`publish/`](file:///publish) directory:
```text
publish\
├── MacFanControl.UI.exe   # Single-file self-contained application
└── applesmc.sys           # Apple T2 SMC kernel driver
```

To run:
1. Right-click `publish\MacFanControl.UI.exe` and select **Run as administrator**.
2. If the Apple SMC driver service is not yet registered on your BootCamp installation, run `scripts\fix_smc_driver.bat` once as Administrator.

---

### Building from Source

**Option 1: Double-Click Batch File**
1. Double-click `scripts\build.bat`.
2. The single-file executable will be compiled to `publish\MacFanControl.UI.exe`.

**Option 2: .NET CLI**
```cmd
dotnet publish src/MacFanControl.UI/MacFanControl.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
copy /y "src\MacFanControl.UI\applesmc.sys" "publish\applesmc.sys"
```

---

## 🖥️ Usage Guide

1. **Dashboard Overview**:
   * Watch live CPU Package, GPU Core, and fan RPMs.
   * Switch between **Apple Default**, **Custom Temp Control**, and **100% Turbo Max**.
2. **Temperature-Based Fan Tuning**:
   * Set Min Temp and Min % (idle speed).
   * Set Max Temp and Max % (full throttle speed).
   * Use the **▲ / ▼** arrows to easily fine-tune thresholds.
3. **Settings & Auto-Start**:
   * Enable **Start with Windows (Boot Camp)** to launch silently at login via Task Scheduler with highest privileges (no UAC popup).
   * Enable **Start Minimized to Tray** for background operation.
4. **System Tray**:
   * Minimizing or closing the window sends the app to the Windows notification area.
   * The tray icon dynamically renders both CPU and GPU temperatures side-by-side in real-time.
   * Right-click the tray icon to switch fan modes or select **Exit**.

---

## 🛡️ Safety & Failsafe Mechanism

* When the application is closed or the machine enters Sleep/Hibernate, `AppleSmcService.RestoreAppleDefaults()` is called immediately to clear manual control bits (`FS! = 0`).
* If the application crashes unexpectedly, Apple's hardware watchdog inside the T2 chip automatically reverts fan control to firmware defaults when heartbeat commands cease.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
