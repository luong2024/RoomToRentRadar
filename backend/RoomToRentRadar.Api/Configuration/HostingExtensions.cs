using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace RoomToRentRadar.Api.Configuration;

public static class HostingExtensions
{
    public const string DevelopmentCorsPolicy = "Development";

    public static IServiceCollection AddDevelopmentCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(DevelopmentCorsPolicy, policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
        });
        return services;
    }

    public static IServiceCollection AddReverseProxySupport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Proxies are only trusted when explicitly configured; loopback stays trusted by default.
            var section = configuration.GetSection("ForwardedHeaders");

            foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
            }
        });
        return services;
    }
}
