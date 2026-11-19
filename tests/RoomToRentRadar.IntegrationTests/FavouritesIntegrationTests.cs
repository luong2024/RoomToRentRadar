using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Favourites;
using RoomToRentRadar.Domain.Entities;
using RoomDetailsStatus = RoomToRentRadar.Domain.Entities.RoomStatus;

namespace RoomToRentRadar.IntegrationTests;

[TestFixture]
public class FavouritesIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "in-memory");
        Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "IntegrationTestDb");

        _factory = new AuthWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task AllFavouritesEndpoints_WithoutAuthToken_ShouldReturn401Unauthorized()
    {
        var getResponse = await _client.GetAsync("/api/v1/favourites");
        var getIdsResponse = await _client.GetAsync("/api/v1/favourites/ids");
        var postResponse = await _client.PostAsync("/api/v1/favourites/room-01", null);
        var putResponse = await _client.PutAsync("/api/v1/favourites/room-01", null);
        var deleteResponse = await _client.DeleteAsync("/api/v1/favourites/room-01");
        var syncResponse = await _client.PostAsJsonAsync("/api/v1/favourites/sync", new SyncFavouritesRequest(["room-01"]));

        Assert.Multiple(() =>
        {
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(getIdsResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(postResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(putResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(syncResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task CrudFlow_AddGetIdsGetListAndRemove_ShouldWorkIdempotently()
    {
        SeedRoom("Room-ICTU-001", RoomDetailsStatus.Available, isVerified: true, thumbnailUrl: "https://example.com/thumb1.jpg");
        var token = await RegisterAndLoginStudentAsync("student.crud@ictu.edu.vn");

        // 1. Add via PUT (idempotent first call)
        using var putReq1 = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-ictu-001", token);
        var putRes1 = await _client.SendAsync(putReq1);
        Assert.That(putRes1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var addedDto1 = await putRes1.Content.ReadFromJsonAsync<FavouriteRoomItemDto>();

        // 2. Add same room again via POST (idempotent second call)
        using var postReq2 = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/ROOM-ICTU-001", token);
        var postRes2 = await _client.SendAsync(postReq2);
        Assert.That(postRes2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var addedDto2 = await postRes2.Content.ReadFromJsonAsync<FavouriteRoomItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(addedDto1, Is.Not.Null);
            Assert.That(addedDto1!.RoomId, Is.EqualTo("Room-ICTU-001"));
            Assert.That(addedDto1.IsAvailable, Is.True);
            Assert.That(addedDto1.ThumbnailUrl, Is.EqualTo("https://example.com/thumb1.jpg"));
            Assert.That(addedDto2, Is.Not.Null);
            Assert.That(addedDto2!.FavouritedAtUtc, Is.EqualTo(addedDto1.FavouritedAtUtc));
        });

        // 3. Get IDs
        using var getIdsReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites/ids", token);
        var getIdsRes = await _client.SendAsync(getIdsReq);
        Assert.That(getIdsRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var idsDto = await getIdsRes.Content.ReadFromJsonAsync<FavouriteIdsResultDto>();
        Assert.That(idsDto!.RoomIds, Is.EqualTo(new[] { "Room-ICTU-001" }));

        // 4. Get full list
        using var getListReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites", token);
        var getListRes = await _client.SendAsync(getListReq);
        Assert.That(getListRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listDto = await getListRes.Content.ReadFromJsonAsync<FavouriteListResultDto>();
        Assert.That(listDto!.TotalCount, Is.EqualTo(1));
        Assert.That(listDto.Items[0].RoomId, Is.EqualTo("Room-ICTU-001"));

        // 5. Delete twice (idempotent)
        using var delReq1 = CreateAuthorizedRequest(HttpMethod.Delete, "/api/v1/favourites/Room-ICTU-001", token);
        var delRes1 = await _client.SendAsync(delReq1);
        using var delReq2 = CreateAuthorizedRequest(HttpMethod.Delete, "/api/v1/favourites/Room-ICTU-001", token);
        var delRes2 = await _client.SendAsync(delReq2);

        Assert.Multiple(() =>
        {
            Assert.That(delRes1.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(delRes2.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });

        // 6. Verify empty after delete
        using var getAfterDelReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites", token);
        var getAfterDelRes = await _client.SendAsync(getAfterDelReq);
        var emptyListDto = await getAfterDelRes.Content.ReadFromJsonAsync<FavouriteListResultDto>();
        Assert.That(emptyListDto!.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AddFavourite_WhenRoomIsRentedButVerified_ShouldSucceedWithIsAvailableFalse_SupportingClientUndo()
    {
        SeedRoom("room-rented-undo", RoomDetailsStatus.Rented, isVerified: true);
        var token = await RegisterAndLoginStudentAsync("student.undo@ictu.edu.vn");

        using var req = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-rented-undo", token);
        var response = await _client.SendAsync(req);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<FavouriteRoomItemDto>();
        Assert.Multiple(() =>
        {
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.RoomId, Is.EqualTo("room-rented-undo"));
            Assert.That(dto.IsAvailable, Is.False);
            Assert.That(dto.Status, Is.EqualTo("Rented"));
            Assert.That(dto.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task AddFavourite_WhenRoomDoesNotExistOrUnverifiedOrInvalidId_ShouldReturnExpectedErrorStatus()
    {
        SeedRoom("room-unverified-01", RoomDetailsStatus.Available, isVerified: false);
        var token = await RegisterAndLoginStudentAsync("student.errors@ictu.edu.vn");

        // 1. Non-existent room -> 404 Not Found
        using var missingReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/missing-room-999", token);
        var missingRes = await _client.SendAsync(missingReq);

        // 2. Unverified room -> 400 Bad Request
        using var unverifiedReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/room-unverified-01", token);
        var unverifiedRes = await _client.SendAsync(unverifiedReq);

        // 3. Invalid roomId with forbidden characters -> 400 Bad Request
        using var invalidIdReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/room%2Finvalid", token);
        var invalidIdRes = await _client.SendAsync(invalidIdReq);

        Assert.Multiple(() =>
        {
            Assert.That(missingRes.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(unverifiedRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(invalidIdRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GetMyFavourites_WhenSavedRoomLaterBecomesRentedOrDeleted_ShouldReturnAccurateFailClosedState()
    {
        SeedRoom("room-later-rented", RoomDetailsStatus.Available, isVerified: true);
        SeedRoom("room-later-deleted", RoomDetailsStatus.Available, isVerified: true);
        var token = await RegisterAndLoginStudentAsync("student.lifecycle@ictu.edu.vn");

        using (var r1 = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-later-rented", token))
        {
            await _client.SendAsync(r1);
        }
        using (var r2 = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-later-deleted", token))
        {
            await _client.SendAsync(r2);
        }

        // Simulate landlord marking room-later-rented as Rented, and deleting room-later-deleted
        SeedRoom("room-later-rented", RoomDetailsStatus.Rented, isVerified: true);
        _factory.RoomRepository.Remove("room-later-deleted");

        using var getReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites", token);
        var getRes = await _client.SendAsync(getReq);
        Assert.That(getRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listDto = await getRes.Content.ReadFromJsonAsync<FavouriteListResultDto>();
        Assert.That(listDto, Is.Not.Null);
        Assert.That(listDto!.TotalCount, Is.EqualTo(2));

        var rentedItem = listDto.Items.Single(i => i.RoomId == "room-later-rented");
        var deletedItem = listDto.Items.Single(i => i.RoomId == "room-later-deleted");

        Assert.Multiple(() =>
        {
            Assert.That(rentedItem.IsAvailable, Is.False);
            Assert.That(rentedItem.IsDeleted, Is.False);
            Assert.That(rentedItem.Status, Is.EqualTo("Rented"));

            Assert.That(deletedItem.IsAvailable, Is.False);
            Assert.That(deletedItem.IsDeleted, Is.True);
            Assert.That(deletedItem.Status, Is.EqualTo("Deleted"));
        });
    }

    [Test]
    public async Task SyncFavourites_ShouldMergeValidRoomsSkipInvalidAndRespectBatchLimit()
    {
        SeedRoom("Room-Sync-01", RoomDetailsStatus.Available, isVerified: true);
        SeedRoom("Room-Sync-02", RoomDetailsStatus.Available, isVerified: true);
        SeedRoom("Room-Sync-Unverified", RoomDetailsStatus.Available, isVerified: false);

        var token = await RegisterAndLoginStudentAsync("student.sync@ictu.edu.vn");

        // Pre-populate Room-Sync-01 in account
        using (var preReq = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/Room-Sync-01", token))
        {
            await _client.SendAsync(preReq);
        }

        // Sync batch containing existing, new valid, duplicate, non-existent, unverified, and forbidden-char IDs
        using var syncReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/sync", token);
        syncReq.Content = JsonContent.Create(new SyncFavouritesRequest([
            "room-sync-01",
            "room-sync-02",
            "ROOM-SYNC-02",
            "non-existent-room",
            "Room-Sync-Unverified",
            "invalid?room#id",
            "   ",
            null
        ]));

        var syncRes = await _client.SendAsync(syncReq);
        Assert.That(syncRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var syncDto = await syncRes.Content.ReadFromJsonAsync<SyncFavouritesResultDto>();
        Assert.Multiple(() =>
        {
            Assert.That(syncDto, Is.Not.Null);
            Assert.That(syncDto!.AddedCount, Is.EqualTo(1));
            Assert.That(syncDto.TotalCount, Is.EqualTo(2));
            Assert.That(syncDto.Favourites.Select(f => f.RoomId), Is.EquivalentTo(new[] { "Room-Sync-01", "Room-Sync-02" }));
            Assert.That(syncDto.SkippedRoomIds, Is.EquivalentTo(new[] { "non-existent-room", "Room-Sync-Unverified", "invalid?room#id" }));
        });

        // Oversized batch (> 100) should return 400 Bad Request
        var oversized = Enumerable.Range(1, 101).Select(i => (string?)$"room-{i}").ToList();
        using var oversizedReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/sync", token);
        oversizedReq.Content = JsonContent.Create(new SyncFavouritesRequest(oversized));

        var oversizedRes = await _client.SendAsync(oversizedReq);
        Assert.That(oversizedRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ConcurrentAddAndSyncRequests_WhenUserNearMaxQuota200_ShouldNeverExceedMaxFavouritesLimit()
    {
        const int maxQuota = AddFavouriteUseCase.MaxFavouritesPerUser; // 200
        const int initialCount = 195;
        const int concurrentExtraCount = 20;

        for (var i = 1; i <= initialCount + concurrentExtraCount; i++)
        {
            SeedRoom($"room-quota-{i:D3}", RoomDetailsStatus.Available, isVerified: true);
        }

        var token = await RegisterAndLoginStudentAsync("student.concurrent.quota@ictu.edu.vn");

        // Seed 195 favourites via two sync batches (100 + 95)
        var batch1 = Enumerable.Range(1, 100).Select(i => (string?)$"room-quota-{i:D3}").ToList();
        var batch2 = Enumerable.Range(101, 95).Select(i => (string?)$"room-quota-{i:D3}").ToList();

        using (var s1 = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/sync", token))
        {
            s1.Content = JsonContent.Create(new SyncFavouritesRequest(batch1));
            var r1 = await _client.SendAsync(s1);
            Assert.That(r1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        using (var s2 = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/favourites/sync", token))
        {
            s2.Content = JsonContent.Create(new SyncFavouritesRequest(batch2));
            var r2 = await _client.SendAsync(s2);
            Assert.That(r2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // Fire 20 concurrent PUT requests for distinct rooms (196..215) at the same time
        var concurrentTasks = Enumerable.Range(196, concurrentExtraCount).Select(async i =>
        {
            using var req = CreateAuthorizedRequest(HttpMethod.Put, $"/api/v1/favourites/room-quota-{i:D3}", token);
            return await _client.SendAsync(req);
        }).ToArray();

        var responses = await Task.WhenAll(concurrentTasks);
        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var badRequestCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        foreach (var resp in responses)
        {
            resp.Dispose();
        }

        using var getIdsReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites/ids", token);
        var getIdsRes = await _client.SendAsync(getIdsReq);
        var idsDto = await getIdsRes.Content.ReadFromJsonAsync<FavouriteIdsResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(okCount, Is.EqualTo(5), "Exactly 5 concurrent requests should succeed to reach the 200 limit.");
            Assert.That(badRequestCount, Is.EqualTo(15), "Remaining 15 concurrent requests must be rejected with 400 Bad Request.");
            Assert.That(idsDto!.TotalCount, Is.EqualTo(maxQuota), "Total favourites count must never exceed 200.");
            Assert.That(idsDto.RoomIds, Has.Count.EqualTo(maxQuota));
        });
    }

    [Test]
    public async Task MultiUserIsolation_UserAShouldNeverSeeOrModifyUserBFavourites()
    {
        SeedRoom("room-shared-01", RoomDetailsStatus.Available, isVerified: true);
        SeedRoom("room-shared-02", RoomDetailsStatus.Available, isVerified: true);

        var tokenA = await RegisterAndLoginStudentAsync("user.a@ictu.edu.vn");
        var tokenB = await RegisterAndLoginStudentAsync("user.b@ictu.edu.vn");

        // UserA adds room-shared-01
        using (var addA = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-shared-01", tokenA))
        {
            var res = await _client.SendAsync(addA);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // UserB adds room-shared-02
        using (var addB = CreateAuthorizedRequest(HttpMethod.Put, "/api/v1/favourites/room-shared-02", tokenB))
        {
            var res = await _client.SendAsync(addB);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // UserB attempts to delete room-shared-01 (which belongs to UserA's favourites)
        using (var delB = CreateAuthorizedRequest(HttpMethod.Delete, "/api/v1/favourites/room-shared-01", tokenB))
        {
            var res = await _client.SendAsync(delB);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        // Verify UserA still has room-shared-01 and NOT room-shared-02
        using var getA = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites/ids", tokenA);
        var resA = await _client.SendAsync(getA);
        var idsA = await resA.Content.ReadFromJsonAsync<FavouriteIdsResultDto>();

        // Verify UserB only has room-shared-02
        using var getB = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/favourites/ids", tokenB);
        var resB = await _client.SendAsync(getB);
        var idsB = await resB.Content.ReadFromJsonAsync<FavouriteIdsResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(idsA!.RoomIds, Is.EqualTo(new[] { "room-shared-01" }));
            Assert.That(idsB!.RoomIds, Is.EqualTo(new[] { "room-shared-02" }));
        });
    }

    [Test]
    public async Task LiveCosmosEmulator_CaseInsensitiveRoomLookupAndMultiInstanceQuota_ShouldSucceed()
    {
        const string endpoint = "https://localhost:8081";
        const string authKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        var dbName = $"RoomToRentRadarDb_FavTest_{Guid.NewGuid():N}"[..36];

        var options = new CosmosDbOptions
        {
            AccountEndpoint = endpoint,
            AuthKey = authKey,
            DatabaseName = dbName,
            RoomsContainerName = "Rooms",
            FavouritesContainerName = "Favourites",
            ConnectionMode = "Gateway",
            IsEmulator = true
        };

        using var dbContext = new CosmosDbContext(options);

        try
        {
            await dbContext.EnsureDatabaseAndContainersAsync();
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is System.Net.Sockets.SocketException ||
            ex.InnerException is HttpRequestException ||
            ex.InnerException is System.Net.Sockets.SocketException ||
            (ex is CosmosException ce && (ce.StatusCode == 0 || (int)ce.StatusCode >= 500)) ||
            ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {endpoint}. Skipping live Cosmos DB verification.");
            return;
        }

        try
        {
            var cosmosRoomRepo = new CosmosRoomRepository(dbContext.RoomsContainer!);
            await cosmosRoomRepo.CreateAsync(new Room
            {
                Id = "Room-Cosmos-001",
                Title = "Cosmos Room 1",
                Price = 1800000m,
                Area = 22.0,
                Status = RoomDetailsStatus.Available,
                IsVerified = true
            });
            await cosmosRoomRepo.CreateAsync(new Room
            {
                Id = "Room-Cosmos-002",
                Title = "Cosmos Room 2",
                Price = 2000000m,
                Area = 24.0,
                Status = RoomDetailsStatus.Available,
                IsVerified = true
            });

            // 1. Verify [P1] on real Cosmos DB: case-insensitive GetByIdAsync and GetByIdsAsync
            var singleRoom = await cosmosRoomRepo.GetByIdAsync("room-cosmos-001");
            var batchRooms = await cosmosRoomRepo.GetByIdsAsync(new[] { "room-cosmos-001", "ROOM-COSMOS-002" });

            Assert.Multiple(() =>
            {
                Assert.That(singleRoom, Is.Not.Null);
                Assert.That(singleRoom!.Id, Is.EqualTo("Room-Cosmos-001"));
                Assert.That(batchRooms, Has.Count.EqualTo(2));
                Assert.That(batchRooms.Select(r => r.Id), Is.EquivalentTo(new[] { "Room-Cosmos-001", "Room-Cosmos-002" }));
            });

            // 2. Verify [P2] on real Cosmos DB: multi-instance concurrent quota enforcement
            var userId = Guid.NewGuid();
            const int maxQuota = 5;
            var now = DateTimeOffset.UtcNow;

            var tasks = Enumerable.Range(1, 12).Select(i => Task.Run(async () =>
            {
                var repoInstance = new CosmosFavouriteRepository(dbContext);
                var fav = FavouriteRoom.Create(userId, $"Room-Cosmos-Quota-{i:D2}", now.AddMilliseconds(i));
                return await repoInstance.TryAddWithQuotaAsync(fav, maxQuota);
            })).ToArray();

            var results = await Task.WhenAll(tasks);
            var verifyRepo = new CosmosFavouriteRepository(dbContext);
            var stored = await verifyRepo.GetByUserIdAsync(userId);

            Assert.Multiple(() =>
            {
                Assert.That(results.Count(r => r.Status == FavouriteAddStatus.Created), Is.EqualTo(maxQuota));
                Assert.That(results.Count(r => r.Status == FavouriteAddStatus.QuotaExceeded), Is.EqualTo(12 - maxQuota));
                Assert.That(stored, Has.Count.EqualTo(maxQuota));
            });
        }
        finally
        {
            if (dbContext.Client is not null)
            {
                try
                {
                    await dbContext.Client.GetDatabase(dbName).DeleteAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private void SeedRoom(
        string id,
        RoomDetailsStatus status,
        bool isVerified,
        string? thumbnailUrl = null)
    {
        _factory.RoomRepository.Upsert(new Room
        {
            Id = id,
            Title = $"Room {id}",
            Description = "Clean and comfortable student room",
            Price = 1800000m,
            Deposit = 1800000m,
            Area = 20,
            Address = "Z115 Street, Quyet Thang, Thai Nguyen",
            RoomType = "Private",
            Status = status,
            IsVerified = isVerified,
            ThumbnailUrl = thumbnailUrl,
            Amenities = ["Wifi", "Water heater"]
        });
    }

    private async Task<string> RegisterAndLoginStudentAsync(string email)
    {
        var registerRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterUserRequest(email, "StrongPass123!", "STUDENT"));
        Assert.That(registerRes.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var loginRes = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginWithEmailRequest(email, "StrongPass123!"));
        Assert.That(loginRes.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>();
        return session!.AccessToken;
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri, string bearerToken)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }
}
