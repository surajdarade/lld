# RESOURCE(S)


[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/logging-service](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/logging-service)


# PROBLEM STATEMENT


Design a logging service that creates log records and writes them to one or more configured destinations. Each destination applies its own minimum severity threshold and format, while concurrent logging calls must not interleave record bytes on the same destination.


**Primary Capabilities:**

- Support five severity levels: `DEBUG`, `INFO`, `WARN`, `ERROR` and `FATAL`.
- Record a timestamp, severity level, message and emitting thread name for each logging call.
- Dispatch each record to one or more destinations configured at startup.
- Apply an independent minimum severity threshold and formatter at each destination.
- Keep destination type and formatting independent.
- Safely serialize concurrent writes to each destination.


**Error/Edge Case Handling:**

- Ignore records below a destination's minimum severity threshold.
- Reject invalid startup configuration, including an empty destination list or invalid severity levels.
- Ensure a failure at one destination does not prevent attempts to write to the remaining destinations.
- Prevent recursive failure reporting through the same logging service.
- Ensure a complete record write is serialized at each destination.


**Scope Boundaries:**

- Runtime configuration reload, asynchronous logging and buffered writes are out of scope.
- Remote/network destinations are out of scope in v1, but the sink abstraction should accommodate them.
- Hierarchical named loggers are out of scope in v1.
- The design covers concurrent calls within one process. Coordination across multiple processes writing to the same target is out of scope.


# STEP 1: REQUIREMENTS


1. Five severity levels: DEBUG < INFO < WARN < ERROR < FATAL

2. Each record carries timestamp, level, message, emitting thread name.

3. Logger writes each record to one or more destinations, set at startup.

4. Each destination has its own min-level threshold and its own format.
   Format and destination type vary independently.

5. Concurrent calls are safe. A record's bytes never interleave with another record's bytes on the same destination.

**Out of scope:**
- Hot-reloading config at runtime
- Async / buffered writes
- Remote / network destinations in v1 (design should accomodate)
- Hierarchical named loggers (com.app.service inheriting from com.app)


# STEP 2: ENTITIES


- `Logger`
  - Creates one record per logging call and dispatches it to the configured destinations.
- `LogRecord`
  - Stores the timestamp, severity level, message and emitting thread name. It is immutable after creation.
- `Formatter`
  - Converts a `LogRecord` into the output representation required by a destination.
- `Destination`
  - Owns a minimum severity threshold, formatter and sink. Filters records and serializes writes.
- `Sink`
  - Writes one complete formatted record to a concrete output target.


**Supporting Types:**

- `LogLevel`: `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL`, ordered by severity.
- `IFormatter`: interface for destination-specific formatting.
- `ISink`: interface for writing a complete formatted record.
- `PlainTextFormatter`: example formatter for human-readable output.
- `ConsoleSink` and `FileSink`: example output implementations.


**Relationships and State Ownership:**

- `Logger` owns an immutable collection of configured `Destination` objects.
- Each `LogRecord` is created once and shared with every destination.
- Each `Destination` owns its minimum level, formatter, sink and write lock.
- `IFormatter` determines the representation; `ISink` determines where the formatted record is written.
- The write lock is destination-local. Calls to the same destination are serialized, while different destinations can proceed independently.


**Unnecessary to Current Set of Requirements:**

- `LoggerManager` / `ConfigurationManager`: startup configuration can be passed directly to `Logger`.
- `LogQueue` / `LogWorker`: asynchronous and buffered logging are out of scope.
- `LoggerHierarchy`: hierarchical named loggers are out of scope.
- `RemoteSink`: remote destinations are out of scope for v1; `ISink` provides the extension point.
- Separate `LogEvent` and `LogRecord` types: one immutable `LogRecord` is sufficient.


# STEP 3: CLASS DESIGN


```text
enum LogLevel:
    DEBUG
    INFO
    WARN
    ERROR
    FATAL
```


```text
class Logger:
    - destinations: IReadOnlyList<Destination>
    - failureReporter: Action<Destination, Exception>

    + Logger(destinations, failureReporter)
    + Debug(message) -> void
    + Info(message) -> void
    + Warn(message) -> void
    + Error(message) -> void
    + Fatal(message) -> void
    + Log(level, message) -> void
```


```text
class LogRecord:
    + Timestamp: DateTimeOffset
    + Level: LogLevel
    + Message: string
    + ThreadName: string

    + LogRecord(timestamp, level, message, threadName)
```


```text
interface IFormatter:
    + Format(record) -> string
```


```text
class Destination:
    - Name: string
    - MinLevel: LogLevel
    - Formatter: IFormatter
    - Sink: ISink
    - writeLock: object

    + Destination(name, minLevel, formatter, sink)
    + Write(record) -> void
```


```text
interface ISink:
    + Write(data) -> void
```


```text
class PlainTextFormatter implements IFormatter:
    + Format(record) -> string
```


```text
class ConsoleSink implements ISink:
    - writeLock: object

    + Write(data) -> void
```


```text
class FileSink implements ISink:
    - writer: StreamWriter
    - writeLock: object

    + FileSink(filePath)
    + Write(data) -> void
    + Dispose() -> void
```


**Design Decisions:**

1. `Logger` creates one immutable record and dispatches it to all configured destinations.
2. `Destination` owns threshold filtering and a destination-local lock. The lock covers the complete sink write.
3. `Formatter` and `Sink` are separate abstractions so output representation and output target can vary independently.
4. Formatting happens before acquiring the destination write lock. Formatter implementations must therefore be thread-safe.
5. A destination failure is isolated so later destinations are still attempted. Failure reporting must not recursively use the same logger.
6. There is no global write lock. A slow destination does not hold a lock needed by other destinations.
7. Startup configuration is fixed after construction. No runtime configuration manager is required.
8. A process-local lock protects concurrent calls within one process. Multi-process writes require additional coordination.


# STEP 4: IMPLEMENTATION


```text
class Logger:

    public void Log(level, message):
        """
        Core Logic:
        1. Validate the severity level and message.
        2. Capture timestamp and emitting thread name once.
        3. Create one immutable LogRecord.
        4. Dispatch the same record to each configured destination.

        Error/Edge Case(s):
        1. Invalid level or null message -> reject the call.
        2. A destination failure must not stop attempts to other destinations.
        3. Failure reporting must not recursively call this logger.
        """

        record = LogRecord(
            currentUtcTimestamp,
            level,
            message,
            currentThreadNameOrFallback
        )

        for each destination in destinations:
            try:
                destination.Write(record)
            catch error:
                try:
                    failureReporter(destination, error)
                catch:
                    // Do not interrupt dispatch to remaining destinations.
```


```text
class Destination:

    public Write(record):
        """
        Core Logic:
        1. Ignore the record if its level is below MinLevel.
        2. Format the record using this destination's Formatter.
        3. Acquire the destination-local write lock.
        4. Write the complete formatted record to the Sink.
        5. Release the lock after the write finishes.

        Error/Edge Case(s):
        1. Below-threshold records are ignored.
        2. Formatter or sink failures propagate to Logger for isolation.
        3. The lock must cover the complete logical record write.
        4. Do not dispatch to other destinations while holding this lock.
        """

        if record.Level < MinLevel:
            return

        formattedRecord = Formatter.Format(record)

        lock(writeLock):
            Sink.Write(formattedRecord)
```


**Concurrency and Correctness Review:**

- Two threads writing to the same destination are serialized by that destination's lock.
- Different destinations use different locks and can proceed independently.
- A record can be ignored by one destination and written by another because thresholds are independent.
- The same immutable record is shared across destinations, while each destination formats it independently.
- The sink must treat one `Write` call as a complete logical record write. If it internally splits a record into writes, it must coordinate those writes itself.
- The destination lock protects threads within one process, not multiple processes writing to the same target.


# STEP 5: EXTENSIBILITY


**1. How would you make `log()` non-blocking?**

- I'd introduce a queue and a background worker.
- `Logger` would enqueue each `LogRecord` and return immediately. The worker would process records and write them to destinations.
- I'd define what happens when the queue is full and how pending records are handled during shutdown.


```text
class Logger:
    - logQueue: Queue<LogRecord>

    + Log(level, message):
        record = CreateRecord(level, message)
        logQueue.Enqueue(record)
        return

class LogWorker:
    + ProcessQueue()
```


**2. How would you support hierarchical named loggers?**

- I'd introduce logger names and parent-child relationships.
- A child logger would inherit its parent's configuration by default, while allowing its own settings to override them.
- For example, `com.app.service` could inherit from `com.app`.


```text
class Logger:
    - name: string
    - parent: Logger
    - destinations: List<Destination>

    + Log(level, message)
    + GetEffectiveConfiguration()
```


**3. How would you add a new log format?**

- I'd implement a new `Formatter`, such as `JsonFormatter`.
- I'd configure the required formatter for a destination.
- `Logger`, `Destination` and `Sink` wouldn't need changes because formatting is already independent of the output mechanism.


```text
class JsonFormatter implements Formatter:
    + Format(record) -> string
```


**4. How would you add a new destination type?**

- I'd implement the `Sink` interface for the new output, such as a network sink.
- I'd configure a `Destination` with that sink, its minimum level and its formatter.
- `Logger` wouldn't need to know how the new destination writes data.


```text
class NetworkSink implements Sink:
    + Write(data)
```


**5. How would you handle failures when writing to multiple destinations?**

- I'd keep failure handling inside the destination dispatch flow.
- If one destination fails, `Logger` should still attempt to write to the remaining destinations.
- I'd report the failure through an independent fallback mechanism to avoid recursively logging through the same failing logger.


```text
for destination in destinations:
    try:
        destination.Write(record)
    catch error:
        ReportFailureIndependently(destination, error)
```
