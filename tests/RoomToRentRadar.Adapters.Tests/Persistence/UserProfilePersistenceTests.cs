using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class UserProfilePersistenceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private CosmosDbContext _dbContext = null!;
    private CosmosUserProfileRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbContext = CosmosDbContext.CreateInMemory();
        await _dbContext.EnsureDatabaseAndContainersAsync();
        _repository = new CosmosUserProfileRepository(_dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task UpdateAsync_WhenConcurrentUpdateOccurs_ShouldThrowConcurrencyConflictException()
    {
        // 1. Tạo một UserProfile ban đầu
        var userId = Guid.NewGuid();
        var initialProfile = UserProfile.CreateDefault(userId, UserRole.Landlord, FixedNow);
        initialProfile.UpdateBasicInfo("Original Landlord", null, null, null, FixedNow);
        await _repository.AddAsync(initialProfile);

        // 2. Hai repo / client đọc cùng một phiên bản profile cũ (cùng ETag ban đầu)
        var client1Repo = new CosmosUserProfileRepository(_dbContext);
        var client2Repo = new CosmosUserProfileRepository(_dbContext);

        var profileCopy1 = await client1Repo.GetByUserIdAsync(userId);
        var profileCopy2 = await client2Repo.GetByUserIdAsync(userId);

        Assert.That(profileCopy1, Is.Not.Null);
        Assert.That(profileCopy2, Is.Not.Null);
        Assert.That(profileCopy1!.Version, Is.Not.Null);
        Assert.That(profileCopy2!.Version, Is.EqualTo(profileCopy1.Version));

        // 3. Client 1 (ví dụ Admin) duyệt KYC và cập nhật thành công
        var oldEtag = profileCopy2!.Version;
        profileCopy1.UpdateBasicInfo("Admin Updated Name", null, null, null, FixedNow.AddMinutes(1));
        await client1Repo.UpdateAsync(profileCopy1);

        Assert.That(profileCopy1.Version, Is.Not.EqualTo(oldEtag));

        // 4. Client 2 (ví dụ Landlord gửi request cũ cùng thời điểm) cố ghi đè với version cũ
        profileCopy2.UpdateBasicInfo("Stale Update", null, null, null, FixedNow.AddMinutes(2));

        Assert.ThrowsAsync<ConcurrencyConflictException>(
            async () => await client2Repo.UpdateAsync(profileCopy2));

        // 5. Xác nhận dữ liệu trong DB không bị ghi đè bởi client 2
        var finalProfile = await _repository.GetByUserIdAsync(userId);
        Assert.That(finalProfile!.FullName, Is.EqualTo("Admin Updated Name"));
    }

    [Test]
    public void UpdateAsync_WhenProfileHasNoETag_ShouldThrowInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var profileWithoutVersion = UserProfile.CreateDefault(userId, UserRole.Student, FixedNow);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _repository.UpdateAsync(profileWithoutVersion));

        Assert.That(ex!.Message, Does.Contain("without an ETag"));
    }

    [Test]
    public async Task AddAsync_WhenProfileAlreadyExists_ShouldThrowConcurrencyConflictException()
    {
        var userId = Guid.NewGuid();
        var profile1 = UserProfile.CreateDefault(userId, UserRole.Student, FixedNow);
        var profile2 = UserProfile.CreateDefault(userId, UserRole.Student, FixedNow);

        await _repository.AddAsync(profile1);

        var ex = Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await _repository.AddAsync(profile2));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }
}
