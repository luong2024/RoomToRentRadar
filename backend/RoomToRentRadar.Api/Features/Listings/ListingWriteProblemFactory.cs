using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Photos;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingWriteProblemFactory
{
    public const string InvalidListing = "invalid_listing";
    public const string TooFewPhotos = "too_few_photos";
    public const string InvalidPhoto = "invalid_listing_photo";
    public const string PhotoTooLarge = "photo_too_large";
    public const string UnsupportedPhotoType = "unsupported_photo_type";
    public const string PhotoLimitExceeded = "photo_limit_exceeded";
    public const string ListingNotFound = "listing_not_found";
    public const string InvalidListingTransition = "invalid_listing_transition";
    public const string InvalidHouseRules = "invalid_house_rules";

    public static ProblemDetails InvalidListingProblem(IReadOnlyDictionary<string, List<string>> errors)
    {
        var problem = Create(
            InvalidListing,
            StatusCodes.Status400BadRequest,
            "Invalid listing",
            "One or more listing fields are invalid.");
        problem.Extensions["errors"] = errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
        return problem;
    }

    public static ProblemDetails InvalidHouseRulesProblem(IReadOnlyDictionary<string, List<string>> errors)
    {
        var problem = Create(
            InvalidHouseRules,
            StatusCodes.Status400BadRequest,
            "Invalid house rules",
            "One or more house rule fields are invalid.");
        problem.Extensions["errors"] = errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
        return problem;
    }

    public static ProblemDetails TooFewPhotosProblem(int minimumPhotos) => Create(
        TooFewPhotos,
        StatusCodes.Status400BadRequest,
        "Not enough photos",
        $"At least {minimumPhotos} photos are required.");

    public static ProblemDetails InvalidPhotoProblem() => Create(
        InvalidPhoto,
        StatusCodes.Status400BadRequest,
        "Invalid photo",
        "Every photo must be a single-frame JPEG or PNG image within the pixel limit.");

    public static ProblemDetails PhotoTooLargeProblem() => Create(
        PhotoTooLarge,
        StatusCodes.Status413PayloadTooLarge,
        "Photo too large",
        $"Every photo must be {PhotoUploadLimits.MaxFileBytes / (1024 * 1024)} MB or smaller.");

    public static ProblemDetails UnsupportedPhotoTypeProblem() => Create(
        UnsupportedPhotoType,
        StatusCodes.Status415UnsupportedMediaType,
        "Unsupported photo type",
        "Only image/jpeg and image/png uploads are supported.");

    public static ProblemDetails PhotoLimitExceededProblem() => Create(
        PhotoLimitExceeded,
        StatusCodes.Status409Conflict,
        "Photo limit reached",
        $"A listing can have at most {PhotoUploadLimits.MaxPhotosPerListing} photos.");

    public static ProblemDetails NotFoundProblem() => Create(
        ListingNotFound,
        StatusCodes.Status404NotFound,
        "Listing not found",
        "No listing exists with the requested identifier for the current user.");

    public static ProblemDetails InvalidListingTransitionProblem() => Create(
        InvalidListingTransition,
        StatusCodes.Status409Conflict,
        "Invalid listing transition",
        "A removed listing cannot be modified.");

    private static ProblemDetails Create(string code, int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = TypeForStatus(status),
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static string TypeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status413PayloadTooLarge => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
        StatusCodes.Status415UnsupportedMediaType => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        _ => "about:blank"
    };
}
