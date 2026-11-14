using System.Reflection;
using System.Text.RegularExpressions;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>ADR-012, system-architecture section 7.2: errorCode values are stable, unique UPPER_SNAKE codes.</summary>
[TestFixture]
public class ProblemCodesTests
{
    private static FieldInfo[] GetConstantFields() =>
        typeof(ProblemCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToArray();

    [Test]
    public void ProblemCodes_AllConstants_AreUpperSnakeCaseAndUnique()
    {
        var values = GetConstantFields()
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.Multiple(() =>
        {
            foreach (var value in values)
            {
                Assert.That(Regex.IsMatch(value, "^[A-Z]+(_[A-Z]+)*$"), Is.True,
                    $"Value '{value}' is not UPPER_SNAKE_CASE.");
            }

            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Length));
        });
    }

    [Test]
    public void ProblemCodes_Count_Is28()
    {
        Assert.That(GetConstantFields(), Has.Length.EqualTo(28));
    }

    [Test]
    public void ProblemCodes_AllSet_MatchesDeclaredConstants()
    {
        var declared = GetConstantFields()
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(ProblemCodes.All, Has.Count.EqualTo(28));
            Assert.That(ProblemCodes.All.SetEquals(declared), Is.True);
        });
    }

    [Test]
    public void ProblemCodes_ContainsFourGenericCodes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProblemCodes.All.Contains("NOT_FOUND"), Is.True);
            Assert.That(ProblemCodes.All.Contains("FORBIDDEN"), Is.True);
            Assert.That(ProblemCodes.All.Contains("UNAUTHORIZED"), Is.True);
            Assert.That(ProblemCodes.All.Contains("INTERNAL_ERROR"), Is.True);
            Assert.That(ProblemCodes.NotFound, Is.EqualTo("NOT_FOUND"));
            Assert.That(ProblemCodes.Forbidden, Is.EqualTo("FORBIDDEN"));
            Assert.That(ProblemCodes.Unauthorized, Is.EqualTo("UNAUTHORIZED"));
            Assert.That(ProblemCodes.InternalError, Is.EqualTo("INTERNAL_ERROR"));
        });
    }
}