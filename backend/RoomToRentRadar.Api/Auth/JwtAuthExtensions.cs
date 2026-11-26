using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Auth
{
    /// <summary>
    /// All JWT wiring for US01 / T01.1 in one place, so the locked Program.cs only needs
    /// <c>builder.AddJwtAuth();</c> (before Build) and <c>app.UseJwtAuth();</c> (after Build).
    /// After this, any endpoint can call <c>.RequireAuthorization()</c> and read the user id from the "sub" claim.
    /// </summary>
    public static class JwtAuthExtensions
    {
        public static WebApplicationBuilder AddJwtAuth(this WebApplicationBuilder builder)
        {
            // Settings from "Jwt" section; a missing/short SigningKey stops the API at startup.
            builder.Services
                .AddOptions<JwtOptions>()
                .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<JwtTokenService>();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            // Configure the bearer handler from the validated JwtOptions (same key as JwtTokenService).
            builder.Services
                .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
                {
                    var jwt = jwtOptions.Value;

                    // Keep claim names exactly as JwtTokenService writes them ("sub", "role").
                    bearer.MapInboundClaims = false;

                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.SigningKey),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1), // small tolerance for device clock drift
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        RoleClaimType = JwtTokenService.RoleClaim,
                    };

                    // Friendly Vietnamese JSON instead of an empty 401/403 body.
                    bearer.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            var message = context.AuthenticateFailure is SecurityTokenExpiredException
                                ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
                                : "Bạn cần đăng nhập để thực hiện thao tác này.";
                            await context.Response.WriteAsJsonAsync(new ErrorResponse(message));
                        },
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsJsonAsync(
                                new ErrorResponse("Tài khoản của bạn không có quyền thực hiện thao tác này."));
                        },
                    };
                });

            builder.Services.AddAuthorization();
            return builder;
        }

        /// <summary>Adds authentication + authorization middleware. Call after Build, before the Map...Endpoints calls.</summary>
        public static WebApplication UseJwtAuth(this WebApplication app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }
    }
}
