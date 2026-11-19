using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ListingManagementClient(HttpClient httpClient) : IListingManagementClient
{
    public async Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        // Load all pages, including hidden and pending listings unavailable in public search.
        var listings = new List<ManagedListingDto>();
        const int pageSize = 100;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/v1/listings/mine?limit={pageSize}&offset={listings.Count}");
            var page = await SendAsync<List<ManagedListingDto>>(request, cancellationToken);
            listings.AddRange(page);
            if (page.Count < pageSize)
                return listings;
        }
    }

    public async Task<ManagedListingDto> GetManagedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/listings/{id}/manage");
        return await SendAsync<ManagedListingDto>(request, cancellationToken);
    }

    public Task<ManagedListingDto> CreateAsync(ListingFormSubmission form, CancellationToken cancellationToken = default)
        => WriteAsync(HttpMethod.Post, "api/v1/listings", form, cancellationToken);

    public Task<ManagedListingDto> UpdateAsync(Guid id, ListingFormSubmission form, CancellationToken cancellationToken = default)
        => WriteAsync(HttpMethod.Put, $"api/v1/listings/{id}", form, cancellationToken);

    public async Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/listings/{id}/unpublish");
        return await SendAsync<ManagedListingDto>(request, cancellationToken);
    }

    private async Task<ManagedListingDto> WriteAsync(
        HttpMethod method, string path, ListingFormSubmission form, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        Add("title", form.Title.Trim());
        Add("address", form.Address.Trim());
        Add("price", form.Price.ToString(CultureInfo.InvariantCulture));
        Add("deposit", form.Deposit.ToString(CultureInfo.InvariantCulture));
        Add("area_m2", form.AreaM2.ToString("R", CultureInfo.InvariantCulture));
        Add("room_type", RoomTypeWire.ToWireValue(form.RoomType));
        Add("lat", form.Lat.ToString("R", CultureInfo.InvariantCulture));
        Add("lng", form.Lng.ToString("R", CultureInfo.InvariantCulture));
        if (form.Description is not null)
            Add("description", form.Description.Trim());

        foreach (var photo in form.Photos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = await photo.OpenReadAsync();
            var fileContent = new StreamContent(stream);
            // Attach immediately so the multipart owner disposes the stream on any failure.
            content.Add(fileContent, "photos", Path.GetFileName(photo.FileName));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType);
        }

        using var request = new HttpRequestMessage(method, path) { Content = content };
        return await SendAsync<ManagedListingDto>(request, cancellationToken);

        void Add(string name, string value) => content.Add(new StringContent(value), name);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string? detail = null, code = null;
            var errors = new Dictionary<string, string[]>();
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    detail = ReadString(root, "detail") ?? ReadString(root, "title");
                    code = ReadString(root, "code");
                    if (root.TryGetProperty("errors", out var fields) && fields.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in fields.EnumerateObject())
                        {
                            if (field.Value.ValueKind == JsonValueKind.Array)
                                errors[field.Name] = field.Value.EnumerateArray()
                                    .Where(x => x.ValueKind == JsonValueKind.String)
                                    .Select(x => x.GetString()!).ToArray();
                        }
                    }
                }
            }
            catch (JsonException) { }

            throw new ListingManagementException(response.StatusCode,
                detail ?? $"Không thể thực hiện yêu cầu ({(int)response.StatusCode}).", code, errors);
        }

        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
            ?? throw new InvalidOperationException("The listing endpoint returned an empty response.");
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
