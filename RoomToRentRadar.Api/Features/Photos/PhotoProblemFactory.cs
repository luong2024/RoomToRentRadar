using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Photos;

public static class PhotoProblemFactory
{
    public const string ListingNotFound = "listing_not_found";
    public const string EmptyUpload = "empty_upload";
    public const string InvalidPhotoKind = "invalid_photo_kind";
    public const string InvalidPhoto = "invalid_photo";
    public const string PhotoTooLarge = "photo_too_large";
    public const string UnsupportedPhotoType = "unsupported_photo_type";
    public const string PhotoLimitExceeded = "photo_limit_exceeded";

    public static ProblemDetails ListingMissing(Guid listingId) => Create(
        ListingNotFound,
        StatusCodes.Status404NotFound,
        "Listing not found",
        $"Listing '{listingId}' does not exist.");

    public static ProblemDetails EmptyUploadProblem() => Create(
        EmptyUpload,
        StatusCodes.Status400BadRequest,
        "Empty photo upload",
        "At least one image file must be sent in the 'files' form field.");

    public static ProblemDetails TooManyFiles() => Create(
        InvalidPhoto,
        StatusCodes.Status400BadRequest,
        "Invalid photo upload",
        $"At most {PhotoUploadLimits.MaxFilesPerRequest} files can be uploaded per request.");

    public static ProblemDetails EmptyFile() => Create(
        InvalidPhoto,
        StatusCodes.Status400BadRequest,
        "Invalid photo upload",
        "Uploaded files must not be empty.");

    public static ProblemDetails PhotoInvalid() => Create(
        InvalidPhoto,
        StatusCodes.Status400BadRequest,
        "Invalid photo",
        "Every file must be a single-frame JPEG, PNG or WebP image within the pixel limit.");

    public static ProblemDetails KindInvalid() => Create(
        InvalidPhotoKind,
        StatusCodes.Status400BadRequest,
        "Invalid photo kind",
        "kind must be one of: photo, panorama.");

    public static ProblemDetails PhotoTooLargeProblem() => Create(
        PhotoTooLarge,
        StatusCodes.Status413PayloadTooLarge,
        "Photo too large",
        $"Every file must be {PhotoUploadLimits.MaxFileBytes / (1024 * 1024)} MB or smaller.");

    public static ProblemDetails UnsupportedType() => Create(
        UnsupportedPhotoType,
        StatusCodes.Status415UnsupportedMediaType,
        "Unsupported photo type",
        "Only image/jpeg, image/png and image/webp uploads are supported.");

    public static ProblemDetails PhotoLimitReached() => Create(
        PhotoLimitExceeded,
        StatusCodes.Status409Conflict,
        "Photo limit reached",
        $"A listing can have at most {PhotoUploadLimits.MaxPhotosPerListing} photos.");

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
