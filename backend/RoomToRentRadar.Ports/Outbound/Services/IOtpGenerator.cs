namespace RoomToRentRadar.Ports.Outbound.Services;

public interface IOtpGenerator
{
    /// <summary>Creates a cryptographically random numeric code of <see cref="Shared.Auth.AccountRules.OtpLength"/> digits.</summary>
    string Generate();
}
