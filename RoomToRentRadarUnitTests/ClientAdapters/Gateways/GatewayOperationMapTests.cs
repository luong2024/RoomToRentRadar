using System.Reflection;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadarUnitTests.ClientAdapters.Support;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 5 (AC-1): every gateway method maps 1:1 to an operationId in docs/api/openapi.yaml
/// via [ApiOperation]; 50 operations across 10 interfaces (PLAN §5.3).
/// </summary>
[TestFixture]
public class GatewayOperationMapTests
{
    // IAuthGateway (T00.13) is deliberately not part of this map: the 6 auth operations stay there unchanged (PLAN Q3).
    private static readonly Type[] GatewayInterfaces = typeof(IRoomGateway).Assembly
        .GetTypes()
        .Where(t => t.IsInterface
            && t.Namespace == "RoomToRentRadar.Client.Adapters.Abstractions"
            && t.Name.EndsWith("Gateway", StringComparison.Ordinal)
            && t != typeof(IAuthGateway))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    private static readonly MethodInfo[] GatewayMethods =
        GatewayInterfaces.SelectMany(t => t.GetMethods()).ToArray();

    // Explicit exclusions (PLAN §5.3, Q3): the 6 auth operations already on IAuthGateway (T00.13);
    // getHealth is a liveness probe, not a feature operation; getAuditLogs is a server-side audit query
    // outside the client gateway scope.
    private static readonly string[] ExcludedOperationIds =
    [
        "registerUser",
        "loginUser",
        "loginWithIctuSso",
        "refreshToken",
        "revokeToken",
        "getCurrentUser",
        "getHealth",
        "getAuditLogs"
    ];

    // Per-interface method counts from the operationId -> gateway table in PLAN §5.3.
    private static readonly (string InterfaceName, int MethodCount)[] ExpectedMethodCounts =
    [
        ("IRoomGateway", 6),
        ("ICampusGateway", 1),
        ("IFavouriteGateway", 3),
        ("ISavedSearchGateway", 5),
        ("INotificationGateway", 4),
        ("IChatGateway", 6),
        ("IMyListingGateway", 13),
        ("IReportGateway", 1),
        ("IAdminGateway", 10),
        ("IProfileGateway", 1)
    ];

    private static IEnumerable<TestCaseData> AttributeCases() =>
        GatewayMethods.Select(m => new TestCaseData(m)
            .SetName($"EveryGatewayMethod_HasApiOperation_ExistingInOpenApi({m.DeclaringType!.Name}.{m.Name})"));

    private static IEnumerable<TestCaseData> SignatureCases() =>
        GatewayMethods.Select(m => new TestCaseData(m)
            .SetName($"GatewayMethods_ReturnTask_AndTakeCancellationTokenLast({m.DeclaringType!.Name}.{m.Name})"));

    [TestCaseSource(nameof(AttributeCases))]
    public void EveryGatewayMethod_HasApiOperation_ExistingInOpenApi(MethodInfo method)
    {
        var attribute = method.GetCustomAttribute<ApiOperationAttribute>();
        Assert.That(attribute, Is.Not.Null,
            $"{method.DeclaringType!.Name}.{method.Name} must carry [ApiOperation(\"<operationId>\")].");
        Assert.That(OpenApiSpec.OperationIds, Does.Contain(attribute!.OperationId),
            $"operationId '{attribute.OperationId}' of {method.DeclaringType.Name}.{method.Name} does not exist in docs/api/openapi.yaml.");
    }

    [Test]
    public void NoOperationId_MappedByTwoMethods()
    {
        var duplicates = GatewayMethods
            .GroupBy(m => m.GetCustomAttribute<ApiOperationAttribute>()?.OperationId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key ?? "(missing ApiOperation)")
            .ToArray();

        Assert.That(duplicates, Is.Empty,
            $"operationIds mapped by more than one gateway method: {string.Join(", ", duplicates)}.");
    }

    [Test]
    public void EveryOpenApiOperationId_IsMappedOrExplicitlyExcluded()
    {
        var mapped = GatewayMethods
            .Select(m => m.GetCustomAttribute<ApiOperationAttribute>()?.OperationId)
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        var unmapped = OpenApiSpec.OperationIds
            .Where(id => !mapped.Contains(id) && !ExcludedOperationIds.Contains(id))
            .ToArray();

        Assert.That(unmapped, Is.Empty,
            $"openapi operationIds without a gateway method or an explicit exclusion: {string.Join(", ", unmapped)}.");
    }

    [Test]
    public void ExcludedOperationIds_StillExistInOpenApi()
    {
        var stale = ExcludedOperationIds
            .Where(id => !OpenApiSpec.OperationIds.Contains(id))
            .ToArray();

        Assert.That(stale, Is.Empty,
            $"Excluded operationIds no longer present in docs/api/openapi.yaml: {string.Join(", ", stale)}.");
    }

    [TestCaseSource(nameof(SignatureCases))]
    public void GatewayMethods_ReturnTask_AndTakeCancellationTokenLast(MethodInfo method)
    {
        Assert.Multiple(() =>
        {
            var returnsTask = method.ReturnType == typeof(Task)
                || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
            Assert.That(returnsTask, Is.True,
                $"{method.DeclaringType!.Name}.{method.Name} must return Task or Task<T>.");

            var last = method.GetParameters()[^1];
            Assert.That(last.ParameterType, Is.EqualTo(typeof(CancellationToken)),
                $"{method.DeclaringType!.Name}.{method.Name} must take CancellationToken as the last parameter.");
            Assert.That(last.Name, Is.EqualTo("ct"),
                $"{method.DeclaringType!.Name}.{method.Name} must name the last parameter 'ct'.");
            Assert.That(last.IsOptional, Is.True,
                $"{method.DeclaringType!.Name}.{method.Name} must declare 'CancellationToken ct = default'.");
        });
    }

    [Test]
    public void OperationsRequiringIfMatch_TakeLongExpectedVersion()
    {
        var expected = OpenApiSpec.OperationsRequiring("#/components/parameters/IfMatch");
        var actual = GatewayMethods
            .Where(m => m.GetParameters().Any(p => p.Name == "expectedVersion" && p.ParameterType == typeof(long)))
            .Select(m => m.GetCustomAttribute<ApiOperationAttribute>()?.OperationId)
            .ToArray();

        Assert.That(actual, Is.EquivalentTo(expected),
            "Operations referencing '#/components/parameters/IfMatch' in openapi must map to methods taking 'long expectedVersion', and vice versa (ADR-012 §6).");
    }

    [Test]
    public void GatewayCount_Is50Methods_In10Interfaces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GatewayInterfaces, Has.Length.EqualTo(10),
                "T00.14 defines exactly 10 feature gateways (PLAN §5.3).");
            Assert.That(GatewayMethods, Has.Length.EqualTo(50),
                "50 of the 58 openapi operations belong to the feature gateways (58 - 6 auth - getHealth - getAuditLogs).");
            foreach (var (interfaceName, methodCount) in ExpectedMethodCounts)
            {
                var actual = GatewayInterfaces.Single(t => t.Name == interfaceName).GetMethods().Length;
                Assert.That(actual, Is.EqualTo(methodCount),
                    $"{interfaceName} method count must match the operationId table in PLAN §5.3.");
            }
        });
    }
}