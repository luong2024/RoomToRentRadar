using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Services;

namespace RoomToRentRadar.Infrastructure.Email;

/// <summary>
/// Development stand-in for SMTP: writes each message as an .eml file so the OTP
/// never has to appear in logs or source-controlled settings.
/// </summary>
public sealed class FileEmailSender(
    SmtpEmailOptions options,
    IHostEnvironment environment,
    ILogger<FileEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendRegistrationOtpAsync(
        RegistrationOtpEmail email,
        CancellationToken cancellationToken)
    {
        var outboxDirectory = ResolveOutboxDirectory(options, environment.ContentRootPath);
        try
        {
            Directory.CreateDirectory(outboxDirectory);
            var path = Path.Combine(
                outboxDirectory,
                $"registration-otp-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml");
            var content = RegistrationOtpMessage.BuildEml(email, options);
            await File.WriteAllTextAsync(
                path,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            logger.LogInformation("Wrote the registration OTP email to {Path}.", path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to write the registration OTP email to the development outbox.");
            return false;
        }
    }

    public static string ResolveOutboxDirectory(SmtpEmailOptions options, string contentRootPath)
        => string.IsNullOrWhiteSpace(options.OutboxDirectory)
            ? Path.Combine(contentRootPath, "outbox")
            : Path.GetFullPath(options.OutboxDirectory, contentRootPath);
}
