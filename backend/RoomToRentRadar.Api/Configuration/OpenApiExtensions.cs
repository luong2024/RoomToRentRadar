using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;

namespace RoomToRentRadar.Api.Configuration;

public static class OpenApiExtensions
{
    public static IServiceCollection AddListingsOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, _) =>
            {
                var request = context.ApplicationServices
                    .GetRequiredService<IHttpContextAccessor>()
                    .HttpContext?.Request;

                if (request is not null)
                {
                    document.Servers = new List<OpenApiServer>
                    {
                        new() { Url = $"{request.Scheme}://{request.Host}{request.PathBase}" }
                    };
                }

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Send the JWT access token returned by POST /api/v1/auth/login."
                    }
                };

                foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
                {
                    if (!RequiresAuthorization(description)
                        || string.IsNullOrEmpty(description.RelativePath)
                        || description.HttpMethod is null)
                    {
                        continue;
                    }

                    var path = "/" + description.RelativePath.TrimStart('/');
                    document.Paths.TryGetValue(path, out var pathItem);
                    if (pathItem is null)
                    {
                        continue;
                    }

                    var operations = pathItem.Operations;
                    if (operations is null)
                    {
                        continue;
                    }

                    operations.TryGetValue(new HttpMethod(description.HttpMethod), out var operation);
                    if (operation is null)
                    {
                        continue;
                    }

                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static bool RequiresAuthorization(ApiDescription description)
    {
        if (description.ActionDescriptor is not ControllerActionDescriptor action)
        {
            return false;
        }

        return action.MethodInfo.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()
            || action.ControllerTypeInfo.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any();
    }
}
