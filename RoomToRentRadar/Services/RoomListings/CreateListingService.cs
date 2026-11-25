using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Services.RoomListings;

/// <summary>
/// Service implementation for sending listing creation request.
/// Integrates with IApiClient and uploads listing form data and photo streams via multipart/form-data.
/// </summary>
public class CreateListingService : ICreateListingService
{
    private readonly IApiClient _apiClient;

    public CreateListingService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<CreateListingResult> CreateListingAsync(CreateListingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var location = request.Location;
        if (location is not null && !ListingLocation.IsValid(location))
        {
            return InvalidLocation();
        }

        var requestStarted = false;
        try
        {
            using var multipartContent = new MultipartFormDataContent();

            multipartContent.Add(new StringContent(request.Title), "title");
            multipartContent.Add(new StringContent(request.Address), "address");
            multipartContent.Add(new StringContent(request.MonthlyRentVnd.ToString(CultureInfo.InvariantCulture)), "monthlyRentVnd");

            if (request.DepositVnd > 0)
            {
                multipartContent.Add(new StringContent(request.DepositVnd.ToString(CultureInfo.InvariantCulture)), "depositVnd");
            }

            multipartContent.Add(new StringContent(request.AreaM2.ToString(CultureInfo.InvariantCulture)), "areaM2");
            multipartContent.Add(new StringContent(request.RoomType), "roomType");

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                multipartContent.Add(new StringContent(request.Description), "description");
            }

            if (location is not null)
            {
                multipartContent.Add(new StringContent(location.Latitude.ToString("R", CultureInfo.InvariantCulture)), "latitude");
                multipartContent.Add(new StringContent(location.Longitude.ToString("R", CultureInfo.InvariantCulture)), "longitude");
            }

            if (request.PhotoUrls != null)
            {
                foreach (var url in request.PhotoUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        multipartContent.Add(new StringContent(url), "photoUrls");
                    }
                }
            }

            if (request.Photos != null)
            {
                for (int i = 0; i < request.Photos.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var photo = request.Photos[i];
                    var stream = await photo.OpenReadStreamAsync();
                    if (stream.CanSeek && stream.Length == 0)
                    {
                        stream.Dispose();
                        return Failure("ListingSubmit_InvalidPhoto");
                    }

                    var contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
                    var fileName = string.IsNullOrWhiteSpace(photo.FileName) ? $"photo_{i}.jpg" : photo.FileName;
                    var streamContent = new StreamContent(stream);
                    try
                    {
                        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                        multipartContent.Add(streamContent, "photos", fileName);
                    }
                    catch
                    {
                        streamContent.Dispose();
                        throw;
                    }
                }
            }

            requestStarted = true;
            var response = await _apiClient.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                multipartContent,
                cancellationToken);

            if (response is null || response.ResolvedId is not { } listingId || string.IsNullOrWhiteSpace(listingId))
            {
                return Failure("ListingSubmit_Unconfirmed");
            }

            var persisted = response.Latitude is { } latitude && response.Longitude is { } longitude
                ? new GeoCoordinate(latitude, longitude)
                : null;

            if (ListingLocation.IsValid(persisted))
            {
                return CreateListingResult.Success(listingId) with { PersistedLocation = persisted };
            }

            return CreateListingResult.Success(listingId) with
            {
                HasLocationResponseWarning = location is not null || response.Latitude.HasValue || response.Longitude.HasValue
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiValidationException ex) when (ex.Fields.Any(IsLocationField))
        {
            return InvalidLocation();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            return status switch
            {
                HttpStatusCode.BadRequest => Failure("ListingSubmit_InvalidForm"),
                HttpStatusCode.Unauthorized => Failure("ListingSubmit_SignIn"),
                HttpStatusCode.Forbidden => Failure("ListingSubmit_Forbidden"),
                HttpStatusCode.NotFound => Failure("ListingSubmit_Unavailable"),
                _ when (int)status >= 500 => CreateListingResult.Failure(string.Format(
                    CultureInfo.CurrentCulture, AppResources.GetString("ListingSubmit_ServerUnconfirmed"), (int)status)),
                _ => CreateListingResult.Failure(string.Format(
                    CultureInfo.CurrentCulture, AppResources.GetString("ListingSubmit_HttpError"), (int)status))
            };
        }
        catch (FileNotFoundException) when (!requestStarted)
        {
            return Failure("ListingSubmit_MissingPhoto");
        }
        catch (Exception)
        {
            return Failure(requestStarted ? "ListingSubmit_Unconfirmed" : "ListingSubmit_InvalidPhoto");
        }
    }

    private static bool IsLocationField(string field) =>
        string.Equals(field, "latitude", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(field, "longitude", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(field, "coordinates", StringComparison.OrdinalIgnoreCase);

    private static CreateListingResult InvalidLocation() =>
        Failure("ListingSubmit_InvalidLocation") with { HasLocationError = true };

    private static CreateListingResult Failure(string resource) =>
        CreateListingResult.Failure(AppResources.GetString(resource));
}

public sealed class CreateListingApiResponse
{
    [JsonPropertyName("listingId")]
    public string? ListingId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }

    [JsonIgnore]
    public string? ResolvedId => !string.IsNullOrWhiteSpace(ListingId) ? ListingId : Id;
}
