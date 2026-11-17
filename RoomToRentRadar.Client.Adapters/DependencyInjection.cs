using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;

namespace RoomToRentRadar.Client.Adapters;

public static partial class DependencyInjection
{
    /// <summary>
    /// Registers the shared client API foundation (ADR-003): <see cref="ApiOptions"/>, session events,
    /// the single-flight <see cref="TokenRefreshCoordinator"/> and the HTTP gateways, each with the
    /// handler pipeline NetworkErrorHandler (outermost) then AuthTokenHandler.
    /// The app registers <c>ISessionStore</c> itself (MAUI: MauiSecureSessionStore).
    /// </summary>
    /// <remarks>
    /// How to add a gateway:
    /// 1. Define the interface in Abstractions/ (I&lt;Feature&gt;Gateway).
    /// 2. Implement Http/Http&lt;Feature&gt;Gateway with constructor (HttpClient, ILogger&lt;Http&lt;Feature&gt;Gateway&gt;? = null).
    ///    Use absolute "/api/..." paths, write JSON with <see cref="ApiResponseReader.JsonOptions"/> and read responses
    ///    with ApiResponseReader.ReadAsync / EnsureSuccessAsync. Do not attach Authorization, handle 401 or catch
    ///    HttpRequestException yourself — the handlers already do it (network errors arrive as ApiException.IsNetworkError).
    /// 3. Add one line in DependencyInjection.Gateways.cs (AddFeatureGateways):
    ///    AddApiHttpClient&lt;IFeatureGateway, HttpFeatureGateway&gt;(services, options);
    /// 4. Optional mock: AddGateway&lt;IFeatureGateway, HttpFeatureGateway, MockFeatureGateway&gt;(services, options);
    ///    mocks are Debug-only — MockGuard rejects UseMocks in Release builds.
    /// 5. Test with services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => stub))
    ///    plus InMemorySessionStore — no real network needed.
    /// </remarks>
    public static IServiceCollection AddClientAdapters(this IServiceCollection services, ApiOptions options)
        => AddClientAdapters(services, options, ClientBuildInfo.IsDebugBuild);

    internal static IServiceCollection AddClientAdapters(this IServiceCollection services, ApiOptions options, bool isDebugBuild)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        MockGuard.EnsureAllowed(options.UseMocks, isDebugBuild);

        services.AddSingleton(options);
        services.AddSingleton<SessionEvents>();
        services.AddSingleton<ISessionEvents>(sp => sp.GetRequiredService<SessionEvents>());
        services.AddSingleton<TokenRefreshCoordinator>();
        services.AddTransient<AuthTokenHandler>();
        services.AddTransient<NetworkErrorHandler>();

        // Refresh client: NetworkErrorHandler only — AuthTokenHandler must never wrap refresh (no recursion).
        services.AddHttpClient(ApiClientNames.Refresh, client =>
        {
            client.BaseAddress = options.BaseUrl;
            client.Timeout = options.Timeout + TimeSpan.FromSeconds(5);
        }).AddHttpMessageHandler<NetworkErrorHandler>();

        AddApiHttpClient<IAuthGateway, HttpAuthGateway>(services, options);
        AddFeatureGateways(services, options);

        return services;
    }

    private static IHttpClientBuilder AddApiHttpClient<TService, TImpl>(IServiceCollection services, ApiOptions options)
        where TService : class
        where TImpl : class, TService
    {
        // Registration order = execution order: NetworkErrorHandler outermost, AuthTokenHandler inside.
        // HttpClient.Timeout is only a backstop; NetworkErrorHandler applies options.Timeout itself.
        return services.AddHttpClient<TService, TImpl>(client =>
            {
                client.BaseAddress = options.BaseUrl;
                client.Timeout = options.Timeout + TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<NetworkErrorHandler>()
            .AddHttpMessageHandler<AuthTokenHandler>();
    }
}