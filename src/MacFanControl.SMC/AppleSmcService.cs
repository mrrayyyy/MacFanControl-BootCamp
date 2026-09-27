using MacFanControl.Core.Interfaces;
using MacFanControl.Core.Models;
using MacFanControl.SMC.Native;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace MacFanControl.SMC;

public class AppleSmcService : ISmcService
{
    private SafeFileHandle? _deviceHandle;
    private bool _isDisposed;
    private readonly bool _useSimulation;

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
        // Try opening physical device; if not available, enable simulation mode
        if (!Open())
        {
            _useSimulation = true;
        }
    }

    public bool Open()
    {
        if (_deviceHandle != null && !_deviceHandle.IsInvalid)
            return true;

        string[] candidatePaths = { @"\\.\AppleSMC", @"\\.\SMC", @"\\.\AppleSMC0" };

        foreach (var path in candidatePaths)
        {
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

                if (handle != null && !handle.IsInvalid)
                {
                    _deviceHandle = handle;
                    return true;
                }
            }
            catch
            {
                // Try next candidate
            }
        }

        return false;
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
            // Simulate natural slight RPM fluctuation
            var sim = _simulatedFans[fanIndex];
            if (sim.Mode != FanMode.AppleAuto)
            {
                // Drift current RPM toward target RPM
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

        // Check if manual bit is set in FS!
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

        // 1. Enable manual mode for this fan in FS!
        if (!SetManualModeBit(fanIndex, true))
            return false;

        // 2. Write target RPM
        uint keyTarget = fanIndex == 0 ? SmcConstants.KEY_FAN0_TARGET : SmcConstants.KEY_FAN1_TARGET;
        byte[] bytes = SmcDataConverter.FloatToFpe2(targetRpm);
        return WriteSMC(keyTarget, bytes);
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
        if (_useSimulation)
        {
            foreach (var fan in _simulatedFans)
                fan.Mode = FanMode.AppleAuto;
            return;
        }

        // Clear manual bits: write 0 to FS!
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
            out _,
            IntPtr.Zero);

        if (result && outCmd.Result == 0)
        {
            data = outCmd.Bytes ?? Array.Empty<byte>();
            return true;
        }

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
            out _,
            IntPtr.Zero);

        return result && outCmd.Result == 0;
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
