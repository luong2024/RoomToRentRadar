using System;
using System.Text.Json;

namespace RoomToRentRadar.DeviceAdapters;

/// <summary>
/// Lightweight helper to extract claims from unverified OIDC ID tokens on the client side
/// for UI dispatching purposes only. Full cryptographic verification is done by the backend.
/// </summary>
public static class JwtPayloadHelper
{
    public const string IctuDomain = "ictu.edu.vn";

    public static string? ExtractEmail(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var base64 = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(base64);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                return emailProp.GetString();
            }
        }
        catch
        {
            // Ignore parsing errors, return null for graceful fallback
        }

        return null;
    }

    public static string? ExtractFullName(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var base64 = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(base64);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                return nameProp.GetString();
            }
        }
        catch
        {
            // Ignore parsing errors, return null for graceful fallback
        }

        return null;
    }

    public static bool IsIctuStudentEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        return trimmed.EndsWith($"@{IctuDomain}", StringComparison.OrdinalIgnoreCase);
    }
}

