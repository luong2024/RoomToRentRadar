using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBearerAuthentication(
        this IServiceCollection services,
        AuthTokenOptions tokenOptions)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = tokenOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = tokenOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(tokenOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var token = GetRawToken(context);
                        if (token is null)
                        {
                            context.Fail("The bearer token is missing.");
                            return;
                        }

                        var authUseCase = context.HttpContext.RequestServices
                            .GetRequiredService<IAuthUseCase>();
                        var user = await authUseCase.AuthenticateAsync(
                            token,
                            context.HttpContext.RequestAborted);
                        if (user is null)
                        {
                            context.Fail("The session has been revoked or has expired.");
                            return;
                        }

                        // The session lookup already loaded the account, so build the principal
                        // from it instead of relying on claim-name mapping in the JWT handler.
                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                            new Claim(ClaimTypes.Name, user.DisplayName),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, UserRoleWire.ToWireValue(user.Role))
                        ], JwtBearerDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(
                            AuthProblemFactory.UnauthorizedProblem(),
                            options: null,
                            contentType: "application/problem+json",
                            context.HttpContext.RequestAborted);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(
                            AuthProblemFactory.ForbiddenProblem(),
                            options: null,
                            contentType: "application/problem+json",
                            context.HttpContext.RequestAborted);
                    }
                };
            });

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

    private static string? GetRawToken(TokenValidatedContext context)
    {
        const string bearerPrefix = "Bearer ";
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header[bearerPrefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }
}
