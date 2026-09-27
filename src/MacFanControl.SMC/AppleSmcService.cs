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

        string[] candidatePaths = { @"\\.\AppleSMC", @"\\.\SMC", @"\\.\AppleSMC0", @"\\.\AppleSMC1" };

        foreach (var path in candidatePaths)
        {
            DiagnosticLogger.Instance.Debug($"Attempting to open SMC device: {path}");
            try
            {
                var handle = SmcNative.CreateFile(
                    path,
                    SmcNative.GENERIC_READ | SmcNative.GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    SmcNative.OPEN_EXISTING,
                    SmcNative.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                int lastError = Marshal.GetLastWin32Error();

                if (handle != null && !handle.IsInvalid)
                {
                    _deviceHandle = handle;
                    _useSimulation = false;
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
            float rpm = SmcDataConverter.Fpe2ToFloat(f0Data);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F0Ac' (Left Fan) returned {rpm:F0} RPM.");
        }

        if (ReadSMC(SmcConstants.KEY_FAN1_ACTUAL, out var f1Data))
        {
            float rpm = SmcDataConverter.Fpe2ToFloat(f1Data);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'F1Ac' (Right Fan) returned {rpm:F0} RPM.");
        }

        if (ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var fsData))
        {
            ushort mask = SmcDataConverter.ToUInt16(fsData);
            DiagnosticLogger.Instance.Smc($"SMC Probe: Key 'FS! ' (Manual Status Mask) returned 0x{mask:X4}.");
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
            SmcConstants.KEY_FAN1_ACTUAL,
            SmcConstants.KEY_FAN1_MIN,
            SmcConstants.KEY_FAN1_MAX,
            SmcConstants.KEY_FAN1_TARGET,
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
            fan.CurrentRpm = SmcDataConverter.Fpe2ToFloat(dataActual);

        if (ReadSMC(keyMin, out var dataMin))
            fan.MinRpm = SmcDataConverter.Fpe2ToFloat(dataMin);

        if (ReadSMC(keyMax, out var dataMax))
            fan.MaxRpm = SmcDataConverter.Fpe2ToFloat(dataMax);

        if (ReadSMC(keyTarget, out var dataTarget))
            fan.TargetRpm = SmcDataConverter.Fpe2ToFloat(dataTarget);

        if (ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var manualData))
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

        if (!SetManualModeBit(fanIndex, true))
        {
            DiagnosticLogger.Instance.Error($"Failed to set manual mode bit for Fan {fanIndex}");
            return false;
        }

        uint keyTarget = fanIndex == 0 ? SmcConstants.KEY_FAN0_TARGET : SmcConstants.KEY_FAN1_TARGET;
        byte[] bytes = SmcDataConverter.FloatToFpe2(targetRpm);
        bool success = WriteSMC(keyTarget, bytes);

        if (success)
            DiagnosticLogger.Instance.Smc($"Set Fan {fanIndex} Target RPM -> {targetRpm:F0} (Key: {SmcConstants.FromFourCc(keyTarget)})");
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
            return SetManualModeBit(fanIndex, false);
        }
        else if (mode == FanMode.Turbo)
        {
            var info = GetFanInfo(fanIndex);
            return SetFanSpeed(fanIndex, info.MaxRpm);
        }

        return SetManualModeBit(fanIndex, true);
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
        DiagnosticLogger.Instance.Info("Restoring Apple Default SMC thermal management (Clearing manual bits)");
        if (_useSimulation)
        {
            foreach (var fan in _simulatedFans)
                fan.Mode = FanMode.AppleAuto;
            return;
        }

        WriteSMC(SmcConstants.KEY_FAN_MANUAL, new byte[] { 0, 0 });
    }

    private bool SetManualModeBit(int fanIndex, bool enable)
    {
        if (!ReadSMC(SmcConstants.KEY_FAN_MANUAL, out var currentData))
            return false;

        ushort mask = SmcDataConverter.ToUInt16(currentData);
        if (enable)
            mask |= (ushort)(1 << fanIndex);
        else
            mask &= (ushort)~(1 << fanIndex);

        return WriteSMC(SmcConstants.KEY_FAN_MANUAL, SmcDataConverter.FromUInt16(mask));
    }

    private bool ReadSMC(uint key, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (_deviceHandle == null || _deviceHandle.IsInvalid) return false;

        var inCmd = new SmcNative.SMC_KEY_DATA
        {
            Key = key,
            Bytes = new byte[32]
        };

        int size = Marshal.SizeOf<SmcNative.SMC_KEY_DATA>();
        bool result = SmcNative.DeviceIoControl(
            _deviceHandle,
            SmcNative.IOCTL_SMC_READ_KEY,
            ref inCmd,
            size,
            out var outCmd,
            size,
            out int bytesReturned,
            IntPtr.Zero);

        int win32Err = Marshal.GetLastWin32Error();

        if (result && outCmd.Result == 0)
        {
            data = outCmd.Bytes ?? Array.Empty<byte>();
            return true;
        }

        DiagnosticLogger.Instance.Debug($"ReadSMC [0x{key:X8} - {SmcConstants.FromFourCc(key)}] failed. Result: {result}, SmcResult: {outCmd.Result}, Win32Err: {win32Err}, BytesRet: {bytesReturned}");
        return false;
    }

    private bool WriteSMC(uint key, byte[] data)
    {
        if (_deviceHandle == null || _deviceHandle.IsInvalid) return false;

        var inCmd = new SmcNative.SMC_KEY_DATA
        {
            Key = key,
            Bytes = new byte[32]
        };

        if (data != null && data.Length > 0)
        {
            Array.Copy(data, inCmd.Bytes, Math.Min(data.Length, 32));
        }

        int size = Marshal.SizeOf<SmcNative.SMC_KEY_DATA>();
        bool result = SmcNative.DeviceIoControl(
            _deviceHandle,
            SmcNative.IOCTL_SMC_WRITE_KEY,
            ref inCmd,
            size,
            out var outCmd,
            size,
            out int bytesReturned,
            IntPtr.Zero);

        int win32Err = Marshal.GetLastWin32Error();

        if (result && outCmd.Result == 0)
            return true;

        DiagnosticLogger.Instance.Debug($"WriteSMC [0x{key:X8} - {SmcConstants.FromFourCc(key)}] failed. Result: {result}, SmcResult: {outCmd.Result}, Win32Err: {win32Err}, BytesRet: {bytesReturned}");
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
