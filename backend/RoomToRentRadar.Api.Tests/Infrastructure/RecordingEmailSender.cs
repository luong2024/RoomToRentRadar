using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class RecordingEmailSender : IEmailSender
{
    public List<RegistrationOtpEmail> Sent { get; } = [];

    public bool FailSends { get; set; }

    public Task<bool> SendRegistrationOtpAsync(
        RegistrationOtpEmail email,
        CancellationToken cancellationToken)
    {
        if (FailSends)
        {
            return Task.FromResult(false);
        }

        Sent.Add(email);
        return Task.FromResult(true);
    }

    public void Reset()
    {
        Sent.Clear();
        FailSends = false;
    }
}
