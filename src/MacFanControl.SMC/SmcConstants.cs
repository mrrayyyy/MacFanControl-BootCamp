namespace MacFanControl.SMC;

public static class SmcConstants
{
    // FourCC Helper to convert string to uint32
    public static uint ToFourCc(string key)
    {
        if (key.Length != 4)
            throw new ArgumentException("SMC key must be exactly 4 characters.", nameof(key));

        return ((uint)key[0] << 24) |
               ((uint)key[1] << 16) |
               ((uint)key[2] << 8)  |
               ((uint)key[3]);
    }

    public static string FromFourCc(uint code)
    {
        return new string(new[]
        {
            (char)((code >> 24) & 0xFF),
            (char)((code >> 16) & 0xFF),
            (char)((code >> 8) & 0xFF),
            (char)(code & 0xFF)
        });
    }

    // Common Apple SMC Keys
    public static readonly uint KEY_FAN_COUNT = ToFourCc("FNum"); // UInt8
    public static readonly uint KEY_FAN_MANUAL = ToFourCc("FS! "); // UInt16 (Bitmask: 1 = manual, 0 = auto)

    // Fan 0 (Left Fan - CPU Side on MBP 16" 2019)
    public static readonly uint KEY_FAN0_ACTUAL = ToFourCc("F0Ac"); // fpe2
    public static readonly uint KEY_FAN0_MIN    = ToFourCc("F0Mn"); // fpe2
    public static readonly uint KEY_FAN0_MAX    = ToFourCc("F0Mx"); // fpe2
    public static readonly uint KEY_FAN0_TARGET = ToFourCc("F0Tg"); // fpe2

    // Fan 1 (Right Fan - GPU Side on MBP 16" 2019)
    public static readonly uint KEY_FAN1_ACTUAL = ToFourCc("F1Ac"); // fpe2
    public static readonly uint KEY_FAN1_MIN    = ToFourCc("F1Mn"); // fpe2
    public static readonly uint KEY_FAN1_MAX    = ToFourCc("F1Mx"); // fpe2
    public static readonly uint KEY_FAN1_TARGET = ToFourCc("F1Tg"); // fpe2

    // Temperatures
    public static readonly uint KEY_CPU_TEMP_PROX = ToFourCc("TC0P"); // sp78
    public static readonly uint KEY_GPU_TEMP_PROX = ToFourCc("TG0P"); // sp78
}
