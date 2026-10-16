namespace RateLimiterImplementation;

public readonly record struct LimiterKey(string ClientId, string Endpoint);
