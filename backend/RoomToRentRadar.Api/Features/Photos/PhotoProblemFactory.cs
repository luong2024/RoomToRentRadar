using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Common;
using RoomToRentRadar.Ports.Models.Photos;

namespace RoomToRentRadar.Api.Features.Photos;

public static class PhotoProblemFactory
{
    public const string ListingNotFound = PhotoErrorCodes.ListingNotFound;
    public const string ListingUnavailable = PhotoErrorCodes.ListingUnavailable;
    public const string EmptyUpload = "empty_upload";
    public const string InvalidPhotoKind = "invalid_photo_kind";
    public const string InvalidPhoto = PhotoErrorCodes.InvalidPhoto;
    public const string PhotoTooLarge = "photo_too_large";
    public const string UnsupportedPhotoType = "unsupported_photo_type";
    public const string PhotoLimitExceeded = PhotoErrorCodes.PhotoLimitExceeded;

    public static ProblemDetails ListingMissing(Guid listingId) => Create(
        ListingNotFound,
        StatusCodes.Status404NotFound,
        "Listing not found",
        $"Listing '{listingId}' does not exist.");

    public static ProblemDetails ListingUnavailableProblem() => Create(
        ListingUnavailable,
        StatusCodes.Status410Gone,
        "Listing unavailable",
        "The listing is not available.");

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
        "Every file must be a single-frame JPEG or PNG image within the pixel limit.");

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
        "Only image/jpeg and image/png uploads are supported.");

    public static ProblemDetails PhotoLimitReached() => Create(
        PhotoLimitExceeded,
        StatusCodes.Status409Conflict,
        "Photo limit reached",
        $"A listing can have at most {PhotoUploadLimits.MaxPhotosPerListing} photos.");

    public static ProblemDetails FromError(ApplicationError error) => error.Code switch
    {
        PhotoErrorCodes.ListingNotFound => Create(
            PhotoErrorCodes.ListingNotFound,
            StatusCodes.Status404NotFound,
            "Listing not found",
            error.Message),
        PhotoErrorCodes.ListingUnavailable => Create(
            PhotoErrorCodes.ListingUnavailable,
            StatusCodes.Status410Gone,
            "Listing unavailable",
            error.Message),
        PhotoErrorCodes.InvalidPhoto => Create(
            PhotoErrorCodes.InvalidPhoto,
            StatusCodes.Status400BadRequest,
            "Invalid photo",
            error.Message),
        PhotoErrorCodes.PhotoLimitExceeded => Create(
            PhotoErrorCodes.PhotoLimitExceeded,
            StatusCodes.Status409Conflict,
            "Photo limit reached",
            error.Message),
        _ => throw new InvalidOperationException($"Unmapped photo error code '{error.Code}'.")
    };

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
        StatusCodes.Status410Gone => "https://tools.ietf.org/html/rfc9110#section-15.5.11",
        StatusCodes.Status413PayloadTooLarge => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
        StatusCodes.Status415UnsupportedMediaType => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        _ => "about:blank"
    };
}
