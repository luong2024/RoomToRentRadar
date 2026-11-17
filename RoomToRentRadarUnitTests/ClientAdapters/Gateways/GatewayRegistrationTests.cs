using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 6 (AC-2): AddClientAdapters registers the feature gateways on the shared pipeline
/// (Bearer from ISessionStore, network failures as ApiException.IsNetworkError).
/// </summary>
[TestFixture]
public class GatewayRegistrationTests
{
    [Test]
    public void RoomAndCampusGateways_Resolve_ToHttpImplementations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions());
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IRoomGateway>(), Is.InstanceOf<HttpRoomGateway>());
        Assert.That(provider.GetRequiredService<ICampusGateway>(), Is.InstanceOf<HttpCampusGateway>());
    }

    [Test]
    public async Task RoomGateway_Request_AttachesBearerFromSessionStore()
    {
        var stub = new StubHttpMessageHandler(_ => OkPage());
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a1", "r1"));
        using var provider = BuildProvider(stub, store);
        var gateway = provider.GetRequiredService<IRoomGateway>();

        await gateway.SearchRoomsAsync(new RoomSearchRequest());

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Path, Is.EqualTo("/api/rooms"));
        Assert.That(recorded.Authorization, Is.EqualTo("Bearer a1"));
    }

    [Test]
    public void RoomGateway_NetworkDown_ThrowsApiExceptionNetworkError()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var store = new InMemorySessionStore();
        store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();
        using var provider = BuildProvider(stub, store);
        var gateway = provider.GetRequiredService<IRoomGateway>();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.SearchRoomsAsync(new RoomSearchRequest()));

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(ex.InnerException, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public void EveryGatewayInterface_IsRegistered_AsHttp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions());
        using var provider = services.BuildServiceProvider();

        Assert.That(FeatureGatewayInterfaces, Has.Length.EqualTo(10));
        foreach (var gatewayInterface in FeatureGatewayInterfaces)
        {
            Assert.That(provider.GetRequiredService(gatewayInterface), Is.InstanceOf(HttpTypeFor(gatewayInterface)), gatewayInterface.Name);
        }
    }

    [Test]
    public void UseMocksFalse_ResolvesHttpGateways_ForEveryGatewayInterface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions { UseMocks = false });
        using var provider = services.BuildServiceProvider();

        foreach (var gatewayInterface in FeatureGatewayInterfaces)
        {
            Assert.That(provider.GetRequiredService(gatewayInterface), Is.InstanceOf(HttpTypeFor(gatewayInterface)), gatewayInterface.Name);
        }
    }

    [Test]
    public void UseMocksTrue_DebugBuild_ResolvesMockRoomAndCampus()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions { UseMocks = true }, isDebugBuild: true);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IRoomGateway>(), Is.InstanceOf<MockRoomGateway>());
        Assert.That(provider.GetRequiredService<ICampusGateway>(), Is.InstanceOf<MockCampusGateway>());
        // The other eight gateways stay Http until their feature tasks ship their own mocks.
        foreach (var gatewayInterface in FeatureGatewayInterfaces.Where(t => t != typeof(IRoomGateway) && t != typeof(ICampusGateway)))
        {
            Assert.That(provider.GetRequiredService(gatewayInterface), Is.InstanceOf(HttpTypeFor(gatewayInterface)), gatewayInterface.Name);
        }
    }

    [Test]
    public void UseMocksTrue_ReleaseBuild_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddClientAdapters(new ApiOptions { UseMocks = true }, isDebugBuild: false));

        Assert.That(ex!.Message, Is.EqualTo("UseMocks is only allowed in Debug builds."));
    }

    [Test]
    public void UseMocksTrue_AuthGateway_StillHttp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions { UseMocks = true }, isDebugBuild: true);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IAuthGateway>(), Is.InstanceOf<HttpAuthGateway>());
    }

#if DEBUG
    [Test]
    public void AddClientAdapters_PublicOverload_UseMocksTrue_IsAllowed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());

        Assert.DoesNotThrow(() => services.AddClientAdapters(new ApiOptions { UseMocks = true }));
    }
#else
    [Test]
    public void AddClientAdapters_PublicOverload_UseMocksTrue_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddClientAdapters(new ApiOptions { UseMocks = true }));

        Assert.That(ex!.Message, Is.EqualTo("UseMocks is only allowed in Debug builds."));
    }
#endif

    private static readonly Type[] FeatureGatewayInterfaces = typeof(IRoomGateway).Assembly
        .GetTypes()
        .Where(t => t.IsInterface
            && t.Namespace == "RoomToRentRadar.Client.Adapters.Abstractions"
            && t.Name.EndsWith("Gateway", StringComparison.Ordinal)
            && t != typeof(IAuthGateway))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    private static Type HttpTypeFor(Type gatewayInterface) =>
        typeof(IRoomGateway).Assembly.GetType(
            "RoomToRentRadar.Client.Adapters.Http.Http" + gatewayInterface.Name[1..], throwOnError: true)!;

    private static ServiceProvider BuildProvider(StubHttpMessageHandler stub, InMemorySessionStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(store);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => stub));
        services.AddClientAdapters(new ApiOptions());
        return services.BuildServiceProvider();
    }

    private static AuthTokensDto Tokens(string access, string refresh) => new(
        access,
        refresh,
        DateTime.UtcNow.AddMinutes(30),
        DateTime.UtcNow.AddDays(7),
        new UserProfileDto("u1", "student@ictu.edu.vn", "Test Student", "Student", "Local", null, null, true, DateTime.UtcNow));

    private static HttpResponseMessage OkPage() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new PagedResult<RoomCardDto>(
                        new[]
                        {
                            new RoomCardDto(
                                "room-1", "Test room", 2_000_000, 20, RoomType.BoardingRoom, 0.5, true,
                                ListingStatus.Available, null, 21.5851, 105.8066,
                                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false)
                        },
                        1, 1, 20),
                    ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
}