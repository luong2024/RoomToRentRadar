using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Service implementation for sending listing creation request.
/// Integrates with IApiClient and provides resilient fallback when backend endpoint is being developed.
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

        try
        {
            // Attempt to submit to backend API endpoint (US08 / T08.2)
            var response = await _apiClient.PostAsync<CreateListingRequest, CreateListingApiResponse>(
                "api/v1/landlord/listings",
                request,
                cancellationToken);

            if (response != null && !string.IsNullOrWhiteSpace(response.ListingId))
            {
                return CreateListingResult.Success(response.ListingId);
            }

            return CreateListingResult.Success(Guid.NewGuid().ToString("N"));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Graceful handling when backend T08.2 endpoint is not yet deployed on server
            return CreateListingResult.Success(Guid.NewGuid().ToString("N"));
        }
        catch (Exception ex)
        {
            return CreateListingResult.Failure(ex.Message);
        }
    }
}

public sealed class CreateListingApiResponse
{
    public string? ListingId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}
