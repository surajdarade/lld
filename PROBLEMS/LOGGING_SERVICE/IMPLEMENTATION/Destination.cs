namespace LoggingService;

public sealed class Destination
{
    private readonly object _writeLock = new();

    public string Name { get; }
    public LogLevel MinLevel { get; }
    public IFormatter Formatter { get; }
    public ISink Sink { get; }

    public Destination(
        string name,
        LogLevel minLevel,
        IFormatter formatter,
        ISink sink)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Destination name is required.", nameof(name));

        if (!Enum.IsDefined(minLevel))
            throw new ArgumentOutOfRangeException(nameof(minLevel));

        Name = name;
        MinLevel = minLevel;
        Formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        Sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public void Write(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Level < MinLevel)
            return;

        // Formatter implementations should be thread-safe.
        string formattedRecord = Formatter.Format(record)
            ?? throw new InvalidOperationException("Formatter returned null.");

        lock (_writeLock)
        {
            Sink.Write(formattedRecord);
        }
    }
}
