namespace MacFanControl.Core.Models;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Smc
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string DisplayText => string.IsNullOrEmpty(Details)
        ? $"[{FormattedTimestamp}] [{Level.ToString().ToUpper()}] {Message}"
        : $"[{FormattedTimestamp}] [{Level.ToString().ToUpper()}] {Message} -> {Details}";
}
