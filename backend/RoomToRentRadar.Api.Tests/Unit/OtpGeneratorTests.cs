using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class OtpGeneratorTests
{
    [Test]
    public void Generate_AlwaysReturnsCodesWithAccountRulesLengthAndDigits()
    {
        var generator = new OtpGenerator();

        var codes = Enumerable.Range(0, 500).Select(_ => generator.Generate()).ToList();

        Assert.Multiple(() =>
        {
            foreach (var code in codes)
            {
                Assert.That(code.Length, Is.EqualTo(AccountRules.OtpLength), $"Code '{code}' has a wrong length.");
                Assert.That(code.All(char.IsAsciiDigit), Is.True, $"Code '{code}' contains non-digits.");
                Assert.That(AccountRules.IsOtpCodeValid(code), Is.True, $"Code '{code}' is not valid.");
            }

            Assert.That(codes.Distinct().Count(), Is.GreaterThan(1));
        });
    }
}
