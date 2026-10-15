namespace LoggingService;

public sealed class FileSink : ISink, IDisposable
{
    private readonly object _writeLock = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public FileSink(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        _writer = new StreamWriter(
            new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void Write(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        lock (_writeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(data);
        }
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
                return;

            _writer.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
