namespace LoggingService;

public sealed class LogRecord
{
    public DateTimeOffset Timestamp { get; }
    public LogLevel Level { get; }
    public string Message { get; }
    public string ThreadName { get; }

    public LogRecord(
        DateTimeOffset timestamp,
        LogLevel level,
        string message,
        string threadName)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level));

        Timestamp = timestamp;
        Level = level;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ThreadName = threadName ?? throw new ArgumentNullException(nameof(threadName));
    }
}
