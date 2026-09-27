using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MacFanControl.SMC.Native;

public static class SmcNative
{
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    // Standard IOCTL for Apple SMC driver on Boot Camp Windows
    public const uint IOCTL_SMC_READ_KEY  = 0x00220004;
    public const uint IOCTL_SMC_WRITE_KEY = 0x00220008;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SMC_VERSION
    {
        public byte Major;
        public byte Minor;
        public byte Build;
        public byte Reserved;
        public ushort Release;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SMC_KEY_INFO_DATA
    {
        public uint DataSize;
        public uint DataType;
        public byte DataAttributes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SMC_KEY_DATA
    {
        public uint Key;
        public SMC_VERSION Vers;
        public SMC_KEY_INFO_DATA KeyInfo;
        public byte Result;
        public byte Status;
        public byte Data8;
        public uint Data32;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Bytes;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref SMC_KEY_DATA lpInBuffer,
        int nInBufferSize,
        out SMC_KEY_DATA lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}
