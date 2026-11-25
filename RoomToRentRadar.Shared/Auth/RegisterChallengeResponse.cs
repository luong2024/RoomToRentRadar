namespace RoomToRentRadar.Shared.Auth;

/// <summary>Returned when registration is started and a one-time code has been emailed.</summary>
public sealed record RegisterChallengeResponse(Guid ChallengeId, string Email, DateTime ExpiresAt);
