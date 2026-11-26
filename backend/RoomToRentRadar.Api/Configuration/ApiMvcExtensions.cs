using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Configuration;

public static class ApiMvcExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
        services.AddControllers(options =>
            {
                var formIndex = options.ValueProviderFactories.IndexOf(
                    options.ValueProviderFactories.OfType<FormValueProviderFactory>().First());
                options.ValueProviderFactories.RemoveType<FormValueProviderFactory>();
                options.ValueProviderFactories.RemoveType<JQueryFormValueProviderFactory>();
                options.ValueProviderFactories.Insert(formIndex, new InvariantFormValueProviderFactory());
            })
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

    private sealed class InvariantFormValueProviderFactory : IValueProviderFactory
    {
        public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
        {
            var httpContext = context.ActionContext.HttpContext;
            if (!httpContext.Request.HasFormContentType)
            {
                return;
            }

            IFormCollection form;
            try
            {
                form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
            }
            catch (InvalidDataException ex)
            {
                if (ex.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadHttpRequestException(
                        "Request body too large.",
                        StatusCodes.Status413PayloadTooLarge,
                        ex);
                }

                throw new ValueProviderException($"Failed to read the request form. {ex.Message}", ex);
            }
            catch (IOException ex) when (ex is not BadHttpRequestException)
            {
                throw new ValueProviderException($"Failed to read the request form. {ex.Message}", ex);
            }

            context.ValueProviders.Add(new FormValueProvider(
                BindingSource.Form,
                form,
                CultureInfo.InvariantCulture));
        }
    }
}
