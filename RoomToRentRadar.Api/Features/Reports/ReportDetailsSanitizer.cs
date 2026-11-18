using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RoomToRentRadar.Api.Features.Reports;

/// <summary>
/// Stores report details as plain text: scripts and tags are removed, encoded
/// markup is decoded and stripped again, and control characters are dropped.
/// The result must only ever be rendered as text, never as HTML.
/// </summary>
public static partial class ReportDetailsSanitizer
{
    /// <summary>Raw input bound checked before the regexes run.</summary>
    public const int MaximumInputLength = 8000;

    public const int MaximumLength = 1000;

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = BlockRegex().Replace(value, string.Empty);
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = TagRegex().Replace(text, string.Empty);

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsControl(character) && character is not '\n' and not '\t')
            {
                continue;
            }

            builder.Append(character);
        }

        var sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? null : sanitized;
    }

    [GeneratedRegex(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockRegex();

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
