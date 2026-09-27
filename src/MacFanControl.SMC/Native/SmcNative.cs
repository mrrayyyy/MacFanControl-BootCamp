using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MacFanControl.SMC.Native;

public static class SmcNative
{
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    // Standard IOCTL for Apple SMC driver on Boot Camp Windows
    public const uint IOCTL_SMC_INIT      = 0x00220020;
    public const uint IOCTL_SMC_READ_KEY  = 0x00220000;
    public const uint IOCTL_SMC_WRITE_KEY = 0x00220004;
    public const uint IOCTL_SMC_GET_KEY   = 0x00220008;
    public const uint IOCTL_SMC_KEY_INFO  = 0x0022000c;

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
        byte[]? lpInBuffer,
        int nInBufferSize,
        byte[]? lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

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

    // =====================================================================
    // Win32 Advapi32 Service Control Manager (SCM) APIs
    // =====================================================================
    public const uint SC_MANAGER_CONNECT             = 0x0001;
    public const uint SC_MANAGER_CREATE_SERVICE      = 0x0002;
    public const uint SC_MANAGER_ENUMERATE_SERVICE   = 0x0004;
    public const uint SC_MANAGER_LOCK                = 0x0008;
    public const uint SC_MANAGER_QUERY_LOCK_STATUS   = 0x0010;
    public const uint SC_MANAGER_MODIFY_BOOT_CONFIG  = 0x0020;
    public const uint SC_MANAGER_ALL_ACCESS          = 0xF003F;

    public const uint SERVICE_QUERY_CONFIG           = 0x0001;
    public const uint SERVICE_CHANGE_CONFIG          = 0x0002;
    public const uint SERVICE_QUERY_STATUS           = 0x0004;
    public const uint SERVICE_ENUMERATE_DEPENDENTS   = 0x0008;
    public const uint SERVICE_START                  = 0x0010;
    public const uint SERVICE_STOP                   = 0x0020;
    public const uint SERVICE_PAUSE_CONTINUE         = 0x0040;
    public const uint SERVICE_INTERROGATE            = 0x0080;
    public const uint SERVICE_USER_DEFINED_CONTROL   = 0x0100;
    public const uint SERVICE_ALL_ACCESS             = 0xF01FF;

    public const uint SERVICE_KERNEL_DRIVER          = 0x00000001;
    public const uint SERVICE_FILE_SYSTEM_DRIVER     = 0x00000002;

    public const uint SERVICE_BOOT_START             = 0x00000000;
    public const uint SERVICE_SYSTEM_START           = 0x00000001;
    public const uint SERVICE_AUTO_START             = 0x00000002;
    public const uint SERVICE_DEMAND_START           = 0x00000003;
    public const uint SERVICE_DISABLED               = 0x00000004;

    public const uint SERVICE_ERROR_IGNORE           = 0x00000000;
    public const uint SERVICE_ERROR_NORMAL           = 0x00000001;
    public const uint SERVICE_ERROR_SEVERE           = 0x00000002;
    public const uint SERVICE_ERROR_CRITICAL         = 0x00000003;

    public const uint SERVICE_STOPPED                = 1;
    public const uint SERVICE_START_PENDING          = 2;
    public const uint SERVICE_STOP_PENDING           = 3;
    public const uint SERVICE_RUNNING                = 4;
    public const uint SERVICE_CONTINUE_PENDING       = 5;
    public const uint SERVICE_PAUSE_PENDING          = 6;
    public const uint SERVICE_PAUSED                 = 7;

    public const uint SERVICE_NO_CHANGE              = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr OpenSCManager(
        string? lpMachineName,
        string? lpDatabaseName,
        uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr OpenService(
        IntPtr hSCManager,
        string lpServiceName,
        uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool StartService(
        IntPtr hService,
        uint dwNumServiceArgs,
        IntPtr lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool QueryServiceStatus(
        IntPtr hService,
        ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool ChangeServiceConfig(
        IntPtr hService,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string? lpBinaryPathName,
        string? lpLoadOrderGroup,
        IntPtr lpdwTagId,
        string? lpDependencies,
        string? lpServiceStartName,
        string? lpPassword,
        string? lpDisplayName);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateService(
        IntPtr hSCManager,
        string lpServiceName,
        string lpDisplayName,
        uint dwDesiredAccess,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string lpBinaryPathName,
        string? lpLoadOrderGroup,
        IntPtr lpdwTagId,
        string? lpDependencies,
        string? lpServiceStartName,
        string? lpPassword);
}
