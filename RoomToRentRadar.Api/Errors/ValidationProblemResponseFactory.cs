using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Errors;

// ADR-012 / system-architecture section 7.2: framework-generated 400s (model binding) must use the same
// problem+json shape as domain validation errors, with a stable errorCode and an `errors` map.
public static class ValidationProblemResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        // ADR-012 / system-architecture 7.2: errors{} keys are camelCase to match the JSON the client sends and receives.
        var mergedErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in context.ModelState)
        {
            if (entry.Value is not { Errors.Count: > 0 })
            {
                continue;
            }

            var key = ToCamelCasePath(entry.Key);
            var messages = entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Invalid value."
                    : error.ErrorMessage)
                .ToList();

            if (mergedErrors.TryGetValue(key, out var existingMessages))
            {
                existingMessages.AddRange(messages);
            }
            else
            {
                mergedErrors.Add(key, messages);
            }
        }

        var errors = mergedErrors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = "VALIDATION_FAILED";

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string ToCamelCasePath(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key ?? string.Empty;
        }

        var segments = key.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var bracketIndex = segment.IndexOf('[');
            var namePart = bracketIndex < 0 ? segment : segment[..bracketIndex];
            var suffix = bracketIndex < 0 ? "" : segment[bracketIndex..];
            if (namePart.Length > 0 && namePart != "$")
            {
                namePart = JsonNamingPolicy.CamelCase.ConvertName(namePart);
            }

            segments[i] = namePart + suffix;
        }

        return string.Join(".", segments);
    }
}