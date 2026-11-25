using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace RoomToRentRadar.Infrastructure.Email;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Auth:Email:Smtp";
    public const int DefaultPort = 587;

    private const string DefaultFromAddress = "no-reply@roomtorentradar.local";
    private const string DefaultFromName = "RoomToRentRadar";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = DefaultPort;
    public bool UseStartTls { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = DefaultFromAddress;
    public string FromName { get; init; } = DefaultFromName;
    public string OutboxDirectory { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    /// <summary>
    /// Reads <c>Auth:Email:Smtp</c>. Outside development a missing host fails fast;
    /// development falls back to writing messages to a local outbox.
    /// </summary>
    public static SmtpEmailOptions Resolve(IConfiguration configuration, bool allowOutboxFallback)
    {
        var options = new SmtpEmailOptions
        {
            Host = configuration[$"{SectionName}:Host"]?.Trim() ?? string.Empty,
            Port = configuration.GetValue<int?>($"{SectionName}:Port") ?? DefaultPort,
            UseStartTls = configuration.GetValue<bool?>($"{SectionName}:UseStartTls") ?? true,
            UserName = configuration[$"{SectionName}:UserName"]?.Trim() ?? string.Empty,
            Password = configuration[$"{SectionName}:Password"] ?? string.Empty,
            FromAddress = ReadOrDefault(configuration, $"{SectionName}:FromAddress", DefaultFromAddress),
            FromName = ReadOrDefault(configuration, $"{SectionName}:FromName", DefaultFromName),
            OutboxDirectory = configuration[$"{SectionName}:OutboxDirectory"]?.Trim() ?? string.Empty
        };

        if (!MailAddress.TryCreate(options.FromAddress, out _))
        {
            throw new InvalidOperationException(
                $"{SectionName}:FromAddress must be a valid email address.");
        }

        if (options.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.UserName) != string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                $"{SectionName}:UserName and Password must be configured together.");
        }

        if (!options.IsConfigured && !allowOutboxFallback)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Host is required. Configure it with "
                + $"'dotnet user-secrets set \"{SectionName}:Host\" \"<smtp host>\" --project RoomToRentRadar.Api' "
                + $"or the Auth__Email__Smtp__Host environment variable.");
        }

        return options;
    }

    private static string ReadOrDefault(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
