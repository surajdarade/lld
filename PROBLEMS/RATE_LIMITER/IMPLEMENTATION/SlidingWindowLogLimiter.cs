using System.Diagnostics;

namespace RateLimiterImplementation;

public sealed class SlidingWindowLogLimiter : Limiter
{
    private readonly int _maxRequests;
    private readonly long _windowSizeTicks;
    private readonly Queue<long> _requestTimestamps = new();
    private readonly object _syncLock = new();

    public SlidingWindowLogLimiter(RateLimitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Algorithm != LimiterAlgorithm.SlidingWindowLog)
            throw new ArgumentException("Sliding Window Log configuration is required.", nameof(config));

        config.Validate();
        _maxRequests = config.MaxRequests;
        _windowSizeTicks = checked((long)Math.Ceiling(
            config.WindowSizeMs * (double)Stopwatch.Frequency / 1000.0));
    }

    public RateLimitResult AllowRequest()
    {
        lock (_syncLock)
        {
            var now = Stopwatch.GetTimestamp();
            var cutoff = now - _windowSizeTicks;

            while (_requestTimestamps.Count > 0 &&
                   _requestTimestamps.Peek() <= cutoff)
                _requestTimestamps.Dequeue();

            if (_requestTimestamps.Count < _maxRequests)
            {
                _requestTimestamps.Enqueue(now);
                return new RateLimitResult(
                    true, _maxRequests - _requestTimestamps.Count, null);
            }

            var oldestExpiry = _requestTimestamps.Peek() + _windowSizeTicks;
            var remainingTicks = Math.Max(0, oldestExpiry - now);
            var retryAfterMs = (long)Math.Ceiling(
                remainingTicks * 1000.0 / Stopwatch.Frequency);

            return new RateLimitResult(false, 0, retryAfterMs);
        }
    }
}
