using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

/// <summary>
/// In-memory sliding-window rate limiter with dictionary-level atomic synchronization
/// preventing race conditions between bucket eviction, retrieval, and timestamp recording.
/// </summary>
public sealed class InMemoryKycUploadRateLimiter : IKycUploadRateLimiter
{
    private sealed class UserRateLimitBucket
    {
        public readonly Queue<DateTimeOffset> Timestamps = new();
    }

    private readonly Dictionary<Guid, UserRateLimitBucket> _userUploadRateBuckets = new();
    private readonly object _syncLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxRequestsPerWindow;
    private readonly TimeSpan _window;

    public InMemoryKycUploadRateLimiter(
        TimeProvider? timeProvider = null,
        int maxRequestsPerWindow = 10,
        TimeSpan? window = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxRequestsPerWindow = maxRequestsPerWindow;
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    public int ActiveBucketCount
    {
        get
        {
            lock (_syncLock)
            {
                return _userUploadRateBuckets.Count;
            }
        }
    }

    public ValueTask CheckAndAcquireAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_syncLock)
        {
            // 1. Evict any expired buckets across the dictionary to keep RAM strictly bounded
            EvictExpiredBucketsUnsafe(now);

            // 2. Retrieve or create the user's bucket atomically
            if (!_userUploadRateBuckets.TryGetValue(userId, out var bucket))
            {
                bucket = new UserRateLimitBucket();
                _userUploadRateBuckets[userId] = bucket;
            }

            // 3. Purge timestamps older than the sliding window for this user
            while (bucket.Timestamps.TryPeek(out var oldest) && (now - oldest) > _window)
            {
                bucket.Timestamps.Dequeue();
            }

            // 4. Enforce rate limit
            if (bucket.Timestamps.Count >= _maxRequestsPerWindow)
            {
                throw new RateLimitExceededException(
                    $"Rate limit exceeded: You have generated {_maxRequestsPerWindow} upload URLs within {_window.TotalMinutes} minutes. Please try again later.");
            }

            bucket.Timestamps.Enqueue(now);
        }

        return ValueTask.CompletedTask;
    }

    private void EvictExpiredBucketsUnsafe(DateTimeOffset now)
    {
        var expiredUserIds = new List<Guid>();

        foreach (var (uid, bucket) in _userUploadRateBuckets)
        {
            while (bucket.Timestamps.TryPeek(out var oldest) && (now - oldest) > _window)
            {
                bucket.Timestamps.Dequeue();
            }

            if (bucket.Timestamps.Count == 0)
            {
                expiredUserIds.Add(uid);
            }
        }

        foreach (var uid in expiredUserIds)
        {
            _userUploadRateBuckets.Remove(uid);
        }
    }
}
