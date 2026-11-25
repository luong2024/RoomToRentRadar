using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.UseCases.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class AuthServiceTests
{
    [Test]
    public async Task Register_WithNonSelfServiceRole_ReturnsRoleNotAllowed()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakeUnitOfWork());

        var result = await service.RegisterAsync(
            new RegisterUserCommand("admin@example.test", "Password1", "Wannabe Admin", UserRole.Admin),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.RoleNotAllowed));
            Assert.That(result.Response, Is.Null);
            Assert.That(repository.AttemptedUser, Is.Null);
        });
    }

    [Test]
    public async Task Register_WithUndefinedRole_ReturnsRoleNotAllowed()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository, new FakeUnitOfWork());

        var result = await service.RegisterAsync(
            new RegisterUserCommand("weird@example.test", "Password1", "Weird Role", (UserRole)42),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.RoleNotAllowed));
            Assert.That(repository.AttemptedUser, Is.Null);
        });
    }

    [Test]
    public async Task Register_WhenUniqueEmailRaceIsLost_ReturnsDuplicateEmail()
    {
        var repository = new FakeUserRepository { TryAddResult = false };
        var service = CreateService(repository, new FakeUnitOfWork());

        var result = await service.RegisterAsync(
            new RegisterUserCommand("new@example.test", "Password1", "New User", UserRole.Student),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RegisterUserOutcome.EmailAlreadyRegistered));
            Assert.That(result.Response, Is.Null);
            Assert.That(repository.AttemptedUser, Is.Not.Null);
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
            Assert.That(repository.AttemptedUser, Is.Not.Null);
            Assert.That(repository.AttemptedUser!.Email, Is.EqualTo("land.lord@example.test"));
            Assert.That(repository.AttemptedUser.Role, Is.EqualTo(UserRole.Landlord));
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
        var tokens = new FakeTokenGenerator();
        var service = CreateService(repository, new FakeUnitOfWork(), hasher, sessions, tokens);

        var result = await service.LoginAsync("DEMO@example.test", "Password1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Role, Is.EqualTo("student"));
            Assert.That(hasher.VerifyCount, Is.EqualTo(1));
            Assert.That(hasher.DummyVerifyCount, Is.Zero);
            Assert.That(sessions.Added, Has.Count.EqualTo(1));
            Assert.That(sessions.Added[0].UserId, Is.EqualTo(user.Id));
            Assert.That(sessions.Added[0].ExpiresAt, Is.EqualTo(tokens.ExpiresAt));
            Assert.That(tokens.Subjects, Has.Count.EqualTo(1));
            Assert.That(tokens.Subjects[0].UserId, Is.EqualTo(user.Id));
            Assert.That(tokens.Subjects[0].Role, Is.EqualTo(UserRole.Student));
            Assert.That(tokens.Subjects[0].Email, Is.EqualTo(user.Email));
        });
    }

    private static AuthService CreateService(
        FakeUserRepository repository,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher? passwordHasher = null,
        FakeAuthSessionRepository? authSessions = null,
        FakeTokenGenerator? tokenGenerator = null)
        => new(
            repository,
            authSessions ?? new FakeAuthSessionRepository(),
            passwordHasher ?? new FakePasswordHasher(),
            tokenGenerator ?? new FakeTokenGenerator(),
            unitOfWork);

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ExistingUser { get; init; }

        public bool TryAddResult { get; init; } = true;

        public User? AttemptedUser { get; private set; }

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(ExistingUser);

        public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
        {
            AttemptedUser = user;
            return Task.FromResult(TryAddResult);
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
        public List<AuthTokenSubject> Subjects { get; } = [];

        public DateTime? ExpiresAt { get; private set; }

        public (string Token, string TokenHash) Create(AuthTokenSubject subject, DateTime expiresAt)
        {
            Subjects.Add(subject);
            ExpiresAt = expiresAt;
            return ("token", "token-hash");
        }

        public string Hash(string token) => token;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
