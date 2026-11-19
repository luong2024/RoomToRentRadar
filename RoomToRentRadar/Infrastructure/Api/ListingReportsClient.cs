using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Shared.Reports;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ListingReportsClient(HttpClient httpClient) : IListingReportsClient
{
    public async Task<ListingReportSubmitResult> CreateReportAsync(
        Guid listingId,
        ListingReportReason reason,
        string? details,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/v1/listings/{listingId}/reports",
            new CreateListingReportRequest
            {
                Reason = ListingReportReasonWire.ToWireValue(reason),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim()
            },
            ApiJson.Options,
            cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Created:
                var created = await response.Content.ReadFromJsonAsync<CreateListingReportResponse>(
                    ApiJson.Options,
                    cancellationToken);
                return created is null
                    ? throw new InvalidOperationException("The report endpoint returned an empty response.")
                    : new ListingReportSubmitResult(ListingReportSubmitOutcome.Created, created.Id);

            case HttpStatusCode.Unauthorized:
                return new ListingReportSubmitResult(ListingReportSubmitOutcome.Unauthorized);

            case HttpStatusCode.NotFound:
                return new ListingReportSubmitResult(ListingReportSubmitOutcome.ListingNotFound);

            case HttpStatusCode.Conflict:
                return new ListingReportSubmitResult(ListingReportSubmitOutcome.DuplicateOpenReport);

            case HttpStatusCode.UnprocessableEntity:
                return new ListingReportSubmitResult(ListingReportSubmitOutcome.OwnListing);

            case HttpStatusCode.TooManyRequests:
                return new ListingReportSubmitResult(
                    ListingReportSubmitOutcome.RateLimited,
                    RetryAfter: await ReadRetryAfterAsync(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        // Success statuses other than 201 are not part of the endpoint contract.
        throw new InvalidOperationException(
            $"The report endpoint returned unexpected status {(int)response.StatusCode}.");
    }

    private static async Task<TimeSpan> ReadRetryAfterAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("retry_after_seconds", out var seconds)
                && seconds.TryGetInt32(out var value)
                && value > 0)
            {
                return TimeSpan.FromSeconds(value);
            }
        }
        catch (JsonException)
        {
        }

        return TimeSpan.FromMinutes(1);
    }
}
