namespace RoomToRentRadar.Shared.Auth;

public sealed record VerifyRegistrationRequest(Guid ChallengeId, string Code);
