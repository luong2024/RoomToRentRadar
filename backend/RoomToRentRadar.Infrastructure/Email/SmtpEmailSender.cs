using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure.Email;

public sealed class SmtpEmailSender(
    SmtpEmailOptions options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const int TimeoutMilliseconds = 15_000;

    public async Task<bool> SendRegistrationOtpAsync(
        RegistrationOtpEmail email,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = RegistrationOtpMessage.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = RegistrationOtpMessage.BuildBody(email),
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email.ToEmail));

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = TimeoutMilliseconds
        };
        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(exception, "Failed to send the registration OTP email through SMTP.");
            return false;
        }
    }
}
