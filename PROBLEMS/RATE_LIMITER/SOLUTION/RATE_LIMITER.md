# RESOURCE(S)

[https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/rate-limiter](https://www.hellointerview.com/learn/low-level-design/problem-breakdowns/rate-limiter)


# PROBLEM STATEMENT


You're building an in-memory rate limiter for an API gateway. The system receives configuration from an external service that provides rate limiting rules per endpoint. Each endpoint can have its own limit with a specific algorithm. Here's an example configuration for one endpoint:

```text
{
  "endpoint": "/search",
  "algorithm": "TokenBucket",
  "algoConfig": {
    "capacity": 1000,
    "refillRatePerSecond": 10
  }
}
```

This config allows bursts up to 1000 requests, refilling at 10 requests per second.
Your job is to build the in-memory rate limiter that enforces these rules.


**Primary Capabilities:**

- Accept requests containing `clientId` and `endpoint`.
- Load endpoint-specific rate-limit configuration at startup.
- Select the configured algorithm and its parameters for each endpoint.
- Use a default configuration when an endpoint has no explicit configuration.
- Return `allowed`, `remaining` and `retryAfterMs`.
- Keep rate-limit state independent for each client and endpoint.
- Safely evaluate concurrent requests.

**Error/Edge Case Handling:**

- Unknown endpoint -> use the default configuration.
- Unsupported algorithms or invalid basic parameters -> reject configuration at startup.
- A denied request must not consume quota or append an accepted request timestamp.
- Concurrent requests for the same client and endpoint must not exceed the configured limit because of a race.
- `remaining` must never be negative.
- `retryAfterMs` is `null` when a request is allowed.

**Scope Boundaries:**

- Distributed rate limiting and coordination through Redis are out of scope.
- Dynamic configuration updates are out of scope.
- Metrics and monitoring are out of scope.
- Extensive configuration validation is out of scope.


# STEP 1: REQUIREMENTS


1. Configuration is provided at startup (loaded once)

2. System receives requests with (clientId: string, endpoint: string)

3. Each endpoint has a configuration specifying:
   - Algorithm to use (e.g., "TokenBucket", "SlidingWindowLog", etc.)
   - Algorithm-specific parameters (e.g., capacity, refillRatePerSecond for Token Bucket)

4. System enforces rate limits by checking clientId against the endpoint's configuration

5. Return structured result: (allowed: boolean, remaining: int, retryAfterMs: long | null)

6. If endpoint has no configuration, use a default limit

**Out of scope:**

- Distributed rate limiting (Redis, coordination)
- Dynamic configuration updates
- Metrics and monitoring
- Config validation beyond basic checks


# STEP 2: ENTITIES


- `RateLimiter`
  - Resolves the endpoint configuration, retrieves the client's limiter and returns the result.
- `LimiterFactory`
  - Creates a limiter implementation based on the configured algorithm.
- `Limiter`
  - Defines the common operation used to evaluate a request.
- `RateLimitConfig`
  - Stores the algorithm and the parameters needed by that algorithm.
- `RateLimitResult`
  - Stores the decision, remaining quota and optional retry delay.
- `TokenBucketLimiter`
  - Tracks available tokens and refill time for one client-endpoint pair.
- `SlidingWindowLogLimiter`
  - Tracks accepted request timestamps for one client-endpoint pair.


**Supporting Types:**

- `LimiterAlgorithm`: supported algorithm names.
- `LimiterKey`: the combination of client ID and endpoint used to isolate state.

**Relationships and State Ownership:**

- `RateLimiter` owns the startup configuration, default configuration, factory and client-endpoint limiter map.
- `LimiterFactory` creates the concrete implementation selected by `RateLimitConfig`.
- Each concrete limiter owns the changing state required by its algorithm.
- `RateLimitResult` is a result value and does not own rate-limit state.
- Each limiter instance represents exactly one client-endpoint pair.
- A limiter's lock protects its complete state transition. The map lock protects limiter lookup and creation.

**Unnecessary to Current Set of Requirements:**

- `ConfigurationManager`: configuration is supplied once at startup.
- `DistributedLimiter` / `RedisLimiter`: distributed coordination is out of scope.
- `MetricsCollector`: metrics and monitoring are out of scope.
- A separate state class for each algorithm: each limiter can own its small algorithm-specific state directly.


# STEP 3: CLASS DESIGN


```text
enum LimiterAlgorithm:
    TokenBucket
    SlidingWindowLog
```


```text
class RateLimiter:
    - endpointConfigs: IReadOnlyDictionary<string, RateLimitConfig>
    - defaultConfig: RateLimitConfig
    - limiterFactory: LimiterFactory
    - limiters: Dictionary<LimiterKey, Limiter>
    - limiterMapLock: object

    + RateLimiter(endpointConfigs, defaultConfig, limiterFactory)
    + AllowRequest(clientId: string, endpoint: string) -> RateLimitResult
```


```text
class LimiterKey:
    + clientId: string
    + endpoint: string

    + LimiterKey(clientId: string, endpoint: string)
```


```text
class RateLimitConfig:
    + algorithm: LimiterAlgorithm
    + capacity: int
    + refillPerSecond: double
    + maxRequests: int
    + windowSizeMs: long

    + RateLimitConfig(algorithm: LimiterAlgorithm, capacity: int, refillRatePerSecond: double, maxRequests: int, windowSizeMs: long)
    + Validate() -> void
```


```text
class LimiterFactory:
    + Limiter CreateLimiter(config: RateLimitConfig)
```


```text
interface Limiter:
    + RateLimitResult AllowRequest()
```


```text
class TokenBucketLimiter implements Limiter:
    - capacity: int
    - refillRatePerSecond: double
    - availableTokens: double
    - lastRefillTimestamp: long
    - syncLock: object

    + TokenBucketLimiter(config: RateLimitConfig)
    + RateLimitResult AllowRequest()
```


```text
class SlidingWindowLogLimiter implements Limiter:
    - maxRequests: int
    - windowSizeTicks: long
    - requestTimestamps: Queue<long>
    - syncLock: object

    + SlidingWindowLogLimiter(config: RateLimitConfig)
    + RateLimitResult AllowRequest()
```


```text
class RateLimitResult:
    + Allowed: bool
    + Remaining: int
    + RetryAfterMs: long?

    + RateLimitResult(allowed: bool, remaining: int, retryAfterMs: long?)
```

**Design Decisions:**

1. `RateLimiter` uses `(clientId, endpoint)` as the limiter key so different clients and endpoints have independent state.
2. The limiter map is protected while looking up or creating a limiter. The algorithm runs after releasing that map lock.
3. Each limiter synchronizes the complete check-and-update operation using its own lock.
4. `LimiterFactory` keeps algorithm selection in one place.
5. A monotonic clock is used for elapsed time so wall-clock adjustments do not corrupt refill or window calculations.
6. Configuration is validated once during construction and then treated as fixed.


**Pattern Decision Log:**

| Pattern | Where it could apply | Verdict | Justification | Trigger that would make me adopt it |
|---|---|---|---|---|
| Strategy | Rate-limiting algorithms | USED | `Limiter` gives the algorithms one common contract while their state and behavior differ. | Already required by multiple configured algorithms. |
| Factory | Algorithm creation | USED | `LimiterFactory` centralizes the mapping from configuration to concrete limiter. | Already used to select the configured algorithm. |
| Singleton | `RateLimiter` | NOT USED | Independent instances should be possible for separate configurations or tests. | Only if the application explicitly requires one process-wide instance. |
| Observer | Limit decisions | NOT USED | No subscribers or change notifications are required. | Add only if consumers need reliable decision events. |


# STEP 4: IMPLEMENTATION


```text
class RateLimiter:
    public RateLimitResult AllowRequest(clientId: string, endpoint: string):
        """
        Core Logic:
        1. Resolve the endpoint configuration or the default configuration.
        2. Create a LimiterKey from clientId and endpoint.
        3. Acquire the map lock and retrieve or create the limiter.
        4. Release the map lock before evaluating the request.
        5. Return the limiter's result.

        Error/Edge Case(s):
        1. Null or empty clientId/endpoint -> reject.
        2. Unknown endpoint -> use defaultConfig.
        3. Concurrent creation for the same key -> create and register only one limiter.
        """

        config = endpointConfigs.getOrDefault(endpoint, defaultConfig)
        key = LimiterKey(clientId, endpoint)

        lock(limiterMapLock):
            if key not in limiters:
                limiters[key] = limiterFactory.CreateLimiter(config)
            limiter = limiters[key]

        return limiter.AllowRequest()
```


```text
class LimiterFactory:
    public Limiter CreateLimiter(config: RateLimitConfig):
        """
        Core Logic:
        1. Read the algorithm from the configuration.
        2. Create the matching limiter implementation.
        3. Return the limiter through the common Limiter interface.

        Error/Edge Case(s):
        1. Unsupported algorithm -> reject configuration at startup.
        2. Invalid algorithm-specific parameters -> reject configuration at startup.
        """

        if config.Algorithm == TokenBucket:
            return TokenBucketLimiter(config)

        if config.Algorithm == SlidingWindowLog:
            return SlidingWindowLogLimiter(config)

        throw UnsupportedAlgorithmException
```


```text
class TokenBucketLimiter implements Limiter:
    public RateLimitResult AllowRequest():
        """
        Core Logic:
        1. Acquire the limiter lock.
        2. Calculate elapsed time using a monotonic clock.
        3. Refill tokens and cap the value at capacity.
        4. If at least one token is available, consume one and allow the request.
        5. Otherwise calculate the delay until one token is available.

        Error/Edge Case(s):
        1. Full bucket -> do not exceed capacity.
        2. Insufficient tokens -> deny without consuming a token.
        3. Invalid capacity or refill rate -> reject configuration at startup.
        4. The lock covers refill, decision and token consumption.
        """

        lock(syncLock):
            now = MonotonicTimestamp()
            elapsedSeconds = ElapsedSeconds(lastRefillTimestamp, now)

            availableTokens = min(
                capacity,
                availableTokens + elapsedSeconds * refillRatePerSecond
            )
            lastRefillTimestamp = now

            if availableTokens >= 1:
                availableTokens -= 1
                return RateLimitResult(true, floor(availableTokens), null)

            retryAfterMs = ceil(
                (1 - availableTokens) / refillRatePerSecond * 1000
            )
            return RateLimitResult(false, 0, retryAfterMs)
```

```text
class SlidingWindowLogLimiter implements Limiter:
    public RateLimitResult AllowRequest():
        """
        Core Logic:
        1. Acquire the limiter lock.
        2. Remove timestamps at or before the window cutoff.
        3. If fewer than maxRequests remain, record this request and allow it.
        4. Otherwise calculate when the oldest request expires.

        Error/Edge Case(s):
        1. Expired timestamps -> remove before checking the limit.
        2. Limit reached -> deny without recording a timestamp.
        3. Invalid maxRequests or window size -> reject configuration at startup.
        4. Cleanup, count check and enqueue are one atomic operation.
        """

        lock(syncLock):
            now = MonotonicTimestamp()
            cutoff = now - windowSizeTicks

            while requestTimestamps is not empty
                  and requestTimestamps.front() <= cutoff:
                requestTimestamps.dequeue()

            if requestTimestamps.count < maxRequests:
                requestTimestamps.enqueue(now)
                remaining = maxRequests - requestTimestamps.count
                return RateLimitResult(true, remaining, null)

            retryAfterMs = MillisecondsUntil(
                requestTimestamps.front() + windowSizeTicks,
                now
            )
            return RateLimitResult(false, 0, max(0, retryAfterMs))
```

**Concurrency and Correctness Review:**

- Two threads requesting the same client-endpoint key cannot create separate active limiter instances because lookup and creation happen under the map lock.
- The map lock is released before rate-limit evaluation, so requests for different keys can evaluate concurrently.
- Two requests using the same limiter are serialized while state is checked and updated.
- Token refill and consumption are one critical section.
- Sliding-window cleanup, count check and timestamp insertion are one critical section.
- The design is process-local. Separate service instances do not share counters or coordinate their decisions.
- Inactive client state is retained for the process lifetime in this version; eviction is an extensibility option.


# STEP 5: EXTENSIBILITY


**1. How would you add a new rate limiting algorithm?**

- I'd implement the `Limiter` interface for the new algorithm.
- I'd add the algorithm to `LimiterAlgorithm` and add its creation case in `LimiterFactory`.
- `RateLimiter` wouldn't need to change because it already works through the common `Limiter` interface.

```text
class FixedWindowCounterLimiter implements Limiter:
    + RateLimitResult AllowRequest()
```

**2. How would you handle dynamic configuration updates?**

- I'd add a method to load and prepare a new immutable configuration snapshot.
- I'd publish the new snapshot atomically so each request uses a consistent configuration.
- I'd explicitly decide whether existing limiter state is preserved or reset. Recreating limiters can give clients a fresh quota, so state migration may be needed if that is not acceptable.


**3. How would you handle thread safety for concurrent requests?**

- I'd keep the map lock limited to limiter lookup and creation.
- Each limiter would continue to protect its complete state transition with its own lock.
- For Token Bucket, refill, check and consume must be atomic. For Sliding Window Log, cleanup, check and enqueue must be atomic.
- A concurrent collection alone would not make the algorithm's multi-step state transition atomic.


**4. How would you handle memory growth from tracking many clients?**

- I'd track each limiter's last-access time and periodically remove inactive entries.
- Eviction must coordinate with active requests so a limiter is not removed while being used.
- I'd choose the inactivity threshold carefully because a client returning after eviction starts with fresh in-memory state.


**5. How would you test the rate limiter under concurrent load?**

- I'd test each algorithm's boundary conditions using a controllable clock where possible.
- I'd send simultaneous requests for the same client and endpoint and verify that accepted requests never exceed the configured limit.
- I'd verify that different clients and endpoints do not share state.
- I'd run a many-client stress test and check correctness and memory growth.