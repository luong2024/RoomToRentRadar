using Microsoft.Extensions.Configuration;
using RoomToRentRadar.Infrastructure.Email;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class SmtpEmailOptionsTests
{
    [Test]
    public void Resolve_InDevelopmentWithoutHost_FallsBackToOutboxDefaults()
    {
        var options = Resolve(isDevelopment: true);

        Assert.Multiple(() =>
        {
            Assert.That(options.IsConfigured, Is.False);
            Assert.That(options.Host, Is.Empty);
            Assert.That(options.Port, Is.EqualTo(SmtpEmailOptions.DefaultPort));
            Assert.That(options.UseStartTls, Is.True);
            Assert.That(options.TimeoutSeconds, Is.EqualTo(SmtpEmailOptions.DefaultTimeoutSeconds));
            Assert.That(options.FromAddress, Is.EqualTo("no-reply@roomtorentradar.local"));
            Assert.That(options.FromName, Is.EqualTo("RoomToRentRadar"));
            Assert.That(options.OutboxDirectory, Is.Empty);
        });
    }

    [Test]
    public void Resolve_OutsideDevelopmentWithoutHost_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(isDevelopment: false));

        Assert.That(exception!.Message, Does.Contain("Host is required"));
    }

    [Test]
    public void Resolve_ReadsConfiguredValues()
    {
        var options = Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:Host", "smtp.example.test"),
            ("Auth:Email:Smtp:Port", "2525"),
            ("Auth:Email:Smtp:UseStartTls", "false"),
            ("Auth:Email:Smtp:TimeoutSeconds", "5"),
            ("Auth:Email:Smtp:FromAddress", "otp@example.test"),
            ("Auth:Email:Smtp:FromName", "OTP Sender"),
            ("Auth:Email:Smtp:OutboxDirectory", "custom-outbox"));

        Assert.Multiple(() =>
        {
            Assert.That(options.IsConfigured, Is.True);
            Assert.That(options.Host, Is.EqualTo("smtp.example.test"));
            Assert.That(options.Port, Is.EqualTo(2525));
            Assert.That(options.UseStartTls, Is.False);
            Assert.That(options.TimeoutSeconds, Is.EqualTo(5));
            Assert.That(options.FromAddress, Is.EqualTo("otp@example.test"));
            Assert.That(options.FromName, Is.EqualTo("OTP Sender"));
            Assert.That(options.OutboxDirectory, Is.EqualTo("custom-outbox"));
        });
    }

    [Test]
    public void Resolve_WithInvalidFromAddress_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:FromAddress", "not-an-address")));

        Assert.That(exception!.Message, Does.Contain("FromAddress"));
    }

    [TestCase("0")]
    [TestCase("65536")]
    public void Resolve_WithPortOutOfRange_Throws(string port)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:Port", port)));

        Assert.That(exception!.Message, Does.Contain("Port"));
    }

    [TestCase("0")]
    [TestCase("61")]
    public void Resolve_WithTimeoutOutOfRange_Throws(string timeoutSeconds)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:TimeoutSeconds", timeoutSeconds)));

        Assert.That(exception!.Message, Does.Contain("TimeoutSeconds"));
    }

    [Test]
    public void Resolve_WithUserNameWithoutPassword_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:UserName", "mailer")));

        Assert.That(exception!.Message, Does.Contain("configured together"));
    }

    [Test]
    public void Resolve_WithPasswordWithoutUserName_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:Password", "secret")));

        Assert.That(exception!.Message, Does.Contain("configured together"));
    }

    [Test]
    public void Resolve_OutsideDevelopmentWithPlaintextCredentials_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(
            isDevelopment: false,
            ("Auth:Email:Smtp:Host", "smtp.example.test"),
            ("Auth:Email:Smtp:UseStartTls", "false"),
            ("Auth:Email:Smtp:UserName", "mailer"),
            ("Auth:Email:Smtp:Password", "secret")));

        Assert.That(exception!.Message, Does.Contain("UseStartTls"));
    }

    [Test]
    public void Resolve_OutsideDevelopmentWithPlaintextAndNoCredentials_IsAllowed()
    {
        var options = Resolve(
            isDevelopment: false,
            ("Auth:Email:Smtp:Host", "smtp.example.test"),
            ("Auth:Email:Smtp:UseStartTls", "false"));

        Assert.Multiple(() =>
        {
            Assert.That(options.IsConfigured, Is.True);
            Assert.That(options.UseStartTls, Is.False);
        });
    }

    [Test]
    public void Resolve_InDevelopmentWithPlaintextCredentials_IsAllowed()
    {
        var options = Resolve(
            isDevelopment: true,
            ("Auth:Email:Smtp:Host", "smtp.example.test"),
            ("Auth:Email:Smtp:UseStartTls", "false"),
            ("Auth:Email:Smtp:UserName", "mailer"),
            ("Auth:Email:Smtp:Password", "secret"));

        Assert.That(options.IsConfigured, Is.True);
    }

    private static SmtpEmailOptions Resolve(
        bool isDevelopment,
        params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(
                setting => setting.Key,
                setting => (string?)setting.Value))
            .Build();
        return SmtpEmailOptions.Resolve(configuration, isDevelopment);
    }
}
