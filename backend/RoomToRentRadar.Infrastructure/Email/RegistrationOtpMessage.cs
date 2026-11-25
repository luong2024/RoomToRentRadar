using System.Globalization;
using System.Net.Mail;
using System.Text;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Infrastructure.Email;

internal static class RegistrationOtpMessage
{
    public const string Subject = "Mã xác minh đăng ký RoomToRentRadar";

    public static string BuildBody(RegistrationOtpEmail email)
        => $"""
           Xin chào {email.DisplayName},

           Mã xác minh đăng ký RoomToRentRadar của bạn là: {email.Code}

           Mã có hiệu lực trong {AccountRules.OtpLifetimeMinutes} phút.
           Nếu bạn không yêu cầu tạo tài khoản, hãy bỏ qua email này.

           Trân trọng,
           RoomToRentRadar
           """;

    public static string BuildEml(RegistrationOtpEmail email, SmtpEmailOptions options)
    {
        var from = new MailAddress(options.FromAddress, options.FromName).ToString();
        var builder = new StringBuilder();
        builder.Append("From: ").Append(from).Append("\r\n");
        builder.Append("To: ").Append(email.ToEmail).Append("\r\n");
        builder.Append("Subject: ").Append(Subject).Append("\r\n");
        builder.Append("Date: ")
            .Append(DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture))
            .Append("\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n");
        builder.Append("\r\n");
        builder.Append(BuildBody(email));
        return builder.ToString();
    }
}
