using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Infrastructure.Api;

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
                    var photo = request.Photos[i];
                    var stream = await photo.OpenReadStreamAsync();
                    if (stream == null || !stream.CanRead || (stream.CanSeek && stream.Length == 0))
                    {
                        throw new InvalidOperationException($"Ảnh '{photo.FileName}' không có nội dung dữ liệu hợp lệ.");
                    }

                    var streamContent = new StreamContent(stream);
                    var contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                    var fileName = string.IsNullOrWhiteSpace(photo.FileName) ? $"photo_{i}.jpg" : photo.FileName;
                    multipartContent.Add(streamContent, "photos", fileName);
                }
            }

            var response = await _apiClient.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                multipartContent,
                cancellationToken);

            var listingId = response?.ResolvedId;
            if (!string.IsNullOrWhiteSpace(listingId))
            {
                return CreateListingResult.Success(listingId);
            }

            return CreateListingResult.Failure(response?.Message ?? "Không nhận được mã tin đăng hợp lệ từ máy chủ.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return CreateListingResult.Failure("Dịch vụ tạo tin đăng tạm thời không khả dụng (404 Not Found). Vui lòng thử lại sau.");
        }
        catch (HttpRequestException ex)
        {
            var statusCode = ex.StatusCode.HasValue ? $" ({(int)ex.StatusCode.Value})" : string.Empty;
            return CreateListingResult.Failure($"Lỗi máy chủ{statusCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CreateListingResult.Failure(ex.Message);
        }
    }
}

public sealed class CreateListingApiResponse
{
    [JsonPropertyName("listingId")]
    public string? ListingId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    public string? Status { get; set; }
    public string? Message { get; set; }

    [JsonIgnore]
    public string? ResolvedId => !string.IsNullOrWhiteSpace(ListingId) ? ListingId : Id;
}
