namespace RateLimiterImplementation;

public sealed class LimiterFactory
{
    public Limiter CreateLimiter(RateLimitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Algorithm switch
        {
            LimiterAlgorithm.TOCKEN_BUCKET => new TokenBucketLimiter(config),
            LimiterAlgorithm.SLIDING_WINDOW_LOG => new SlidingWindowLogLimiter(config),
            _ => throw new ArgumentOutOfRangeException(nameof(config.Algorithm))
        };
    }
}
