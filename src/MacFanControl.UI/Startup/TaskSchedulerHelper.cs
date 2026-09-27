using System.Diagnostics;
using System.IO;

namespace MacFanControl.UI.Startup;

public static class TaskSchedulerHelper
{
    private const string TaskName = "MacFanControlBootCamp";

    public static bool IsStartupEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartup(bool enable)
    {
        try
        {
            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                // /RL HIGHEST runs as administrator without triggering UAC prompt at Windows logon
                string args = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" --minimized\" /SC ONLOGON /RL HIGHEST /F";

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            else
            {
                string args = $"/Delete /TN \"{TaskName}\" /F";

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }
}
