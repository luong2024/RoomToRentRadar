using System.Text.RegularExpressions;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>ADR-012, system-architecture section 7.2: the OpenAPI ProblemDetails errorCode examples must stay in sync with ProblemCodes.All.</summary>
[TestFixture]
public class ProblemCodesOpenApiSyncTests
{
    private static string ReadOpenApi()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "docs", "api", "openapi.yaml");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            dir = dir.Parent;
        }

        throw new AssertionException($"docs/api/openapi.yaml was not found by walking up parent directories from AppContext.BaseDirectory '{AppContext.BaseDirectory}'.");
    }

    private static string ReadProblemDetailsSchema()
    {
        var yaml = ReadOpenApi();
        var start = Regex.Match(yaml, @"^\s{4}ProblemDetails:\s*$", RegexOptions.Multiline);
        if (!start.Success)
        {
            Assert.Fail("The ProblemDetails schema was not found in docs/api/openapi.yaml (expected a line '    ProblemDetails:').");
        }

        var next = new Regex(@"^\s{4}\S+:\s*$", RegexOptions.Multiline).Match(yaml, start.Index + start.Length);
        return next.Success
            ? yaml[start.Index..next.Index]
            : yaml[start.Index..];
    }

    [Test]
    public void ProblemCodes_All_EqualsOpenApiErrorCodeExamples()
    {
        var block = ReadProblemDetailsSchema();
        var match = Regex.Match(block, @"examples:\s*\[(?<codes>[^\]]*)\]");
        Assert.That(match.Success, Is.True, "No errorCode examples flow list found in the ProblemDetails schema of docs/api/openapi.yaml.");

        var examples = match.Groups["codes"].Value
            .Split(',')
            .Select(code => code.Trim())
            .Where(code => code.Length > 0)
            .ToArray();

        var duplicates = examples.GroupBy(code => code).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        var missingFromOpenApi = ProblemCodes.All.Except(examples).OrderBy(code => code).ToArray();
        var missingFromProblemCodes = examples.Except(ProblemCodes.All).OrderBy(code => code).ToArray();

        var message =
            "docs/api/openapi.yaml ProblemDetails errorCode examples are out of sync with ProblemCodes.All. " +
            $"Missing from openapi.yaml: [{string.Join(", ", missingFromOpenApi)}]. " +
            $"Missing from ProblemCodes: [{string.Join(", ", missingFromProblemCodes)}].";

        Assert.Multiple(() =>
        {
            Assert.That(missingFromOpenApi, Is.Empty, message);
            Assert.That(missingFromProblemCodes, Is.Empty, message);
            Assert.That(duplicates, Is.Empty, $"Duplicate codes in the openapi.yaml errorCode examples: [{string.Join(", ", duplicates)}].");
        });
    }

    [Test]
    public void OpenApi_ProblemDetails_RequiresErrorCode()
    {
        var block = ReadProblemDetailsSchema();
        var match = Regex.Match(block, @"required:\s*\[(?<req>[^\]]*)\]");
        Assert.That(match.Success, Is.True, "No required flow list found in the ProblemDetails schema of docs/api/openapi.yaml.");

        var required = match.Groups["req"].Value
            .Split(',')
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .ToArray();

        Assert.That(required, Does.Contain("errorCode"));
    }
}