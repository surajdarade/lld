namespace RateLimiterImplementation;

public sealed class RateLimiter
{
    private readonly IReadOnlyDictionary<string, RateLimitConfig> _endpointConfigs;
    private readonly RateLimitConfig _defaultConfig;
    private readonly LimiterFactory _limiterFactory;
    private readonly Dictionary<LimiterKey, Limiter> _limiters = new();
    private readonly object _limiterMapLock = new();

    public RateLimiter(
        IReadOnlyDictionary<string, RateLimitConfig> endpointConfigs,
        RateLimitConfig defaultConfig,
        LimiterFactory limiterFactory)
    {
        ArgumentNullException.ThrowIfNull(endpointConfigs);
        ArgumentNullException.ThrowIfNull(defaultConfig);
        ArgumentNullException.ThrowIfNull(limiterFactory);

        _endpointConfigs = endpointConfigs.ToDictionary(
            pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _defaultConfig = defaultConfig;
        _limiterFactory = limiterFactory;

        foreach (var config in _endpointConfigs.Values)
            config.Validate();
        _defaultConfig.Validate();
    }

    public RateLimitResult AllowRequest(string clientId, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));

        var config = _endpointConfigs.TryGetValue(endpoint, out var endpointConfig)
            ? endpointConfig
            : _defaultConfig;

        var key = new LimiterKey(clientId, endpoint);
        Limiter limiter;

        lock (_limiterMapLock)
        {
            if (_limiters.TryGetValue(key, out var existing))
            {
                limiter = existing;
            }
            else
            {
                limiter = _limiterFactory.CreateLimiter(config);
                _limiters.Add(key, limiter);
            }
        }

        return limiter.AllowRequest();
    }
}
