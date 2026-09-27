namespace MacFanControl.SMC;

public static class SmcDataConverter
{
    /// <summary>
    /// Converts SMC 'fpe2' format (16-bit unsigned fixed-point 14.2) to float RPM.
    /// </summary>
    public static float Fpe2ToFloat(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 2) return 0f;
        int raw = (bytes[0] << 8) | bytes[1];
        return raw / 4.0f;
    }

    /// <summary>
    /// Converts target RPM float to SMC 'fpe2' 2-byte array.
    /// </summary>
    public static byte[] FloatToFpe2(float value)
    {
        int raw = (int)Math.Round(value * 4.0f);
        raw = Math.Clamp(raw, 0, 0xFFFF);
        return new byte[] { (byte)((raw >> 8) & 0xFF), (byte)(raw & 0xFF) };
    }

    /// <summary>
    /// Converts SMC 'sp78' format (16-bit signed fixed-point 7.8) to float temperature (°C).
    /// </summary>
    public static float Sp78ToFloat(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 2) return 0f;
        short raw = (short)((bytes[0] << 8) | bytes[1]);
        return raw / 256.0f;
    }

    /// <summary>
    /// Converts byte array to UInt16 (Big-Endian).
    /// </summary>
    public static ushort ToUInt16(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 2) return 0;
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    /// <summary>
    /// Converts UInt16 to 2-byte array (Big-Endian).
    /// </summary>
    public static byte[] FromUInt16(ushort value)
    {
        return new byte[] { (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };
    }
}
