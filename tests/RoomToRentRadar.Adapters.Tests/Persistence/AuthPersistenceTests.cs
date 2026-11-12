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
            Assert.That(_dbContext.EmailIndex, Is.Not.Null);
            Assert.That(_dbContext.ExternalLoginIndex, Is.Not.Null);
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

    [Test]
    public async Task UnitOfWork_WhenDuplicateEmailRegisteredConcurrently_ShouldEnsureOnlyOneSucceeds()
    {
        var email = EmailAddress.Create("concurrent-student@example.com");

        var uow1 = new CosmosUnitOfWork(_dbContext);
        var repo1 = new CosmosUserRepository(_dbContext, uow1);

        var uow2 = new CosmosUnitOfWork(_dbContext);
        var repo2 = new CosmosUserRepository(_dbContext, uow2);

        var user1 = User.RegisterLocalUser(email, "hash1", UserRole.Student, FixedNow);
        var user2 = User.RegisterLocalUser(email, "hash2", UserRole.Landlord, FixedNow);

        await repo1.AddAsync(user1);
        await repo2.AddAsync(user2);

        var task1 = Task.Run(async () =>
        {
            try
            {
                await uow1.SaveChangesAsync();
                return (Success: true, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex);
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await uow2.SaveChangesAsync();
                return (Success: true, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var conflictCount = results.Count(r => !r.Success && r.Error is DuplicateEmailException);

        Assert.Multiple(() =>
        {
            Assert.That(successCount, Is.EqualTo(1), "Exactly one registration must succeed.");
            Assert.That(conflictCount, Is.EqualTo(1), "The concurrent duplicate registration must fail with DuplicateEmailException.");
        });

        var loadedEmailIndex = await _dbContext.EmailIndex.ReadItemAsync("concurrent-student@example.com", "concurrent-student@example.com");
        Assert.That(loadedEmailIndex, Is.Not.Null);
    }

    [Test]
    public async Task RefreshToken_WhenRotatedConcurrentlyWithSameToken_ShouldEnforceETagConcurrencyAndAllowOnlyOne()
    {
        var user = User.RegisterLocalUser(EmailAddress.Create("concurrent-token@example.com"), "hash", UserRole.Student, FixedNow);
        await _userRepository.AddAsync(user);

        var family = Guid.NewGuid();
        var initialToken = RefreshToken.Create(user.Id, "shared-token-hash", family, FixedNow, FixedNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(initialToken);
        await _unitOfWork.SaveChangesAsync();

        // Simulate 2 concurrent requests resolving scoped instances
        var uow1 = new CosmosUnitOfWork(_dbContext);
        var repo1 = new CosmosRefreshTokenRepository(_dbContext, uow1);

        var uow2 = new CosmosUnitOfWork(_dbContext);
        var repo2 = new CosmosRefreshTokenRepository(_dbContext, uow2);

        // Both read the same active token hash concurrently
        var tokenForRequest1 = await repo1.GetByTokenHashAsync("shared-token-hash");
        var tokenForRequest2 = await repo2.GetByTokenHashAsync("shared-token-hash");

        Assert.Multiple(() =>
        {
            Assert.That(tokenForRequest1, Is.Not.Null);
            Assert.That(tokenForRequest2, Is.Not.Null);
        });

        var replacement1 = RefreshToken.Create(user.Id, "hash-replacement-1", family, FixedNow.AddMinutes(1), FixedNow.AddDays(7));
        var replacement2 = RefreshToken.Create(user.Id, "hash-replacement-2", family, FixedNow.AddMinutes(1), FixedNow.AddDays(7));

        tokenForRequest1!.ReplaceWith(replacement1.Id, FixedNow.AddMinutes(1));
        repo1.Update(tokenForRequest1);
        await repo1.AddAsync(replacement1);

        tokenForRequest2!.ReplaceWith(replacement2.Id, FixedNow.AddMinutes(1));
        repo2.Update(tokenForRequest2);
        await repo2.AddAsync(replacement2);

        var task1 = Task.Run(async () =>
        {
            try
            {
                await uow1.SaveChangesAsync();
                return (Success: true, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex);
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await uow2.SaveChangesAsync();
                return (Success: true, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var concurrencyFailureCount = results.Count(r => !r.Success && r.Error is InvalidRefreshTokenException);

        Assert.Multiple(() =>
        {
            Assert.That(successCount, Is.EqualTo(1), "Exactly one concurrent rotation request must succeed.");
            Assert.That(concurrencyFailureCount, Is.EqualTo(1), "The concurrent conflicting request must fail with InvalidRefreshTokenException due to ETag mismatch.");
        });
    }

    [Test]
    public void CosmosDbContext_WhenAccountEndpointMissingInProduction_ShouldFailFast()
    {
        var options = new RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations.CosmosDbOptions
        {
            AccountEndpoint = "",
            AuthKeyOrResourceToken = "some-key"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _ = new CosmosDbContext(options));
        Assert.That(ex.Message, Does.Contain("CosmosDb:AccountEndpoint"));
    }

    [Test]
    public void CosmosDbContext_WhenAuthKeyMissingInProduction_ShouldFailFast()
    {
        var options = new RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations.CosmosDbOptions
        {
            AccountEndpoint = "https://my-cosmos-account.documents.azure.com:443/",
            AuthKeyOrResourceToken = ""
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _ = new CosmosDbContext(options));
        Assert.That(ex.Message, Does.Contain("CosmosDb:AuthKeyOrResourceToken"));
    }

    [Test]
    public async Task User_WhenUpdatedConcurrently_ShouldEnforceETagAndPreventLostUpdate()
    {
        var email = EmailAddress.Create("concurrent-user-etag@example.com");
        var initialUser = User.RegisterLocalUser(email, "hash", UserRole.Student, FixedNow);
        await _userRepository.AddAsync(initialUser);
        await _unitOfWork.SaveChangesAsync();

        // Simulate 2 concurrent requests loading the same user snapshot (same initial ETag)
        var uowAdmin = new CosmosUnitOfWork(_dbContext);
        var repoAdmin = new CosmosUserRepository(_dbContext, uowAdmin);

        var uowLogin = new CosmosUnitOfWork(_dbContext);
        var repoLogin = new CosmosUserRepository(_dbContext, uowLogin);

        var userForAdmin = await repoAdmin.GetByIdAsync(initialUser.Id);
        var userForLogin = await repoLogin.GetByIdAsync(initialUser.Id);

        Assert.Multiple(() =>
        {
            Assert.That(userForAdmin, Is.Not.Null);
            Assert.That(userForLogin, Is.Not.Null);
            Assert.That(userForAdmin!.IsActive, Is.True);
        });

        // 1. Admin deactivates the user and rotates security stamp first
        var adminStampBefore = userForAdmin!.SecurityStamp;
        userForAdmin.Deactivate(FixedNow.AddMinutes(5));
        repoAdmin.Update(userForAdmin);
        await uowAdmin.SaveChangesAsync();

        // 2. Concurrent login request holding stale snapshot tries to update LastLoginAt
        userForLogin!.RecordSuccessfulLogin(FixedNow.AddMinutes(6));
        repoLogin.Update(userForLogin);

        Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await uowLogin.SaveChangesAsync());

        // 3. Verify that Admin's deactivation and security stamp rotation were NOT overwritten
        var uowVerify = new CosmosUnitOfWork(_dbContext);
        var repoVerify = new CosmosUserRepository(_dbContext, uowVerify);
        var persisted = await repoVerify.GetByIdAsync(initialUser.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.IsActive, Is.False);
            Assert.That(persisted.SecurityStamp, Is.Not.EqualTo(adminStampBefore));
            Assert.That(persisted.LastLoginAt, Is.Null);
        });
    }

    [Test]
    public async Task ExternalLogin_WhenSameIdentityRegisteredConcurrentlyAcrossUsers_ShouldEnsureOnlyOneSucceedsAndCleanupOrphanedEmailIndex()
    {
        var uow1 = new CosmosUnitOfWork(_dbContext);
        var repo1 = new CosmosUserRepository(_dbContext, uow1);

        var uow2 = new CosmosUnitOfWork(_dbContext);
        var repo2 = new CosmosUserRepository(_dbContext, uow2);

        var user1 = User.RegisterGoogleUser(EmailAddress.Create("ext-concurrent-1@gmail.com"), "shared-google-sub-777", UserRole.Student, FixedNow);
        var user2 = User.RegisterGoogleUser(EmailAddress.Create("ext-concurrent-2@gmail.com"), "shared-google-sub-777", UserRole.Landlord, FixedNow);

        await repo1.AddAsync(user1);
        await repo2.AddAsync(user2);

        var task1 = Task.Run(async () =>
        {
            try
            {
                await uow1.SaveChangesAsync();
                return (Success: true, FailedEmail: (string?)null, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, FailedEmail: "ext-concurrent-1@gmail.com", Error: ex);
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await uow2.SaveChangesAsync();
                return (Success: true, FailedEmail: (string?)null, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, FailedEmail: "ext-concurrent-2@gmail.com", Error: ex);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var conflictResult = results.SingleOrDefault(r => !r.Success);

        Assert.Multiple(() =>
        {
            Assert.That(successCount, Is.EqualTo(1), "Exactly one registration with the external identity must succeed.");
            Assert.That(conflictResult.Error, Is.InstanceOf<ExternalIdentityConflictException>());
        });

        // Verify the orphaned EmailIndex entry for the failed registration was cleaned up
        var failedEmail = conflictResult.FailedEmail!;
        var uowRetryLocal = new CosmosUnitOfWork(_dbContext);
        var repoRetryLocal = new CosmosUserRepository(_dbContext, uowRetryLocal);
        var localRetryUser = User.RegisterLocalUser(EmailAddress.Create(failedEmail), "local-hash", UserRole.Student, FixedNow);

        await repoRetryLocal.AddAsync(localRetryUser);
        Assert.DoesNotThrowAsync(async () => await uowRetryLocal.SaveChangesAsync());
    }
}
