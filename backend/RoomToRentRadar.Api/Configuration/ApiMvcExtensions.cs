using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Configuration;

public static class ApiMvcExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
        services.AddControllers()
            .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = _ =>
            {
                var problem = ListingProblemFactory.InvalidQuery(
                    ListingProblemFactory.InvalidParameterFormat,
                    "One or more request parameters have an invalid format.");
                return new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            };
        });
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        return services;
    }
}
