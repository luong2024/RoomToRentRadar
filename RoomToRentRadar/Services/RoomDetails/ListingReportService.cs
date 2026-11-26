using System.Net;
using System.Text.Json.Serialization;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.RoomDetails;

namespace RoomToRentRadar.Services.RoomDetails;

public sealed class ListingReportService : IListingReportService
{
    private readonly IApiClient _apiClient;

    public ListingReportService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<SubmitListingReportResult> SubmitReportAsync(
        string roomId,
        ReportReasonCode reason,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return SubmitListingReportResult.Failure(
                "Room information is missing.",
                isValidationError: true);
        }

        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var payload = new SubmitListingReportApiRequest((int)reason, normalizedNote);

        try
        {
            var response = await _apiClient.PostAsync<SubmitListingReportApiRequest, SubmitListingReportApiResponse>(
                $"api/v1/rooms/{Uri.EscapeDataString(roomId.Trim())}/reports",
                payload,
                cancellationToken);

            if (response is null || string.IsNullOrWhiteSpace(response.ReportId))
            {
                return SubmitListingReportResult.Failure("Unable to confirm your report submission. Please try again.");
            }

            return SubmitListingReportResult.Success(
                response.ReportId,
                response.Status ?? "Pending",
                response.CreatedAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiValidationException)
        {
            return SubmitListingReportResult.Failure(
                "Please review your report reason and note, then try again.",
                isValidationError: true);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            return status switch
            {
                HttpStatusCode.BadRequest => SubmitListingReportResult.Failure(
                    "Please review your report reason and note, then try again.",
                    isValidationError: true),
                HttpStatusCode.Unauthorized => SubmitListingReportResult.Failure(
                    "Please sign in again to report this listing.",
                    isUnauthorized: true),
                HttpStatusCode.NotFound => SubmitListingReportResult.Failure(
                    "This listing could not be found."),
                HttpStatusCode.Conflict => SubmitListingReportResult.Failure(
                    "You have already reported this listing.",
                    isDuplicate: true),
                HttpStatusCode.TooManyRequests => SubmitListingReportResult.Failure(
                    "Too many reports submitted. Please try again later.",
                    isRateLimited: true),
                _ => SubmitListingReportResult.Failure(
                    $"Could not submit report ({(int)status}). Please try again.")
            };
        }
        catch (Exception)
        {
            return SubmitListingReportResult.Failure("Unable to submit report right now. Please try again.");
        }
    }

    private sealed record SubmitListingReportApiRequest(
        [property: JsonPropertyName("reason")] int Reason,
        [property: JsonPropertyName("note")] string? Note);

    private sealed class SubmitListingReportApiResponse
    {
        [JsonPropertyName("reportId")]
        public string? ReportId { get; set; }

        [JsonPropertyName("roomId")]
        public string? RoomId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
