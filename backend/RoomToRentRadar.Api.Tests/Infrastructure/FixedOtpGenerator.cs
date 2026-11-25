using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class FixedOtpGenerator : IOtpGenerator
{
    public const string DefaultCode = "246810";

    public string NextCode { get; set; } = DefaultCode;

    public string Generate() => NextCode;

    public void Reset() => NextCode = DefaultCode;
}
