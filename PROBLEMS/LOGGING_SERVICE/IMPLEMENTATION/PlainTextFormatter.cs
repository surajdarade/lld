namespace LoggingService;

public sealed class PlainTextFormatter : IFormatter
{
    public string Format(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return $"{record.Timestamp:O} [{record.Level}] " +
               $"[{record.ThreadName}] {record.Message}";
    }
}
