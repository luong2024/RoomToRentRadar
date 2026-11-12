using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Authentication;

namespace RoomToRentRadar.Adapters.Tests.Authentication;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Test]
    public void Validate_WhenAllOptionsValidWith256BitSecret_ShouldSucceed()
    {
        var options = new JwtOptions
        {
            Issuer = "RoomToRentRadar.Api",
            Audience = "RoomToRentRadar.Client",
            Secret = "0123456789abcdef0123456789abcdef", // 32 bytes (256 bits)
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7,
            ClockSkewSeconds = 30
        };

        var result = _validator.Validate(null, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WhenSecretMissing_ShouldFail(string secret)
    {
        var options = new JwtOptions
        {
            Issuer = "RoomToRentRadar.Api",
            Audience = "RoomToRentRadar.Client",
            Secret = secret,
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        var result = _validator.Validate(null, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failed, Is.True);
            Assert.That(result.FailureMessage, Does.Contain("JWT__SECRET"));
        });
    }

    [Test]
    public void Validate_WhenSecretShorterThan256Bits_ShouldFail()
    {
        var options = new JwtOptions
        {
            Issuer = "RoomToRentRadar.Api",
            Audience = "RoomToRentRadar.Client",
            Secret = "0123456789abcdef0123456789abcde", // 31 bytes (248 bits)
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        var result = _validator.Validate(null, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failed, Is.True);
            Assert.That(result.FailureMessage, Does.Contain("256 bits"));
        });
    }

    [Test]
    public void Validate_WhenIssuerOrAudienceOrLifetimesInvalid_ShouldReportAllFailures()
    {
        var options = new JwtOptions
        {
            Issuer = "",
            Audience = " ",
            Secret = "0123456789abcdef0123456789abcdef",
            AccessTokenExpirationMinutes = 0,
            RefreshTokenExpirationDays = -1,
            ClockSkewSeconds = -5
        };

        var result = _validator.Validate(null, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Failed, Is.True);
            Assert.That(result.Failures?.Count(), Is.EqualTo(5));
        });
    }

    [Test]
    public void ServiceCollection_WhenSecretNotConfigured_ShouldThrowOptionsValidationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "RoomToRentRadar.Api",
                ["Jwt:Audience"] = "RoomToRentRadar.Client",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        });
    }
}
