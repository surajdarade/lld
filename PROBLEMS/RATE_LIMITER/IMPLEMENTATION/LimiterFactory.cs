namespace RateLimiterImplementation;

public sealed class LimiterFactory
{
    public Limiter CreateLimiter(RateLimitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Algorithm switch
        {
            LimiterAlgorithm.TokenBucket => new TokenBucketLimiter(config),
            LimiterAlgorithm.SlidingWindowLog => new SlidingWindowLogLimiter(config),
            _ => throw new ArgumentOutOfRangeException(nameof(config.Algorithm))
        };
    }
}
