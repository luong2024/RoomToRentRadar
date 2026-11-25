using RoomToRentRadar.Ports.Models.Auth;

namespace RoomToRentRadar.Ports.Outbound.Services;

public interface IEmailSender
{
    /// <summary>
    /// Delivers the registration OTP. Returns false when the message could not be handed off
    /// to the mail server so the use case can drop the challenge.
    /// </summary>
    Task<bool> SendRegistrationOtpAsync(
        RegistrationOtpEmail email,
        CancellationToken cancellationToken);
}
