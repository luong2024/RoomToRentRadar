
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Abstractions.HouseRules;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// API endpoints for versioned house rules (US12).
    /// </summary>
    public static class HouseRuleEndpoints
    {
        public static IEndpointRouteBuilder MapHouseRuleEndpoints(
            this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/house-rules");

            // GET: /api/house-rules/{roomId}
            // Retrieve the version history of a room's house rules.
            group.MapGet("/{roomId:int}", async (
                int roomId,
                IHouseRuleRepository repository) =>
            {
                if (roomId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        message = "Room ID must be greater than zero."
                    });
                }

                var history = await repository.GetHistoryByRoomAsync(roomId);

                if (history.Count == 0)
                {
                    return Results.NotFound(new
                    {
                        message = $"No house rules found for room {roomId}."
                    });
                }

                return Results.Ok(history);
            });

            // POST: /api/house-rules
            // Save a new version of house rules.
            group.MapPost("/", async (
                HouseRuleVersion ruleVersion,
                IHouseRuleRepository repository) =>
            {
                if (ruleVersion.RoomId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        message = "Room ID must be greater than zero."
                    });
                }

                if (string.IsNullOrWhiteSpace(ruleVersion.QuietHours) ||
                    string.IsNullOrWhiteSpace(ruleVersion.GuestPolicy) ||
                    string.IsNullOrWhiteSpace(ruleVersion.CookingRules) ||
                    string.IsNullOrWhiteSpace(ruleVersion.ParkingRules))
                {
                    return Results.BadRequest(new
                    {
                        message = "All house rule fields are required."
                    });
                }

                ruleVersion.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                var id = await repository.SaveNewVersionAsync(ruleVersion);

                return Results.Created(
                    $"/api/house-rules/{ruleVersion.RoomId}",
                    new
                    {
                        id,
                        roomId = ruleVersion.RoomId,
                        versionNumber = ruleVersion.VersionNumber,
                        updatedAt = ruleVersion.UpdatedAt
                    });
            });

            return app;
        }
    }
}
