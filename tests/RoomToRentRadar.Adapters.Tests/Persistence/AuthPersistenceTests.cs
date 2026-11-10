using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Adapters.Outbound.Persistence;
using RoomToRentRadar.Adapters.Outbound.Persistence.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

public class AuthPersistenceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private SqliteConnection _connection = null!;
    private AppDbContext _dbContext = null!;
    private EfUserRepository _userRepository = null!;
    private EfRefreshTokenRepository _refreshTokenRepository = null!;
    private EfUnitOfWork _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AppDbContext(options);
        await _dbContext.Database.MigrateAsync();

        _userRepository = new EfUserRepository(_dbContext);
        _refreshTokenRepository = new EfRefreshTokenRepository(_dbContext);
        _unitOfWork = new EfUnitOfWork(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task MigrateAsync_ShouldApplyInitialAuthSchemaCleanly()
    {
        var appliedMigrations = await _dbContext.Database.GetAppliedMigrationsAsync();

        Assert.That(appliedMigrations, Has.One.Contains("InitialAuthSchema"));
    }

    [Test]
    public async Task UserRepository_AddAndGetByEmail_ShouldPersistAndLoadUserAggregate()
    {
        var email = EmailAddress.Create("Student@Example.com");
        var user = User.RegisterLocalUser(email, "bcrypt-hash", UserRole.Student, FixedNow);

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

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

        _dbContext.ChangeTracker.Clear();

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

        Assert.ThrowsAsync<DbUpdateException>(async () =>
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

        Assert.ThrowsAsync<DbUpdateException>(async () =>
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

        _dbContext.ChangeTracker.Clear();

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
