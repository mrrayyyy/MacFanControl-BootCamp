using MacFanControl.Core.Models;

namespace MacFanControl.Core.Interfaces;

public interface ISensorService : IDisposable
{
    bool IsAvailable { get; }
    Task InitializeAsync();
    HardwareOverview ReadHardwareOverview();
    IReadOnlyList<SensorInfo> GetAllSensors();
}

public interface ISmcService : IDisposable
{
    bool IsConnected { get; }
    string DeviceModel { get; }
    int FanCount { get; }

    bool Open();
    void Close();

    FanInfo GetFanInfo(int fanIndex);
    bool SetFanSpeed(int fanIndex, float targetRpm);
    bool SetFanMode(int fanIndex, FanMode mode);
    bool SetAllFansMode(FanMode mode, float targetRpm = 0);
    void RestoreAppleDefaults();
}
