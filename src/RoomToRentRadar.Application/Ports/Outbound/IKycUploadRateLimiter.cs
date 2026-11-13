namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Rate limiter abstraction for KYC upload URL generation.
/// Enables seamless substitution of in-memory sliding-window rate limiting with distributed providers
/// (e.g., Redis, Azure Cache, or Cosmos DB lease) in multi-instance scale-out environments.
/// </summary>
public interface IKycUploadRateLimiter
{
    /// <summary>
    /// Checks the current rate limit for the specified user and records the request.
    /// Throws <see cref="RoomToRentRadar.Application.Exceptions.RateLimitExceededException"/> if the limit is exceeded.
    /// </summary>
    ValueTask CheckAndAcquireAsync(Guid userId, CancellationToken cancellationToken = default);
}
