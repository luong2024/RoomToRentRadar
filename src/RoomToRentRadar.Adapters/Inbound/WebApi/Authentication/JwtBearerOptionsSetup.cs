using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Application.Ports.Inbound;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Authentication;

/// <summary>
/// Configures JWT Bearer validation parameters, security stamp / active account verification,
/// and RFC 7807 ProblemDetails 401/403 responses.
/// </summary>
public sealed class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptions<JwtOptions> _jwtOptions;

    public JwtBearerOptionsSetup(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.IsNullOrEmpty(name) &&
            !string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var jwt = _jwtOptions.Value;

        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    context.Fail("Missing principal in validated token.");
                    return;
                }

                var subValue = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrWhiteSpace(subValue) || !Guid.TryParse(subValue, out var userId) || userId == Guid.Empty)
                {
                    context.Fail("Token is missing a valid user identifier.");
                    return;
                }

                var securityStamp = principal.FindFirst(AuthClaimTypes.SecurityStamp)?.Value;
                if (string.IsNullOrWhiteSpace(securityStamp))
                {
                    context.Fail("Token is missing the required security stamp.");
                    return;
                }

                var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
                    ?? principal.FindFirst(AuthClaimTypes.Role)?.Value;

                var accountValidator = context.HttpContext.RequestServices
                    .GetRequiredService<IAccountSecurityValidator>();

                var isValid = await accountValidator.ValidateTokenSecurityStateAsync(
                    userId,
                    securityStamp,
                    roleClaim,
                    context.HttpContext.RequestAborted);

                if (!isValid)
                {
                    context.Fail("User account is inactive or token security state has been invalidated.");
                }
            },

            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "A valid, active authentication token is required to access this resource.",
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsJsonAsync(problem, context.HttpContext.RequestAborted);
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "You do not have permission to access this resource.",
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsJsonAsync(problem, context.HttpContext.RequestAborted);
            }
        };
    }
}
