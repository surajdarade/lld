namespace RateLimiterImplementation;

public interface Limiter
{
    RateLimitResult AllowRequest();
}
