using Microsoft.AspNetCore.Authentication;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
                BearerTokenAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.ModeratorOrAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoleWire.Moderator, UserRoleWire.Admin));
            options.AddPolicy(AuthPolicies.Landlord, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoleWire.Landlord));
        });

        return services;
    }
}
