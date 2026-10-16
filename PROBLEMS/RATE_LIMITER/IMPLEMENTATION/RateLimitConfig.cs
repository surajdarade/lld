namespace RateLimiterImplementation;

public sealed class RateLimitConfig
{
    public LimiterAlgorithm Algorithm { get; }
    public int Capacity { get; }
    public double RefillRatePerSecond { get; }
    public int MaxRequests { get; }
    public long WindowSizeMs { get; }

    public RateLimitConfig(
        LimiterAlgorithm algorithm,
        int capacity = 0,
        double refillRatePerSecond = 0,
        int maxRequests = 0,
        long windowSizeMs = 0)
    {
        Algorithm = algorithm;
        Capacity = capacity;
        RefillRatePerSecond = refillRatePerSecond;
        MaxRequests = maxRequests;
        WindowSizeMs = windowSizeMs;
        Validate();
    }

    public void Validate()
    {
        switch (Algorithm)
        {
            case LimiterAlgorithm.TokenBucket:
                if (Capacity <= 0)
                    throw new ArgumentOutOfRangeException(nameof(Capacity));
                if (RefillRatePerSecond <= 0 ||
                    double.IsNaN(RefillRatePerSecond) ||
                    double.IsInfinity(RefillRatePerSecond))
                    throw new ArgumentOutOfRangeException(nameof(RefillRatePerSecond));
                break;

            case LimiterAlgorithm.SlidingWindowLog:
                if (MaxRequests <= 0)
                    throw new ArgumentOutOfRangeException(nameof(MaxRequests));
                if (WindowSizeMs <= 0)
                    throw new ArgumentOutOfRangeException(nameof(WindowSizeMs));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Algorithm));
        }
    }
}
