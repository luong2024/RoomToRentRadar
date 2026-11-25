using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.UseCases.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class AuthServiceTests
{
    [Test]
    public async Task Register_WithNonSelfServiceRole_ReturnsRoleNotAllowed()
    {
        var repository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var result = await service.RegisterAsync(
            new RegisterUserCommand("admin@example.test", "Password1", "Wannabe Admin", UserRole.Admin),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.RoleNotAllowed));
            Assert.That(result.Response, Is.Null);
            Assert.That(repository.AddedUser, Is.Null);
            Assert.That(unitOfWork.SaveCount, Is.Zero);
        });
    }

    [Test]
    public async Task Register_WhenUniqueEmailRaceIsLost_ReturnsDuplicateEmail()
    {
        var repository = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork
        {
            SaveException = new DuplicateEmailException(new InvalidOperationException("race"))
        };
        var service = CreateService(repository, unitOfWork);

        var result = await service.RegisterAsync(
            new RegisterUserCommand("new@example.test", "Password1", "New User", UserRole.Student),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.EmailAlreadyRegistered));
            Assert.That(result.Response, Is.Null);
            Assert.That(repository.AddedUser, Is.Not.Null);
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Register_WithSelfServiceRole_CreatesUserWithNormalizedEmail()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakeUnitOfWork());

        var result = await service.RegisterAsync(
            new RegisterUserCommand("  Land.Lord@Example.test  ", "Password1", "Landlord", UserRole.Landlord),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.Created));
            Assert.That(repository.AddedUser, Is.Not.Null);
            Assert.That(repository.AddedUser!.Email, Is.EqualTo("land.lord@example.test"));
            Assert.That(repository.AddedUser.Role, Is.EqualTo(UserRole.Landlord));
        });
    }

    [Test]
    public async Task Login_WithUnknownEmail_RunsDummyVerification()
    {
        var hasher = new FakePasswordHasher();
        var service = CreateService(new FakeUserRepository(), new FakeUnitOfWork(), hasher);

        var result = await service.LoginAsync("missing@example.test", "Password1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(hasher.DummyVerifyCount, Is.EqualTo(1));
            Assert.That(hasher.VerifyCount, Is.Zero);
        });
    }

    [Test]
    public async Task Login_WithKnownEmail_VerifiesStoredHashInsteadOfDummy()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "demo@example.test",
            DisplayName = "Demo Tester",
            PasswordHash = "stored-hash",
            PasswordSalt = "stored-salt",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };
        var repository = new FakeUserRepository { ExistingUser = user };
        var hasher = new FakePasswordHasher { VerifyResult = true };
        var sessions = new FakeAuthSessionRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), hasher, sessions);

        var result = await service.LoginAsync("DEMO@example.test", "Password1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Role, Is.EqualTo("student"));
            Assert.That(hasher.VerifyCount, Is.EqualTo(1));
            Assert.That(hasher.DummyVerifyCount, Is.Zero);
            Assert.That(sessions.Added, Has.Count.EqualTo(1));
            Assert.That(sessions.Added[0].UserId, Is.EqualTo(user.Id));
        });
    }

    private static AuthService CreateService(
        FakeUserRepository repository,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher? passwordHasher = null,
        FakeAuthSessionRepository? authSessions = null)
        => new(
            repository,
            authSessions ?? new FakeAuthSessionRepository(),
            passwordHasher ?? new FakePasswordHasher(),
            new FakeTokenGenerator(),
            unitOfWork);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ExistingUser { get; init; }

        public User? AddedUser { get; private set; }

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(ExistingUser);

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            AddedUser = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthSessionRepository : IAuthSessionRepository
    {
        public List<AuthSession> Added { get; } = [];

        public Task AddAsync(AuthSession session, CancellationToken cancellationToken)
        {
            Added.Add(session);
            return Task.CompletedTask;
        }

        public Task<User?> FindUserByTokenHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult<User?>(null);

        public Task<AuthSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
            => Task.FromResult<AuthSession?>(null);

        public void Remove(AuthSession session)
        {
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public bool VerifyResult { get; init; }

        public int VerifyCount { get; private set; }

        public int DummyVerifyCount { get; private set; }

        public (string Hash, string Salt) Hash(string password) => ("hashed", "salted");

        public bool Verify(string password, string hash, string salt)
        {
            VerifyCount++;
            return VerifyResult;
        }

        public bool VerifyDummy(string password)
        {
            DummyVerifyCount++;
            return false;
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public (string Token, string TokenHash) Create() => ("token", "token-hash");

        public string Hash(string token) => token;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Exception? SaveException { get; init; }

        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return SaveException is null ? Task.CompletedTask : Task.FromException(SaveException);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
