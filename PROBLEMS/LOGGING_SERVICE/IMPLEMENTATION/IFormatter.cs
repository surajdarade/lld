namespace LoggingService;

public interface IFormatter
{
    string Format(LogRecord record);
}
