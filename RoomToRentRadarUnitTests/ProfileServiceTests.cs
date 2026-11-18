using RoomToRentRadar.Features.Profile.Models;
using RoomToRentRadar.Features.Profile.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ProfileServiceTests
{
    [Test]
    public async Task UpdateAsync_WhenStudent_UsesTenantEndpointAndSelectedAvatar()
    {
        var client = new FakeApiClient();
        var service = new ProfileService(client);
        var profile = new ProfileApiDto
        {
            Role = "STUDENT",
            FullName = "Student",
            Tenant = new TenantProfileDto { University = "ICTU" }
        };

        await service.UpdateAsync(profile, "data:image/jpeg;base64,avatar-data");

        Assert.Multiple(() =>
        {
            Assert.That(client.PutEndpoint, Is.EqualTo("api/v1/profiles/me/tenant"));
            Assert.That(((UpdateTenantProfileRequest)client.PutRequest!).AvatarUrl, Is.EqualTo("data:image/jpeg;base64,avatar-data"));
        });
    }

    [TestCase("LANDLORD", "api/v1/profiles/me/landlord")]
    [TestCase("ADMIN", "api/v1/profiles/me/admin")]
    public async Task UpdateAsync_WhenRoleRequiresSpecificEndpoint_UsesMatchingEndpoint(string role, string expectedEndpoint)
    {
        var client = new FakeApiClient();
        var service = new ProfileService(client);
        var profile = new ProfileApiDto
        {
            Role = role,
            FullName = "Profile user",
            Landlord = new LandlordProfileDto(),
            Admin = new AdminProfileDto()
        };

        await service.UpdateAsync(profile, null);

        Assert.That(client.PutEndpoint, Is.EqualTo(expectedEndpoint));
    }

    private sealed class FakeApiClient : IApiClient
    {
        public string? PutEndpoint { get; private set; }
        public object? PutRequest { get; private set; }

        public Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult<TResponse?>(default);

        public Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
        {
            PutEndpoint = endpoint;
            PutRequest = request;
            return Task.FromResult<TResponse?>(default);
        }
    }
}
