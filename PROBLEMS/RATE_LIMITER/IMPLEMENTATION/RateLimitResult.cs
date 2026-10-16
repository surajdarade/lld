namespace RateLimiterImplementation;

public sealed record RateLimitResult(bool Allowed, int Remaining, long? RetryAfterMs);
