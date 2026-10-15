namespace LoggingService;

public sealed class Logger
{
    private readonly IReadOnlyList<Destination> _destinations;
    private readonly Action<Destination, Exception> _failureReporter;

    public Logger(
        IEnumerable<Destination> destinations,
        Action<Destination, Exception>? failureReporter = null)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        Destination[] configuredDestinations = destinations.ToArray();

        if (configuredDestinations.Length == 0)
            throw new ArgumentException(
                "At least one destination must be configured.",
                nameof(destinations));

        if (configuredDestinations.Any(destination => destination is null))
            throw new ArgumentException(
                "Destinations cannot contain null entries.",
                nameof(destinations));

        _destinations = Array.AsReadOnly(configuredDestinations);
        _failureReporter = failureReporter ?? ReportDestinationFailure;
    }

    public void Debug(string message) => Log(LogLevel.DEBUG, message);

    public void Info(string message) => Log(LogLevel.INFO, message);

    public void Warn(string message) => Log(LogLevel.WARN, message);

    public void Error(string message) => Log(LogLevel.ERROR, message);

    public void Fatal(string message) => Log(LogLevel.FATAL, message);

    public void Log(LogLevel level, string message)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level));

        ArgumentNullException.ThrowIfNull(message);

        string threadName = Thread.CurrentThread.Name
            ?? $"Thread-{Environment.CurrentManagedThreadId}";

        var record = new LogRecord(
            DateTimeOffset.UtcNow,
            level,
            message,
            threadName);

        foreach (Destination destination in _destinations)
        {
            try
            {
                destination.Write(record);
            }
            catch (Exception exception)
            {
                // Failure reporting is isolated and must not stop dispatch.
                try
                {
                    _failureReporter(destination, exception);
                }
                catch
                {
                    // Do not recursively log reporter failures or interrupt
                    // attempts to write to the remaining destinations.
                }
            }
        }
    }

    private static void ReportDestinationFailure(
        Destination destination,
        Exception exception)
    {
        try
        {
            Console.Error.WriteLine(
                $"Logging destination '{destination.Name}' failed: {exception.Message}");
        }
        catch
        {
            // The emergency reporter must not throw back into log dispatch.
        }
    }
}
