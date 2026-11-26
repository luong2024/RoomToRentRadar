using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Ports.Models.Auth;

public sealed record VerifyRegistrationCommand(Guid ChallengeId, string Code);

public enum VerifyRegistrationOutcome
{
    Verified,
    ChallengeNotFound,
    ChallengeExpired,
    InvalidCode,
    AttemptsExceeded,
    EmailAlreadyRegistered
}

public sealed record VerifyRegistrationResult(
    VerifyRegistrationOutcome Outcome,
    LoginResponse? Response,
    int AttemptsRemaining)
{
    public static VerifyRegistrationResult Verified(LoginResponse response)
        => new(VerifyRegistrationOutcome.Verified, response, 0);

    public static VerifyRegistrationResult ChallengeNotFound()
        => new(VerifyRegistrationOutcome.ChallengeNotFound, null, 0);

    public static VerifyRegistrationResult ChallengeExpired()
        => new(VerifyRegistrationOutcome.ChallengeExpired, null, 0);

    public static VerifyRegistrationResult InvalidCode(int attemptsRemaining)
        => new(VerifyRegistrationOutcome.InvalidCode, null, attemptsRemaining);

    public static VerifyRegistrationResult AttemptsExceeded()
        => new(VerifyRegistrationOutcome.AttemptsExceeded, null, 0);

    public static VerifyRegistrationResult DuplicateEmail()
        => new(VerifyRegistrationOutcome.EmailAlreadyRegistered, null, 0);
}
