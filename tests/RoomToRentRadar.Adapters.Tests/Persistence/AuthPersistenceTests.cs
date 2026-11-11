using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using User = RoomToRentRadar.Domain.Entities.User;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

public class AuthPersistenceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private CosmosDbContext _dbContext = null!;
    private CosmosUserRepository _userRepository = null!;
    private CosmosRefreshTokenRepository _refreshTokenRepository = null!;
    private CosmosUnitOfWork _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbContext = CosmosDbContext.CreateInMemory();
        await _dbContext.EnsureDatabaseAndContainersAsync();

        _unitOfWork = new CosmosUnitOfWork(_dbContext);
        _userRepository = new CosmosUserRepository(_dbContext, _unitOfWork);
        _refreshTokenRepository = new CosmosRefreshTokenRepository(_dbContext, _unitOfWork);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task EnsureDatabaseAndContainersAsync_ShouldInitializeCosmosContainersCleanly()
    {
        await _dbContext.EnsureDatabaseAndContainersAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.Users, Is.Not.Null);
            Assert.That(_dbContext.RefreshTokens, Is.Not.Null);
        });
    }

    [Test]
    public async Task UserRepository_AddAndGetByEmail_ShouldPersistAndLoadUserAggregate()
    {
        var email = EmailAddress.Create("Student@Example.com");
        var user = User.RegisterLocalUser(email, "bcrypt-hash", UserRole.Student, FixedNow);

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var loaded = await _userRepository.GetByEmailAsync(EmailAddress.Create("student@example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Id, Is.EqualTo(user.Id));
            Assert.That(loaded.Email.Value, Is.EqualTo("student@example.com"));
            Assert.That(loaded.PasswordHash, Is.EqualTo("bcrypt-hash"));
            Assert.That(loaded.Role, Is.EqualTo(UserRole.Student));
            Assert.That(loaded.IsActive, Is.True);
            Assert.That(loaded.SecurityStamp, Is.EqualTo(user.SecurityStamp));
        });
    }

    [Test]
    public async Task UnitOfWork_WhenDuplicateEmailInserted_ShouldTranslateConstraintToDuplicateEmailException()
    {
        var email = EmailAddress.Create("duplicate@example.com");
        var user1 = User.RegisterLocalUser(email, "hash1", UserRole.Student, FixedNow);
        var user2 = User.RegisterLocalUser(email, "hash2", UserRole.Landlord, FixedNow);

        await _userRepository.AddAsync(user1);
        await _unitOfWork.SaveChangesAsync();

        await _userRepository.AddAsync(user2);

        Assert.ThrowsAsync<DuplicateEmailException>(async () =>
            await _unitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task UserRepository_RegisterGoogleUser_ShouldPersistExternalLoginAndLookupByProviderAndSubject()
    {
        var email = EmailAddress.Create("googleuser@gmail.com");
        var user = User.RegisterGoogleUser(email, "google-sub-999", UserRole.Landlord, FixedNow);

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var loaded = await _userRepository.GetByExternalLoginAsync(UserExternalLogin.GoogleProvider, "google-sub-999");

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Id, Is.EqualTo(user.Id));
            Assert.That(loaded.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(loaded.ExternalLogins.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UserExternalLogin_WhenDuplicateProviderAndSubject_ShouldEnforceUniqueConstraint()
    {
        var user1 = User.RegisterGoogleUser(EmailAddress.Create("one@gmail.com"), "shared-sub", UserRole.Student, FixedNow);
        var user2 = User.RegisterGoogleUser(EmailAddress.Create("two@gmail.com"), "shared-sub", UserRole.Landlord, FixedNow);

        await _userRepository.AddAsync(user1);
        await _unitOfWork.SaveChangesAsync();

        await _userRepository.AddAsync(user2);

        Assert.CatchAsync(async () =>
            await _unitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task RefreshTokenRepository_ShouldEnforceUniqueTokenHashAndQueryByFamily()
    {
        var user = User.RegisterLocalUser(EmailAddress.Create("tokenuser@example.com"), "hash", UserRole.Student, FixedNow);
        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var family = Guid.NewGuid();
        var token1 = RefreshToken.Create(user.Id, "hash-1", family, FixedNow, FixedNow.AddDays(7));
        var token2 = RefreshToken.Create(user.Id, "hash-2", family, FixedNow.AddMinutes(5), FixedNow.AddDays(7));

        await _refreshTokenRepository.AddAsync(token1);
        await _refreshTokenRepository.AddAsync(token2);
        await _unitOfWork.SaveChangesAsync();

        var familyTokens = await _refreshTokenRepository.GetByFamilyAsync(family);
        Assert.That(familyTokens, Has.Count.EqualTo(2));

        var duplicateHashToken = RefreshToken.Create(user.Id, "hash-1", family, FixedNow, FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(duplicateHashToken);

        Assert.CatchAsync(async () =>
            await _unitOfWork.SaveChangesAsync());
    }

    [Test]
    public async Task ExecuteInTransactionAsync_WhenOperationThrows_ShouldRollbackAllChanges()
    {
        var user = User.RegisterLocalUser(EmailAddress.Create("txuser@example.com"), "hash", UserRole.Student, FixedNow);
        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var family = Guid.NewGuid();
        var oldToken = RefreshToken.Create(user.Id, "old-hash", family, FixedNow, FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(oldToken);
        await _unitOfWork.SaveChangesAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var replacement = RefreshToken.Create(user.Id, "new-hash", family, FixedNow.AddMinutes(1), FixedNow.AddDays(7));
                oldToken.ReplaceWith(replacement.Id, FixedNow.AddMinutes(1));
                _refreshTokenRepository.Update(oldToken);
                await _unitOfWork.SaveChangesAsync(ct);

                throw new InvalidOperationException("Simulated failure before new token is saved.");
            });
        });

        var reloadedOldToken = await _refreshTokenRepository.GetByTokenHashAsync("old-hash");
        var reloadedNewToken = await _refreshTokenRepository.GetByTokenHashAsync("new-hash");

        Assert.Multiple(() =>
        {
            Assert.That(reloadedOldToken, Is.Not.Null);
            Assert.That(reloadedOldToken!.IsRevoked, Is.False);
            Assert.That(reloadedNewToken, Is.Null);
        });
    }
}
