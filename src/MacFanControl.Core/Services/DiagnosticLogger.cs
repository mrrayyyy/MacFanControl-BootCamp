using System.Collections.Concurrent;
using System.IO;
using System.Security.Principal;
using System.Text;
using MacFanControl.Core.Models;

namespace MacFanControl.Core.Services;

public class DiagnosticLogger
{
    private static readonly Lazy<DiagnosticLogger> _lazyInstance = new(() => new DiagnosticLogger());
    public static DiagnosticLogger Instance => _lazyInstance.Value;

    private readonly object _lock = new();
    private readonly List<LogEntry> _inMemoryLogs = new();
    private const int MaxInMemoryLogs = 2000;
    private readonly string _logFilePath;

    public event Action<LogEntry>? OnLogAdded;

    public DiagnosticLogger()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "MacFanControl");
        try
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
        catch { }

        _logFilePath = Path.Combine(folder, "diagnostics.log");

        // Write session header
        WriteInitialSystemInfo();
    }

    private void WriteInitialSystemInfo()
    {
        bool isAdmin = false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { }

        Info("=== MacFanControl Diagnostics Session Started ===");
        Info($"OS Version: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
        Info($".NET Runtime: {Environment.Version}");
        Info($"Machine Name: {Environment.MachineName}");
        Info($"Process Elevation (Admin): {(isAdmin ? "YES (Administrator)" : "NO (Non-Elevated - Run as Admin required!)")}");
        Info($"Log Storage Path: {_logFilePath}");
        Info("----------------------------------------------------------------");
    }

    public void Log(LogLevel level, string message, string details = "")
    {
        var entry = new LogEntry
        {
            Level = level,
            Message = message,
            Details = details,
            Timestamp = DateTime.Now
        };

        lock (_lock)
        {
            _inMemoryLogs.Add(entry);
            if (_inMemoryLogs.Count > MaxInMemoryLogs)
            {
                _inMemoryLogs.RemoveAt(0);
            }

            try
            {
                File.AppendAllText(_logFilePath, entry.DisplayText + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore file lock issues during high-frequency writes
            }
        }

        OnLogAdded?.Invoke(entry);
    }

    public void Info(string message, string details = "") => Log(LogLevel.Info, message, details);
    public void Debug(string message, string details = "") => Log(LogLevel.Debug, message, details);
    public void Warn(string message, string details = "") => Log(LogLevel.Warning, message, details);
    public void Error(string message, string details = "") => Log(LogLevel.Error, message, details);
    public void Smc(string message, string details = "") => Log(LogLevel.Smc, message, details);

    public IReadOnlyList<LogEntry> GetRecentLogs()
    {
        lock (_lock)
        {
            return _inMemoryLogs.ToList();
        }
    }

    public string GetAllLogsAsText()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================================");
            sb.AppendLine("  MacFanControl BootCamp Diagnostic Report (MBP 16\" 2019 i9)     ");
            sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                 ");
            sb.AppendLine("==================================================================");
            sb.AppendLine();

            foreach (var log in _inMemoryLogs)
            {
                sb.AppendLine(log.DisplayText);
            }

            return sb.ToString();
        }
    }

    public string ExportToFile(string? targetPath = null)
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filename = $"MacFanControl_Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            targetPath = Path.Combine(desktop, filename);
        }

        string content = GetAllLogsAsText();
        File.WriteAllText(targetPath, content, Encoding.UTF8);
        Info($"Diagnostic log exported successfully to: {targetPath}");
        return targetPath;
    }
}
