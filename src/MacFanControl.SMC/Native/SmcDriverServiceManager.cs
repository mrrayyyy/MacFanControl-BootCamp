using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using MacFanControl.Core.Services;

namespace MacFanControl.SMC.Native;

public static class SmcDriverServiceManager
{
    private const string ServiceName = "applesmc";
    private const string ServiceDisplayName = "Apple SMC service";

    /// <summary>
    /// Ensures that the Apple SMC kernel driver service is installed, configured for Automatic startup,
    /// and actively in the RUNNING state.
    /// </summary>
    /// <returns>True if the driver service is running; otherwise false.</returns>
    public static bool EnsureDriverServiceRunning()
    {
        DiagnosticLogger.Instance.Info("Checking Apple SMC kernel driver service status...");

        IntPtr scm = SmcNative.OpenSCManager(null, null, SmcNative.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
        {
            scm = SmcNative.OpenSCManager(null, null, SmcNative.SC_MANAGER_CONNECT);
        }

        if (scm == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            DiagnosticLogger.Instance.Warn($"Cannot open Service Control Manager (Win32 Error: {err}). Trying fallback via sc.exe...");
            return TryStartViaScCommand();
        }

        try
        {
            IntPtr service = SmcNative.OpenService(
                scm,
                ServiceName,
                SmcNative.SERVICE_ALL_ACCESS);

            if (service == IntPtr.Zero)
            {
                service = SmcNative.OpenService(
                    scm,
                    ServiceName,
                    SmcNative.SERVICE_START | SmcNative.SERVICE_QUERY_STATUS | SmcNative.SERVICE_CHANGE_CONFIG);
            }

            // If the service doesn't exist, try to locate applesmc.sys and register it
            if (service == IntPtr.Zero)
            {
                DiagnosticLogger.Instance.Warn("AppleSMC service not registered. Searching for driver file...");
                string? driverPath = FindDriverFile();

                if (!string.IsNullOrEmpty(driverPath))
                {
                    DiagnosticLogger.Instance.Info($"Registering AppleSMC kernel service with binary: {driverPath}");
                    service = SmcNative.CreateService(
                        scm,
                        ServiceName,
                        ServiceDisplayName,
                        SmcNative.SERVICE_ALL_ACCESS,
                        SmcNative.SERVICE_KERNEL_DRIVER,
                        SmcNative.SERVICE_AUTO_START,
                        SmcNative.SERVICE_ERROR_NORMAL,
                        driverPath,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null);

                    if (service == IntPtr.Zero)
                    {
                        int createErr = Marshal.GetLastWin32Error();
                        DiagnosticLogger.Instance.Warn($"Failed to create AppleSMC service (Win32 Error: {createErr})");
                    }
                }
                else
                {
                    DiagnosticLogger.Instance.Warn("Could not locate applesmc.sys on disk to register service.");
                }
            }

            if (service != IntPtr.Zero)
            {
                try
                {
                    // Ensure the service is configured to start automatically on Windows boot
                    // and permanently points to System32 or local driver without depending on 3rd-party folders
                    string? permanentDriver = FindDriverFile();

                    SmcNative.ChangeServiceConfig(
                        service,
                        SmcNative.SERVICE_NO_CHANGE,
                        SmcNative.SERVICE_AUTO_START,
                        SmcNative.SERVICE_NO_CHANGE,
                        permanentDriver,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        null);

                    // Query current status
                    var status = new SmcNative.SERVICE_STATUS();
                    if (SmcNative.QueryServiceStatus(service, ref status))
                    {
                        if (status.dwCurrentState == SmcNative.SERVICE_RUNNING)
                        {
                            DiagnosticLogger.Instance.Info("AppleSMC kernel driver service is already RUNNING.");
                            return true;
                        }

                        DiagnosticLogger.Instance.Info($"Starting AppleSMC driver service (Current state: {status.dwCurrentState})...");
                        if (!SmcNative.StartService(service, 0, IntPtr.Zero))
                        {
                            int startErr = Marshal.GetLastWin32Error();
                            // 1056 = ERROR_SERVICE_ALREADY_RUNNING
                            if (startErr != 1056)
                            {
                                DiagnosticLogger.Instance.Warn($"StartService returned Win32 Error: {startErr}");
                            }
                        }

                        // Wait up to 3 seconds for service to enter RUNNING state
                        for (int i = 0; i < 30; i++)
                        {
                            Thread.Sleep(100);
                            if (SmcNative.QueryServiceStatus(service, ref status) &&
                                status.dwCurrentState == SmcNative.SERVICE_RUNNING)
                            {
                                DiagnosticLogger.Instance.Info("AppleSMC driver service successfully reached RUNNING state!");
                                return true;
                            }
                        }

                        DiagnosticLogger.Instance.Warn($"Timeout waiting for AppleSMC service to start. Current state: {status.dwCurrentState}");
                    }
                }
                finally
                {
                    SmcNative.CloseServiceHandle(service);
                }
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.Error($"Exception managing AppleSMC service: {ex.Message}");
        }
        finally
        {
            SmcNative.CloseServiceHandle(scm);
        }

        return TryStartViaScCommand();
    }

    private static string? FindDriverFile()
    {
        string system32Driver = Path.Combine(Environment.SystemDirectory, @"drivers\applesmc.sys");
        string localDriver = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "applesmc.sys");

        // Prefer Windows system drivers directory
        if (File.Exists(system32Driver))
        {
            return system32Driver;
        }

        // If local driver exists, copy it to System32\drivers if possible
        if (File.Exists(localDriver))
        {
            try
            {
                File.Copy(localDriver, system32Driver, true);
                if (File.Exists(system32Driver))
                    return system32Driver;
            }
            catch { }

            return localDriver;
        }

        string thirdPartyDriver = @"C:\Program Files (x86)\Macs Fan Control\applesmc.sys";
        if (File.Exists(thirdPartyDriver))
        {
            return thirdPartyDriver;
        }

        return null;
    }

    private static bool TryStartViaScCommand()
    {
        try
        {
            var psiConfig = new ProcessStartInfo("sc.exe", "config applesmc start= auto")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var procConfig = Process.Start(psiConfig);
            procConfig?.WaitForExit(2000);

            var psi = new ProcessStartInfo("sc.exe", "start applesmc")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);

            return proc != null && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
