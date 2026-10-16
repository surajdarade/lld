using System.Diagnostics;

namespace RateLimiterImplementation;

public sealed class TokenBucketLimiter : Limiter
{
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private readonly object _syncLock = new();
    private double _availableTokens;
    private long _lastRefillTimestamp;

    public TokenBucketLimiter(RateLimitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Algorithm != LimiterAlgorithm.TOCKEN_BUCKET)
            throw new ArgumentException("Token Bucket configuration is required.", nameof(config));

        config.Validate();
        _capacity = config.Capacity;
        _refillRatePerSecond = config.RefillRatePerSecond;
        _availableTokens = _capacity;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public RateLimitResult AllowRequest()
    {
        lock (_syncLock)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedSeconds = Math.Max(
                0, Stopwatch.GetElapsedTime(_lastRefillTimestamp, now).TotalSeconds);

            _availableTokens = Math.Min(
                _capacity,
                _availableTokens + elapsedSeconds * _refillRatePerSecond);
            _lastRefillTimestamp = now;

            if (_availableTokens >= 1)
            {
                _availableTokens -= 1;
                return new RateLimitResult(true, (int)Math.Floor(_availableTokens), null);
            }

            var retryAfterMs = (long)Math.Ceiling(
                (1 - _availableTokens) / _refillRatePerSecond * 1000);

            return new RateLimitResult(false, 0, Math.Max(1, retryAfterMs));
        }
    }
}
