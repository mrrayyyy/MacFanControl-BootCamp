namespace MacFanControl.Core.Models;

public enum FanMode
{
    /// <summary>
    /// Default Apple SMC automatic thermal management.
    /// </summary>
    AppleAuto = 0,

    /// <summary>
    /// User defined target RPM / percentage fixed speed.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Dynamic speed calculated using temperature vs RPM curve.
    /// </summary>
    Curve = 2,

    /// <summary>
    /// Instant 100% maximum cooling mode.
    /// </summary>
    Turbo = 3
}

public enum SensorTarget
{
    CpuPackage = 0,
    CpuMaxCore = 1,
    GpuCore = 2,
    MaxCpuGpu = 3
}
