using System.Globalization;
using System.Security.Cryptography;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class OtpGenerator : IOtpGenerator
{
    private const int ExclusiveUpperBound = 1_000_000;

    public string Generate()
        => RandomNumberGenerator
            .GetInt32(ExclusiveUpperBound)
            .ToString($"D{AccountRules.OtpLength}", CultureInfo.InvariantCulture);
}
