using System.Text.RegularExpressions;

namespace RoomToRentRadarUnitTests.ClientAdapters.Support;

/// <summary>
/// Regex-based reader for <c>docs/api/openapi.yaml</c> (no YAML dependency), located by walking up
/// parent directories from the test assembly like <c>ProblemCodesOpenApiSyncTests</c>.
/// </summary>
internal static class OpenApiSpec
{
    private static readonly Lazy<string> YamlText = new(ReadOpenApi);
    private static readonly Lazy<IReadOnlyList<ApiOperation>> Operations = new(() => ReadOperations().ToArray());

    public static IReadOnlyList<string> OperationIds => Operations.Value.Select(o => o.OperationId).ToArray();

    public static IReadOnlyList<string> EnumValues(string schemaName)
    {
        var block = SchemaBlock(schemaName);
        return ParseEnumList(block, $"schema '{schemaName}'");
    }

    public static IReadOnlyList<string> SchemaPropertyNames(string schemaName)
    {
        var block = SchemaBlock(schemaName);
        var properties = Regex.Match(block, @"^      properties:\s*$", RegexOptions.Multiline);
        if (!properties.Success)
        {
            throw new AssertionException($"Schema '{schemaName}' has no block-style 'properties:' section in docs/api/openapi.yaml.");
        }

        var section = block[(properties.Index + properties.Length)..];
        return Regex.Matches(section, @"^        (?<name>[A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value)
            .ToArray();
    }

    public static IReadOnlyList<string> OperationsRequiring(string parameterRef) =>
        Operations.Value
            .Where(o => o.PathHeader.Contains(parameterRef, StringComparison.Ordinal) || o.Body.Contains(parameterRef, StringComparison.Ordinal))
            .Select(o => o.OperationId)
            .ToArray();

    /// <summary>Enum of an inline property, e.g. <c>resolution</c> of <c>ResolveReportRequest</c>; raw values, including a literal <c>null</c> when present.</summary>
    public static IReadOnlyList<string> PropertyEnumValues(string schemaName, string propertyName)
    {
        var block = SchemaBlock(schemaName);
        var property = new Regex($@"^        {Regex.Escape(propertyName)}:\s*$", RegexOptions.Multiline).Match(block);
        if (!property.Success)
        {
            throw new AssertionException($"Property '{propertyName}' of schema '{schemaName}' was not found in docs/api/openapi.yaml.");
        }

        var next = new Regex(@"^        [A-Za-z_][A-Za-z0-9_]*:", RegexOptions.Multiline).Match(block, property.Index + property.Length);
        var body = next.Success ? block[property.Index..next.Index] : block[property.Index..];
        return ParseEnumList(body, $"property '{propertyName}' of schema '{schemaName}'");
    }

    /// <summary>Enum of an inline operation parameter, e.g. the lowercase <c>queue</c> query parameter of <c>listModerationRooms</c>.</summary>
    public static IReadOnlyList<string> OperationParameterEnumValues(string operationId, string parameterName)
    {
        var operation = Operations.Value.FirstOrDefault(o => o.OperationId == operationId)
            ?? throw new AssertionException($"operationId '{operationId}' was not found in docs/api/openapi.yaml.");
        var match = Regex.Match(operation.Body, $@"- name: {Regex.Escape(parameterName)}\b[\s\S]*?enum:\s*\[(?<values>[^\]]*)\]");
        if (!match.Success)
        {
            throw new AssertionException($"Parameter '{parameterName}' of operation '{operationId}' has no enum flow list in docs/api/openapi.yaml.");
        }

        return SplitValues(match.Groups["values"].Value);
    }

    private static string PathsSection
    {
        get
        {
            var text = YamlText.Value;
            var start = Regex.Match(text, @"^paths:\s*$", RegexOptions.Multiline);
            var end = Regex.Match(text, @"^components:\s*$", RegexOptions.Multiline);
            if (!start.Success || !end.Success || end.Index <= start.Index)
            {
                throw new AssertionException("The 'paths:' and 'components:' sections were not found in docs/api/openapi.yaml.");
            }

            return text[start.Index..end.Index];
        }
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

    private static IEnumerable<ApiOperation> ReadOperations()
    {
        var paths = PathsSection;
        var pathStarts = Regex.Matches(paths, @"^  /[^:\s]*:", RegexOptions.Multiline).Select(m => m.Index).ToArray();
        for (var i = 0; i < pathStarts.Length; i++)
        {
            var blockEnd = i + 1 < pathStarts.Length ? pathStarts[i + 1] : paths.Length;
            var block = paths[pathStarts[i]..blockEnd];
            var ops = Regex.Matches(block, @"operationId:\s*(?<id>[A-Za-z0-9_]+)");
            var header = block[..(ops.Count > 0 ? ops[0].Index : block.Length)];
            for (var j = 0; j < ops.Count; j++)
            {
                var bodyEnd = j + 1 < ops.Count ? ops[j + 1].Index : block.Length;
                yield return new ApiOperation(ops[j].Groups["id"].Value, header, block[ops[j].Index..bodyEnd]);
            }
        }
    }

    private static IReadOnlyList<string> ParseEnumList(string text, string source)
    {
        var match = Regex.Match(text, @"enum:\s*\[(?<values>[^\]]*)\]");
        if (!match.Success)
        {
            throw new AssertionException($"No enum flow list found in {source} of docs/api/openapi.yaml.");
        }

        return SplitValues(match.Groups["values"].Value);
    }

    private static IReadOnlyList<string> SplitValues(string raw) =>
        raw.Split(',')
            .Select(value => value.Trim().Trim('\'', '"'))
            .Where(value => value.Length > 0)
            .ToArray();

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

    private sealed record ApiOperation(string OperationId, string PathHeader, string Body);
}