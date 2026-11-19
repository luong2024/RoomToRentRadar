namespace RoomToRentRadar.Application.Validators;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

public static class SearchRoomsInPolygonQueryValidator
{
    public const int MinPageSize = 1;
    public const int MaxPageSize = 50;

    public static void Validate(SpatialRoomSearchRequestDto? request)
    {
        if (request is null)
        {
            throw new ValidationException(nameof(request), "Search request cannot be null.");
        }

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddError(string property, string message)
        {
            if (!errors.TryGetValue(property, out var list))
            {
                list = new List<string>();
                errors[property] = list;
            }
            list.Add(message);
        }

        // 1. Polygon coordinates validation
        if (request.PolygonCoordinates is null || request.PolygonCoordinates.Count == 0)
        {
            AddError(nameof(request.PolygonCoordinates), "Polygon coordinates are required.");
        }
        else
        {
            if (request.PolygonCoordinates.Count < 3)
            {
                AddError(nameof(request.PolygonCoordinates), "A polygon must contain at least 3 vertices.");
            }

            for (int i = 0; i < request.PolygonCoordinates.Count; i++)
            {
                var point = request.PolygonCoordinates[i];
                if (!double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude))
                {
                    AddError($"{nameof(request.PolygonCoordinates)}[{i}]", "Coordinates must be finite real numbers.");
                }
                else
                {
                    if (point.Latitude < GeoPolygon.MinLatitude || point.Latitude > GeoPolygon.MaxLatitude)
                    {
                        AddError($"{nameof(request.PolygonCoordinates)}[{i}].Latitude",
                            $"Latitude must be between {GeoPolygon.MinLatitude} and {GeoPolygon.MaxLatitude}.");
                    }

                    if (point.Longitude < GeoPolygon.MinLongitude || point.Longitude > GeoPolygon.MaxLongitude)
                    {
                        AddError($"{nameof(request.PolygonCoordinates)}[{i}].Longitude",
                            $"Longitude must be between {GeoPolygon.MinLongitude} and {GeoPolygon.MaxLongitude}.");
                    }
                }
            }
        }

        // 2. Price filter validation
        if (request.MinPrice.HasValue && request.MinPrice.Value < 0)
        {
            AddError(nameof(request.MinPrice), "Minimum rent price cannot be negative.");
        }

        if (request.MaxPrice.HasValue && request.MaxPrice.Value < 0)
        {
            AddError(nameof(request.MaxPrice), "Maximum rent price cannot be negative.");
        }

        if (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice.Value > request.MaxPrice.Value)
        {
            AddError(nameof(request.MinPrice), "Minimum rent price cannot exceed maximum rent price.");
        }

        // 3. Pagination validation
        if (request.PageIndex < 1)
        {
            AddError(nameof(request.PageIndex), "PageIndex must be greater than or equal to 1.");
        }

        if (request.PageSize < MinPageSize || request.PageSize > MaxPageSize)
        {
            AddError(nameof(request.PageSize), $"PageSize must be between {MinPageSize} and {MaxPageSize}.");
        }

        if (errors.Count > 0)
        {
            var dict = errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
            throw new ValidationException(dict);
        }
    }
}
