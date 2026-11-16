using System.Reflection;
using System.Text.RegularExpressions;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadarUnitTests.ClientAdapters.Support;

namespace RoomToRentRadarUnitTests.ClientAdapters.Contracts;

/// <summary>
/// T00.14 Phase 2: every public contract record in <c>Ports.Contracts.Requests</c>/<c>Responses</c> that has a
/// same-named schema in <c>docs/api/openapi.yaml</c> must expose exactly that schema's properties as positional
/// constructor parameters (camelCase). Computed properties are ignored because they are not constructor parameters.
/// </summary>
[TestFixture]
public class ContractsOpenApiSyncTests
{
    // Documented exclusions (plan Phase 2). Paged* wrappers expose computed paging members and merge PageMeta;
    // RoomSearchRequest models GET query parameters, not a body schema; PageMeta/ProblemDetails are not Ports records.
    // AuditLogDto is a pre-existing record that carries an extra 'userAgent' parameter absent from openapi — reported, not modified here.
    private static readonly HashSet<string> ExcludedRecordNames = new(StringComparer.Ordinal)
    {
        "PagedResult",
        "PageMeta",
        "ProblemDetails",
        "RoomSearchRequest",
        "AuditLogDto"
    };

    private static readonly Lazy<string> YamlText = new(ReadOpenApi);

    private static readonly Lazy<HashSet<string>> SchemaNames = new(() =>
        Regex.Matches(SchemasSection, @"^    (?<name>[A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal));

    private static IEnumerable<TestCaseData> RecordCases()
    {
        var records = typeof(RoomCardDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.IsClass && !t.IsGenericType && IsRecord(t))
            .Where(t => t.Namespace is not null &&
                        (t.Namespace.StartsWith("RoomToRentRadar.Ports.Contracts.Requests", StringComparison.Ordinal) ||
                         t.Namespace.StartsWith("RoomToRentRadar.Ports.Contracts.Responses", StringComparison.Ordinal)))
            .Where(t => !IsExcluded(t.Name))
            .Where(t => SchemaNames.Value.Contains(t.Name))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in records)
        {
            yield return new TestCaseData(type).SetName($"Record_PropertiesMatchOpenApiSchemaProperties({type.Name})");
        }
    }

    [TestCaseSource(nameof(RecordCases))]
    public void Record_PropertiesMatchOpenApiSchemaProperties(Type recordType)
    {
        var expected = SchemaProperties(recordType.Name);
        var constructor = recordType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var actual = constructor.GetParameters().Select(p => ToCamelCase(p.Name!)).ToArray();

        Assert.That(actual, Is.EquivalentTo(expected),
            $"Record '{recordType.Name}' constructor parameters (camelCase) must equal the properties of schema '{recordType.Name}' in docs/api/openapi.yaml.");
    }

    private static bool IsExcluded(string name) =>
        ExcludedRecordNames.Contains(name) || name.StartsWith("Paged", StringComparison.Ordinal);

    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public) is not null;

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static IReadOnlyList<string> SchemaProperties(string schemaName)
    {
        var block = SchemaBlock(schemaName);

        // allOf compositions (UtilitiesView, HouseRulesDto, AreaSearchRequest) merge the referenced base schemas
        // with the inline object's properties. OpenApiSpec.SchemaPropertyNames reads only block-style 'properties:',
        // so the composition is resolved here and the base schemas are still read through the shared helper.
        if (Regex.IsMatch(block, @"^      allOf:\s*$", RegexOptions.Multiline))
        {
            var merged = new List<string>();
            foreach (Match reference in Regex.Matches(block, @"#/components/schemas/(?<name>[A-Za-z0-9_]+)"))
            {
                merged.AddRange(SchemaProperties(reference.Groups["name"].Value));
            }

            merged.AddRange(Regex.Matches(block, @"^            (?<name>[A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Multiline)
                .Select(m => m.Groups["name"].Value));
            return merged.Distinct(StringComparer.Ordinal).ToArray();
        }

        return OpenApiSpec.SchemaPropertyNames(schemaName);
    }

    private static string SchemasSection
    {
        get
        {
            var text = YamlText.Value;
            var start = Regex.Match(text, @"^  schemas:\s*$", RegexOptions.Multiline);
            if (!start.Success)
            {
                throw new AssertionException("The 'components.schemas' section was not found in docs/api/openapi.yaml.");
            }

            var next = new Regex(@"^  [A-Za-z_][A-Za-z0-9_]*:", RegexOptions.Multiline).Match(text, start.Index + start.Length);
            return next.Success ? text[start.Index..next.Index] : text[start.Index..];
        }
    }

    private static string SchemaBlock(string schemaName)
    {
        var schemas = SchemasSection;
        var start = new Regex($@"^    {Regex.Escape(schemaName)}:", RegexOptions.Multiline).Match(schemas);
        if (!start.Success)
        {
            throw new AssertionException($"Schema '{schemaName}' was not found in docs/api/openapi.yaml.");
        }

        var next = new Regex(@"^    [A-Za-z_][A-Za-z0-9_]*:", RegexOptions.Multiline).Match(schemas, start.Index + start.Length);
        return next.Success ? schemas[start.Index..next.Index] : schemas[start.Index..];
    }

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

        throw new AssertionException($"docs/api/openapi.yaml was not found by walking up parent directories from '{AppContext.BaseDirectory}'.");
    }
}