using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Infrastructure.Email;
using RoomToRentRadar.Ports.Models.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class FileEmailSenderTests
{
    private static readonly RegistrationOtpEmail Email = new(
        "new.user@example.test",
        "New User",
        "135790",
        new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc));

    private string _contentRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _contentRoot = Path.Combine(
            Path.GetTempPath(),
            "rtrr-file-email-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Test]
    public void ResolveOutboxDirectory_WithoutOverride_UsesContentRootOutbox()
    {
        var resolved = FileEmailSender.ResolveOutboxDirectory(new SmtpEmailOptions(), _contentRoot);

        Assert.That(resolved, Is.EqualTo(Path.Combine(_contentRoot, "outbox")));
    }

    [Test]
    public void ResolveOutboxDirectory_WithRelativeOverride_ResolvesAgainstContentRoot()
    {
        var options = new SmtpEmailOptions { OutboxDirectory = Path.Combine("custom", "outbox") };

        var resolved = FileEmailSender.ResolveOutboxDirectory(options, _contentRoot);

        Assert.That(
            resolved,
            Is.EqualTo(Path.GetFullPath(Path.Combine(_contentRoot, "custom", "outbox"))));
    }

    [Test]
    public async Task SendRegistrationOtpAsync_WritesEmlWithHeadersAndRelativeLifetime()
    {
        var sender = CreateSender();

        var sent = await sender.SendRegistrationOtpAsync(Email, CancellationToken.None);

        var outbox = Path.Combine(_contentRoot, "outbox");
        var files = Directory.GetFiles(outbox, "registration-otp-*.eml");
        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.True);
            Assert.That(files, Has.Length.EqualTo(1));
        });

        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("From:"));
            Assert.That(content, Does.Contain("To: new.user@example.test"));
            Assert.That(content, Does.Contain("Subject: Mã xác minh đăng ký RoomToRentRadar"));
            Assert.That(content, Does.Contain(Email.Code));
            Assert.That(content, Does.Contain("Mã có hiệu lực trong 10 phút."));
            Assert.That(content, Does.Not.Contain("(UTC)"));
        });
    }

    [Test]
    public async Task SendRegistrationOtpAsync_WhenOutboxCannotBeCreated_ReturnsFalse()
    {
        var blockingFile = Path.Combine(_contentRoot, "blocked");
        await File.WriteAllTextAsync(blockingFile, "not a directory");
        var sender = CreateSender(new SmtpEmailOptions { OutboxDirectory = blockingFile });

        var sent = await sender.SendRegistrationOtpAsync(Email, CancellationToken.None);

        Assert.That(sent, Is.False);
    }

    private FileEmailSender CreateSender(SmtpEmailOptions? options = null)
        => new(
            options ?? new SmtpEmailOptions(),
            new StubHostEnvironment(_contentRoot),
            NullLogger<FileEmailSender>.Instance);

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RoomToRentRadar.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
