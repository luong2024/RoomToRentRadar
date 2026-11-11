using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Photos;

public static class PhotoUploadValidator
{
    public static bool TryValidate(
        IReadOnlyList<IFormFile>? files,
        string? kind,
        out ListingPhotoKind photoKind,
        out ProblemDetails? problem)
    {
        photoKind = ListingPhotoKind.Photo;
        problem = null;

        if (files is null || files.Count == 0)
        {
            problem = PhotoProblemFactory.EmptyUploadProblem();
            return false;
        }

        if (files.Count > PhotoUploadLimits.MaxFilesPerRequest)
        {
            problem = PhotoProblemFactory.TooManyFiles();
            return false;
        }

        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                problem = PhotoProblemFactory.EmptyFile();
                return false;
            }

            if (file.Length > PhotoUploadLimits.MaxFileBytes)
            {
                problem = PhotoProblemFactory.PhotoTooLargeProblem();
                return false;
            }

            if (!IsAllowedContentType(file.ContentType))
            {
                problem = PhotoProblemFactory.UnsupportedType();
                return false;
            }
        }

        if (!TryParseKind(kind, out photoKind))
        {
            problem = PhotoProblemFactory.KindInvalid();
            return false;
        }

        return true;
    }

    public static bool TryParseKind(string? value, out ListingPhotoKind kind)
    {
        kind = ListingPhotoKind.Photo;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "photo", StringComparison.OrdinalIgnoreCase))
        {
            kind = ListingPhotoKind.Photo;
            return true;
        }

        if (string.Equals(trimmed, "panorama", StringComparison.OrdinalIgnoreCase))
        {
            kind = ListingPhotoKind.Panorama;
            return true;
        }

        return false;
    }

    private static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';');
        var mediaType = separator >= 0
            ? contentType[..separator].Trim()
            : contentType.Trim();

        return PhotoUploadLimits.AllowedContentTypes.Contains(mediaType);
    }
}
