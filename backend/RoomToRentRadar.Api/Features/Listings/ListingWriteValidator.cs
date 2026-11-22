using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Models.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingWriteValidator
{
    private static readonly IReadOnlySet<string> SupportedPhotoContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };

    public static bool TryValidate(
        ListingWriteForm form,
        int minimumPhotos,
        out ListingWriteInput input,
        out ProblemDetails? problem)
        => TryValidate(form, minimumPhotos, requireCoordinates: true, out input, out problem);

    public static bool TryValidate(
        ListingWriteForm form,
        int minimumPhotos,
        bool requireCoordinates,
        out ListingWriteInput input,
        out ProblemDetails? problem)
    {
        input = null!;
        problem = null;

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var title = RequireText(form.Title, "title", ListingLimits.MaximumTitleLength, errors);
        var address = RequireText(form.Address, "address", ListingLimits.MaximumAddressLength, errors);
        var description = OptionalText(
            form.Description,
            "description",
            ListingLimits.MaximumDescriptionLength,
            errors);
        var landlordName = OptionalText(
            form.LandlordName,
            "landlord_name",
            ListingLimits.MaximumLandlordNameLength,
            errors);
        var landlordPhone = OptionalText(
            form.LandlordPhone,
            "landlord_phone",
            ListingLimits.MaximumLandlordPhoneLength,
            errors);

        var price = form.Price.GetValueOrDefault();
        if (form.Price is null)
        {
            AddError(errors, "price", "price is required.");
        }
        else if (price <= 0)
        {
            AddError(errors, "price", "price must be greater than 0.");
        }

        var deposit = form.Deposit.GetValueOrDefault();
        if (form.Deposit is null)
        {
            AddError(errors, "deposit", "deposit is required.");
        }
        else if (deposit < 0)
        {
            AddError(errors, "deposit", "deposit must be zero or greater.");
        }

        var areaM2 = form.AreaM2.GetValueOrDefault();
        if (form.AreaM2 is null)
        {
            AddError(errors, "area_m2", "area_m2 is required.");
        }
        else if (!double.IsFinite(areaM2) || areaM2 <= 0)
        {
            AddError(errors, "area_m2", "area_m2 must be greater than 0.");
        }

        var roomType = RoomType.SingleRoom;
        if (string.IsNullOrWhiteSpace(form.RoomType))
        {
            AddError(errors, "room_type", "room_type is required.");
        }
        else if (!RoomTypeWire.TryParse(form.RoomType.Trim(), out roomType))
        {
            AddError(
                errors,
                "room_type",
                $"room_type must be one of: {string.Join(", ", RoomTypeWire.All)}.");
        }

        if (form.Bedrooms is < 0)
        {
            AddError(errors, "bedrooms", "bedrooms must be zero or greater.");
        }

        if (form.Lat is not { } lat)
        {
            if (requireCoordinates)
            {
                AddError(errors, "lat", "lat is required.");
            }
        }
        else if (!double.IsFinite(lat) || lat is < -90d or > 90d)
        {
            AddError(errors, "lat", "lat must be within [-90, 90].");
        }

        if (form.Lng is not { } lng)
        {
            if (requireCoordinates)
            {
                AddError(errors, "lng", "lng is required.");
            }
        }
        else if (!double.IsFinite(lng) || lng is < -180d or > 180d)
        {
            AddError(errors, "lng", "lng must be within [-180, 180].");
        }

        var photos = form.Photos ?? [];
        if (photos.Count > PhotoUploadLimits.MaxFilesPerRequest)
        {
            AddError(
                errors,
                "photos",
                $"At most {PhotoUploadLimits.MaxFilesPerRequest} photos can be uploaded per request.");
        }

        if (errors.Count > 0)
        {
            problem = ListingWriteProblemFactory.InvalidListingProblem(errors);
            return false;
        }

        if (photos.Count < minimumPhotos)
        {
            problem = ListingWriteProblemFactory.TooFewPhotosProblem(minimumPhotos);
            return false;
        }

        foreach (var file in photos)
        {
            if (file.Length <= 0)
            {
                problem = ListingWriteProblemFactory.InvalidPhotoProblem();
                return false;
            }

            if (file.Length > PhotoUploadLimits.MaxFileBytes)
            {
                problem = ListingWriteProblemFactory.PhotoTooLargeProblem();
                return false;
            }

            if (!IsSupportedPhotoType(file.ContentType))
            {
                problem = ListingWriteProblemFactory.UnsupportedPhotoTypeProblem();
                return false;
            }
        }

        input = new ListingWriteInput(
            title!,
            address!,
            price,
            deposit,
            areaM2,
            roomType,
            description,
            form.Bedrooms,
            form.Lat,
            form.Lng,
            landlordName,
            landlordPhone,
            photos);
        return true;
    }

    private static string? RequireText(
        string? value,
        string field,
        int maximumLength,
        Dictionary<string, List<string>> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            AddError(errors, field, $"{field} is required.");
            return null;
        }

        if (trimmed.Length > maximumLength)
        {
            AddError(errors, field, $"{field} must contain at most {maximumLength} characters.");
            return null;
        }

        return trimmed;
    }

    private static string? OptionalText(
        string? value,
        string field,
        int maximumLength,
        Dictionary<string, List<string>> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maximumLength)
        {
            AddError(errors, field, $"{field} must contain at most {maximumLength} characters.");
            return null;
        }

        return trimmed;
    }

    private static bool IsSupportedPhotoType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';');
        var mediaType = separator >= 0
            ? contentType[..separator].Trim()
            : contentType.Trim();

        return SupportedPhotoContentTypes.Contains(mediaType);
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}
