namespace LoggingService;

public sealed class ConsoleSink : ISink
{
    private readonly object _writeLock = new();

    public void Write(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_writeLock)
        {
            Console.WriteLine(data);
        }
    }
}
