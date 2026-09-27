using System.Runtime.InteropServices;
using MacFanControl.Core.Interfaces;
using MacFanControl.Core.Models;
using MacFanControl.Core.Services;
using MacFanControl.SMC.Native;
using Microsoft.Win32.SafeHandles;

namespace MacFanControl.SMC;

public class AppleSmcService : ISmcService
{
    private SafeFileHandle? _deviceHandle;
    private bool _isDisposed;
    private bool _useSimulation;

    // Simulated states for testing/development
    private readonly FanInfo[] _simulatedFans = new FanInfo[]
    {
        new() { Index = 0, Name = "Left Fan (CPU)", MinRpm = 1800, MaxRpm = 5616, CurrentRpm = 2150, TargetRpm = 2150, Mode = FanMode.AppleAuto },
        new() { Index = 1, Name = "Right Fan (GPU)", MinRpm = 1800, MaxRpm = 5616, CurrentRpm = 2150, TargetRpm = 2150, Mode = FanMode.AppleAuto }
    };

    public bool IsConnected => (_deviceHandle != null && !_deviceHandle.IsInvalid) || _useSimulation;
    public string DeviceModel => "MacBookPro16,1 (2019 16\" i9 + AMD Radeon)";
    public int FanCount => 2;

    public AppleSmcService()
    {
        DiagnosticLogger.Instance.Info("AppleSmcService initializing...");
        if (!Open())
        {
            DiagnosticLogger.Instance.Warn("Could not open physical Apple SMC device handle. Falling back to Simulation Mode for development/testing.");
            _useSimulation = true;
        }
        else
        {
            DiagnosticLogger.Instance.Info("Physical Apple SMC device connected successfully!");
            ProbeSmcCapabilities();
        }
    }

    public bool Open()
    {
        if (_deviceHandle != null && !_deviceHandle.IsInvalid)
            return true;

        // Attempt 1: Try opening candidate device paths directly
        if (TryOpenCandidateDevices(out var handle, out var openedPath))
        {
            return FinalizeDeviceConnection(handle, openedPath);
        }

        // If direct open failed, the kernel driver service might not be running (e.g. after a system reboot)
        DiagnosticLogger.Instance.Warn("Could not open SMC device handle. Checking and starting AppleSMC driver service...");
        bool serviceStarted = SmcDriverServiceManager.EnsureDriverServiceRunning();

        if (serviceStarted)
        {
            // Allow brief moment for Windows Object Manager to expose the device symlink
            Thread.Sleep(200);

            // Attempt 2: Retry opening after starting driver service
            if (TryOpenCandidateDevices(out handle, out openedPath))
            {
                return FinalizeDeviceConnection(handle, openedPath);
            }
        }

        return false;
    }

    private static bool TryOpenCandidateDevices([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SafeFileHandle? validHandle, out string openedPath)
    {
        validHandle = null;
        openedPath = string.Empty;

        string[] candidatePaths = { @"\\.\AppleSMC", @"\\.\SMC", @"\\.\AppleSMC0", @"\\.\AppleSMC1" };

        foreach (var path in candidatePaths)
        {
            DiagnosticLogger.Instance.Debug($"Attempting to open SMC device: {path}");
            try
            {
                var handle = SmcNative.CreateFile(
                    path,
                    SmcNative.GENERIC_READ | SmcNative.GENERIC_WRITE,
                    SmcNative.FILE_SHARE_READ | SmcNative.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    SmcNative.OPEN_EXISTING,
                    SmcNative.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                int lastError = Marshal.GetLastWin32Error();

                if (handle != null && !handle.IsInvalid)
                {
                    validHandle = handle;
                    openedPath = path;
                    DiagnosticLogger.Instance.Info($"Successfully opened handle to {path} (Win32 Error: {lastError})");
                    return true;
                }
                else
                {
                    DiagnosticLogger.Instance.Debug($"Failed to open {path}. Win32 Error: {lastError} ({(lastError == 5 ? "Access Denied - Run as Admin!" : lastError == 2 ? "Device Not Found" : "Other")})");
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Instance.Error($"Exception while opening {path}: {ex.Message}");
            }
        }

        return false;
    }

    private bool FinalizeDeviceConnection(SafeFileHandle handle, string openedPath)
    {
        _deviceHandle = handle;
        _useSimulation = false;

        // Initialize Apple SMC communication
        byte[] initOut = new byte[1];
        SmcNative.DeviceIoControl(handle, SmcNative.IOCTL_SMC_INIT, null, 0, initOut, 1, out _, IntPtr.Zero);

        DiagnosticLogger.Instance.Info($"Apple SMC device connected successfully on {openedPath}!");
        return true;
    }

    private void ProbeSmcCapabilities()
    {
        if (ReadSMC(SmcConstants.KEY_FAN_COUNT, out var fanCountData))
        {
            byte fans = fanCountData.Length > 0 ? fanCountData[0] : (byte)0;
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'FNum' returned {fans} fans.");
        }
        else
        {
            DiagnosticLogger.Instance.Warn("SMC Probe: Key 'FNum' read failed or returned empty.");
        }

        if (ReadSMC(SmcConstants.KEY_FAN0_ACTUAL, out var f0Data))
        {
            float rpm = SmcDataConverter.BytesToRpm(f0Data);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F0Ac' (Left Fan) returned {rpm:F0} RPM.");
        }

        if (ReadSMC(SmcConstants.KEY_FAN1_ACTUAL, out var f1Data))
        {
            float rpm = SmcDataConverter.BytesToRpm(f1Data);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F1Ac' (Right Fan) returned {rpm:F0} RPM.");
        }

        if (ReadSMC(SmcConstants.KEY_FAN0_MODE, out var f0m) && f0m.Length > 0)
        {
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F0Md' (Left Fan Mode) returned {f0m[0]} ({(f0m[0] != 0 ? "Forced/Manual" : "Apple Auto")}).");
        }

        if (ReadSMC(SmcConstants.KEY_FAN1_MODE, out var f1m) && f1m.Length > 0)
        {
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F1Md' (Right Fan Mode) returned {f1m[0]} ({(f1m[0] != 0 ? "Forced/Manual" : "Apple Auto")}).");
        }

        if (ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var fsData))
        {
            ushort mask = SmcDataConverter.ToUInt16(fsData);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'FS! ' (Legacy Manual Status Mask) returned 0x{mask:X4}.");
        }
    }

    public void RunFullDiagnostic()
    {
        DiagnosticLogger.Instance.Info("=== STARTING FULL APPLE SMC DIAGNOSTIC PROBE ===");
        DiagnosticLogger.Instance.Info($"Connection Mode: {(_useSimulation ? "SIMULATION (Driver Not Loaded)" : "PHYSICAL KERNEL DRIVER")}");

        uint[] probeKeys = new uint[]
        {
            SmcConstants.ToFourCc("#KEY"),
            SmcConstants.KEY_FAN_COUNT,
            SmcConstants.KEY_FAN_MANUAL,
            SmcConstants.KEY_FAN0_ACTUAL,
            SmcConstants.KEY_FAN0_MIN,
            SmcConstants.KEY_FAN0_MAX,
            SmcConstants.KEY_FAN0_TARGET,
            SmcConstants.KEY_FAN0_MODE,
            SmcConstants.KEY_FAN1_ACTUAL,
            SmcConstants.KEY_FAN1_MIN,
            SmcConstants.KEY_FAN1_MAX,
            SmcConstants.KEY_FAN1_TARGET,
            SmcConstants.KEY_FAN1_MODE,
            SmcConstants.KEY_CPU_TEMP_PROX,
            SmcConstants.KEY_GPU_TEMP_PROX
        };

        foreach (var key in probeKeys)
        {
            string keyStr = SmcConstants.FromFourCc(key);
            if (ReadSMC(key, out var data))
            {
                string hex = BitConverter.ToString(data).Replace("-", " ");
                DiagnosticLogger.Instance.Smc($"[KEY: {keyStr}] SUCCESS -> Raw: {hex}");
            }
            else
            {
                DiagnosticLogger.Instance.Warn($"[KEY: {keyStr}] FAILED -> DeviceIoControl returned false or result != 0");
            }
        }

        DiagnosticLogger.Instance.Info("=== FULL APPLE SMC DIAGNOSTIC PROBE COMPLETED ===");
    }

    public void Close()
    {
        RestoreAppleDefaults();
        _deviceHandle?.Dispose();
        _deviceHandle = null;
    }

    public FanInfo GetFanInfo(int fanIndex)
    {
        if (fanIndex < 0 || fanIndex >= FanCount)
            throw new ArgumentOutOfRangeException(nameof(fanIndex));

        if (_useSimulation || _deviceHandle == null || _deviceHandle.IsInvalid)
        {
            var sim = _simulatedFans[fanIndex];
            if (sim.Mode != FanMode.AppleAuto)
            {
                sim.CurrentRpm += (sim.TargetRpm - sim.CurrentRpm) * 0.25f;
            }
            return sim;
        }

        var fan = new FanInfo
        {
            Index = fanIndex,
            Name = fanIndex == 0 ? "Left Fan (CPU)" : "Right Fan (GPU)"
        };

        uint keyActual = fanIndex == 0 ? SmcConstants.KEY_FAN0_ACTUAL : SmcConstants.KEY_FAN1_ACTUAL;
        uint keyMin    = fanIndex == 0 ? SmcConstants.KEY_FAN0_MIN    : SmcConstants.KEY_FAN1_MIN;
        uint keyMax    = fanIndex == 0 ? SmcConstants.KEY_FAN0_MAX    : SmcConstants.KEY_FAN1_MAX;
        uint keyTarget = fanIndex == 0 ? SmcConstants.KEY_FAN0_TARGET : SmcConstants.KEY_FAN1_TARGET;

        if (ReadSMC(keyActual, out var dataActual))
            fan.CurrentRpm = SmcDataConverter.BytesToRpm(dataActual);

        if (ReadSMC(keyMin, out var dataMin))
            fan.MinRpm = SmcDataConverter.BytesToRpm(dataMin);

        if (ReadSMC(keyMax, out var dataMax))
            fan.MaxRpm = SmcDataConverter.BytesToRpm(dataMax);

        if (ReadSMC(keyTarget, out var dataTarget))
            fan.TargetRpm = SmcDataConverter.BytesToRpm(dataTarget);

        // MBP 16 2019 fallback defaults if SMC keys return 0
        if (fan.MinRpm <= 0) fan.MinRpm = 1800;
        if (fan.MaxRpm <= 0) fan.MaxRpm = 5616;
        if (fan.CurrentRpm <= 0) fan.CurrentRpm = fan.MinRpm;
        if (fan.TargetRpm <= 0) fan.TargetRpm = fan.CurrentRpm;

        uint keyMode = fanIndex == 0 ? SmcConstants.KEY_FAN0_MODE : SmcConstants.KEY_FAN1_MODE;
        if (ReadSMC(keyMode, out var modeData) && modeData.Length > 0)
        {
            fan.Mode = modeData[0] != 0 ? FanMode.Manual : FanMode.AppleAuto;
        }
        else if (ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var manualData) && manualData.Length >= 2)
        {
            ushort mask = SmcDataConverter.ToUInt16(manualData);
            bool isManual = (mask & (1 << fanIndex)) != 0;
            fan.Mode = isManual ? FanMode.Manual : FanMode.AppleAuto;
        }

        return fan;
    }

    public bool SetFanSpeed(int fanIndex, float targetRpm)
    {
        if (fanIndex < 0 || fanIndex >= FanCount) return false;

        if (_useSimulation || _deviceHandle == null || _deviceHandle.IsInvalid)
        {
            _simulatedFans[fanIndex].TargetRpm = Math.Clamp(targetRpm, _simulatedFans[fanIndex].MinRpm, _simulatedFans[fanIndex].MaxRpm);
            _simulatedFans[fanIndex].Mode = FanMode.Manual;
            return true;
        }

        // 1. Enable manual mode for this fan (via T2 FxMd or legacy FS!)
        SetManualMode(fanIndex, true);

        // 2. Clamp target RPM between fan min and max
        var currentInfo = GetFanInfo(fanIndex);
        float clampedRpm = Math.Clamp(targetRpm, currentInfo.MinRpm, currentInfo.MaxRpm);

        // 3. Write target RPM as 4-byte float to F0Tg / F1Tg
        uint keyTarget = fanIndex == 0 ? SmcConstants.KEY_FAN0_TARGET : SmcConstants.KEY_FAN1_TARGET;
        byte[] bytes = SmcDataConverter.RpmToBytes(clampedRpm, 4);
        bool success = WriteSMC(keyTarget, bytes);

        if (success)
            DiagnosticLogger.Instance.Smc($"Set Fan {fanIndex} Target RPM -> {clampedRpm:F0} (Key: {SmcConstants.FromFourCc(keyTarget)})");
        else
            DiagnosticLogger.Instance.Error($"Failed to write target RPM to Fan {fanIndex}");

        return success;
    }

    public bool SetFanMode(int fanIndex, FanMode mode)
    {
        if (fanIndex < 0 || fanIndex >= FanCount) return false;

        if (_useSimulation || _deviceHandle == null || _deviceHandle.IsInvalid)
        {
            _simulatedFans[fanIndex].Mode = mode;
            if (mode == FanMode.Turbo)
                _simulatedFans[fanIndex].TargetRpm = _simulatedFans[fanIndex].MaxRpm;
            else if (mode == FanMode.AppleAuto)
                _simulatedFans[fanIndex].TargetRpm = _simulatedFans[fanIndex].MinRpm;
            return true;
        }

        DiagnosticLogger.Instance.Info($"Switching Fan {fanIndex} to Mode: {mode}");

        if (mode == FanMode.AppleAuto)
        {
            return SetManualMode(fanIndex, false);
        }
        else if (mode == FanMode.Turbo)
        {
            var info = GetFanInfo(fanIndex);
            return SetFanSpeed(fanIndex, info.MaxRpm);
        }

        return SetManualMode(fanIndex, true);
    }

    public bool SetAllFansMode(FanMode mode, float targetRpm = 0)
    {
        bool success = true;
        for (int i = 0; i < FanCount; i++)
        {
            if (mode == FanMode.Manual && targetRpm > 0)
                success &= SetFanSpeed(i, targetRpm);
            else
                success &= SetFanMode(i, mode);
        }
        return success;
    }

    public void RestoreAppleDefaults()
    {
        DiagnosticLogger.Instance.Info("Restoring Apple Default SMC thermal management (Restoring Auto Mode)");
        if (_useSimulation)
        {
            foreach (var fan in _simulatedFans)
                fan.Mode = FanMode.AppleAuto;
            return;
        }

        for (int i = 0; i < FanCount; i++)
        {
            SetManualMode(i, false);
        }

        // Also clear legacy mask if supported
        WriteSMC(SmcConstants.KEY_FAN_MANUAL, new byte[] { 0, 0 });
    }

    private bool SetManualMode(int fanIndex, bool enable)
    {
        bool success = false;
        byte modeVal = enable ? (byte)1 : (byte)0;

        // 1. Apple T2 mode key: F0Md / F1Md (ui8)
        uint keyMode = fanIndex == 0 ? SmcConstants.KEY_FAN0_MODE : SmcConstants.KEY_FAN1_MODE;
        if (WriteSMC(keyMode, new byte[] { modeVal }))
        {
            DiagnosticLogger.Instance.Debug($"SetManualMode: Key '{SmcConstants.FromFourCc(keyMode)}' -> {modeVal} (T2 mode)");
            success = true;
        }

        // 2. Legacy non-T2 Mac mode bitmask: FS!
        if (ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var currentData) && currentData.Length >= 2)
        {
            ushort mask = SmcDataConverter.ToUInt16(currentData);
            if (enable)
                mask |= (ushort)(1 << fanIndex);
            else
                mask &= (ushort)~(1 << fanIndex);

            if (WriteSMC(SmcConstants.KEY_FAN_MANUAL, SmcDataConverter.FromUInt16(mask)))
            {
                DiagnosticLogger.Instance.Debug($"SetManualMode: Key 'FS! ' mask updated to 0x{mask:X4}");
                success = true;
            }
        }

        return success;
    }

    private bool ReadSMC(uint key, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (_deviceHandle == null || _deviceHandle.IsInvalid) return false;

        byte[] keyBytes = BitConverter.GetBytes(key);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(keyBytes);

        // 1. Query Key Info (size and type) via IOCTL_SMC_KEY_INFO
        byte[] infoOut = new byte[6];
        bool infoRes = SmcNative.DeviceIoControl(
            _deviceHandle,
            SmcNative.IOCTL_SMC_KEY_INFO,
            keyBytes,
            4,
            infoOut,
            6,
            out int infoRet,
            IntPtr.Zero);

        byte dataSize = infoRes && infoRet >= 6 ? infoOut[0] : (byte)4;
        if (dataSize == 0 || dataSize > 32) dataSize = 4;

        // 2. Read Key via IOCTL_SMC_READ_KEY (0x00220000)
        byte[] inBuf = new byte[5];
        Array.Copy(keyBytes, inBuf, 4);
        inBuf[4] = dataSize;

        byte[] outBuf = new byte[32];
        bool result = SmcNative.DeviceIoControl(
            _deviceHandle,
            SmcNative.IOCTL_SMC_READ_KEY,
            inBuf,
            5,
            outBuf,
            32,
            out int bytesReturned,
            IntPtr.Zero);

        int win32Err = Marshal.GetLastWin32Error();

        if (result && bytesReturned > 0)
        {
            data = new byte[bytesReturned];
            Array.Copy(outBuf, data, bytesReturned);
            return true;
        }

        DiagnosticLogger.Instance.Debug($"ReadSMC [{SmcConstants.FromFourCc(key)}] failed. Result: {result}, Win32Err: {win32Err}, BytesRet: {bytesReturned}");
        return false;
    }

    private bool WriteSMC(uint key, byte[] data)
    {
        if (_deviceHandle == null || _deviceHandle.IsInvalid) return false;

        byte[] keyBytes = BitConverter.GetBytes(key);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(keyBytes);

        byte dataLen = (byte)(data != null ? Math.Min(data.Length, 32) : 0);
        byte[] inBuf = new byte[5 + dataLen];
        Array.Copy(keyBytes, inBuf, 4);
        inBuf[4] = dataLen;
        if (data != null && dataLen > 0)
        {
            Array.Copy(data, 0, inBuf, 5, dataLen);
        }

        byte[] outBuf = new byte[1];
        bool result = SmcNative.DeviceIoControl(
            _deviceHandle,
            SmcNative.IOCTL_SMC_WRITE_KEY,
            inBuf,
            inBuf.Length,
            outBuf,
            1,
            out int bytesReturned,
            IntPtr.Zero);

        int win32Err = Marshal.GetLastWin32Error();

        if (result)
            return true;

        DiagnosticLogger.Instance.Debug($"WriteSMC [{SmcConstants.FromFourCc(key)}] failed. Result: {result}, Status: {outBuf[0]}, Win32Err: {win32Err}");
        return false;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Close();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
