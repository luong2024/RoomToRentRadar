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
    private const string ValidCode = "135790";

    [Test]
    public async Task BeginRegistration_WithNonSelfServiceRole_ReturnsRoleNotAllowed()
    {
        var repository = new FakeUserRepository();
        var challenges = new FakeRegistrationChallengeRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), challenges: challenges);

        var result = await service.BeginRegistrationAsync(
            new BeginRegistrationCommand("admin@example.test", "Password1", "Wannabe Admin", UserRole.Admin),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginRegistrationOutcome.RoleNotAllowed));
            Assert.That(result.Response, Is.Null);
            Assert.That(challenges.Added, Is.Empty);
        });
    }

    [Test]
    public async Task BeginRegistration_WithUndefinedRole_ReturnsRoleNotAllowed()
    {
        var repository = new FakeUserRepository();
        var challenges = new FakeRegistrationChallengeRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), challenges: challenges);

        var result = await service.BeginRegistrationAsync(
            new BeginRegistrationCommand("weird@example.test", "Password1", "Weird Role", (UserRole)42),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginRegistrationOutcome.RoleNotAllowed));
            Assert.That(challenges.Added, Is.Empty);
        });
    }

    [Test]
    public async Task BeginRegistration_WhenEmailAlreadyRegistered_ReturnsDuplicateEmail()
    {
        var repository = new FakeUserRepository { ExistingUser = CreateUser("taken@example.test") };
        var challenges = new FakeRegistrationChallengeRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), challenges: challenges);

        var result = await service.BeginRegistrationAsync(
            new BeginRegistrationCommand("TAKEN@example.test", "Password1", "Taken", UserRole.Student),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginRegistrationOutcome.EmailAlreadyRegistered));
            Assert.That(result.Response, Is.Null);
            Assert.That(challenges.Added, Is.Empty);
        });
    }

    [Test]
    public async Task BeginRegistration_WithSelfServiceRole_CreatesHashedChallengeAndSendsTheCode()
    {
        var repository = new FakeUserRepository();
        var challenges = new FakeRegistrationChallengeRepository();
        var emails = new FakeEmailSender();
        var service = CreateService(
            repository,
            new FakeUnitOfWork(),
            challenges: challenges,
            emailSender: emails);

        var result = await service.BeginRegistrationAsync(
            new BeginRegistrationCommand("  Land.Lord@Example.test  ", "Password1", "Landlord", UserRole.Landlord),
            CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(BeginRegistrationOutcome.ChallengeCreated));
        Assert.That(result.Response, Is.Not.Null);
        Assert.That(challenges.Added, Has.Count.EqualTo(1));

        var challenge = challenges.Added[0];
        Assert.Multiple(() =>
        {
            Assert.That(challenge.Email, Is.EqualTo("land.lord@example.test"));
            Assert.That(challenge.DisplayName, Is.EqualTo("Landlord"));
            Assert.That(challenge.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(challenge.PasswordHash, Is.EqualTo("hash:Password1"));
            Assert.That(challenge.PasswordHash, Is.Not.EqualTo("Password1"));
            Assert.That(challenge.OtpHash, Is.EqualTo($"hash:{ValidCode}"));
            Assert.That(challenge.OtpHash, Is.Not.EqualTo(ValidCode));
            Assert.That(challenge.AttemptCount, Is.Zero);
            Assert.That(
                challenge.ExpiresAt - challenge.CreatedAt,
                Is.EqualTo(TimeSpan.FromMinutes(AccountRules.OtpLifetimeMinutes)));
            Assert.That(result.Response!.ChallengeId, Is.EqualTo(challenge.Id));
            Assert.That(result.Response.Email, Is.EqualTo(challenge.Email));
            Assert.That(result.Response.ExpiresAt, Is.EqualTo(challenge.ExpiresAt));
            Assert.That(repository.AttemptedUser, Is.Null);
            Assert.That(emails.Sent, Has.Count.EqualTo(1));
            Assert.That(emails.Sent[0].ToEmail, Is.EqualTo(challenge.Email));
            Assert.That(emails.Sent[0].DisplayName, Is.EqualTo(challenge.DisplayName));
            Assert.That(emails.Sent[0].Code, Is.EqualTo(ValidCode));
            Assert.That(emails.Sent[0].ExpiresAt, Is.EqualTo(challenge.ExpiresAt));
            Assert.That(challenges.RemovedByEmail, Is.EqualTo(new[] { "land.lord@example.test" }));
            Assert.That(challenges.RemovedExpired, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task BeginRegistration_WhenEmailDeliveryFails_RemovesChallengeAndReturnsDeliveryFailed()
    {
        var challenges = new FakeRegistrationChallengeRepository();
        var emails = new FakeEmailSender { Result = false };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            new FakeUserRepository(),
            unitOfWork,
            challenges: challenges,
            emailSender: emails);

        var result = await service.BeginRegistrationAsync(
            new BeginRegistrationCommand("nobody@example.test", "Password1", "Nobody", UserRole.Student),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BeginRegistrationOutcome.EmailDeliveryFailed));
            Assert.That(result.Response, Is.Null);
            Assert.That(challenges.Added, Has.Count.EqualTo(1));
            Assert.That(challenges.Removed, Has.Count.EqualTo(1));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenChallengeIsUnknown_ReturnsChallengeNotFound()
    {
        var challenges = new FakeRegistrationChallengeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork, challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(Guid.NewGuid(), ValidCode),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.ChallengeNotFound));
            Assert.That(result.Response, Is.Null);
            Assert.That(unitOfWork.SaveCount, Is.Zero);
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenChallengeExpired_RemovesItAndReturnsExpired()
    {
        var challenge = CreateChallenge(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork, challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, ValidCode),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.ChallengeExpired));
            Assert.That(challenges.Removed, Does.Contain(challenge));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerifyRegistration_WithWrongCode_ReturnsRemainingAttemptsAndConsumesOneAttempt()
    {
        var challenge = CreateChallenge();
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork, challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, "000000"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.InvalidCode));
            Assert.That(result.AttemptsRemaining, Is.EqualTo(AccountRules.OtpMaxAttempts - 1));
            Assert.That(result.Response, Is.Null);
            Assert.That(challenge.AttemptCount, Is.EqualTo(1));
            Assert.That(challenges.Consumed, Is.EqualTo(new[] { challenge.Id }));
            Assert.That(challenges.Removed, Is.Empty);
            Assert.That(unitOfWork.SaveCount, Is.Zero);
            Assert.That(unitOfWork.BeginTransactionCount, Is.Zero);
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenAnotherRequestExhaustsTheCapFirst_RemovesChallengeAndReturnsAttemptsExceeded()
    {
        var challenge = CreateChallenge(attemptCount: AccountRules.OtpMaxAttempts - 1);
        var challenges = new FakeRegistrationChallengeRepository
        {
            Challenge = challenge,
            ExhaustOnConsume = true
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork, challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, "000000"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.AttemptsExceeded));
            Assert.That(result.Response, Is.Null);
            Assert.That(challenges.Removed, Does.Contain(challenge));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerifyRegistration_WithFifthWrongCode_RemovesChallengeAndReturnsAttemptsExceeded()
    {
        var challenge = CreateChallenge(attemptCount: AccountRules.OtpMaxAttempts - 1);
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork, challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, "000000"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.AttemptsExceeded));
            Assert.That(challenge.AttemptCount, Is.EqualTo(AccountRules.OtpMaxAttempts));
            Assert.That(challenges.Removed, Does.Contain(challenge));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenAttemptsAlreadyExhausted_DoesNotVerifyAndRemovesChallenge()
    {
        var challenge = CreateChallenge(attemptCount: AccountRules.OtpMaxAttempts);
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var hasher = new FakePasswordHasher();
        var service = CreateService(
            new FakeUserRepository(),
            new FakeUnitOfWork(),
            hasher,
            challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, ValidCode),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.AttemptsExceeded));
            Assert.That(hasher.VerifyCount, Is.Zero);
            Assert.That(challenges.Removed, Does.Contain(challenge));
        });
    }

    [Test]
    public async Task VerifyRegistration_WithCorrectCode_CreatesUserAndSessionInsideATransaction()
    {
        var challenge = CreateChallenge();
        var repository = new FakeUserRepository();
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var sessions = new FakeAuthSessionRepository();
        var tokens = new FakeTokenGenerator();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            repository,
            unitOfWork,
            authSessions: sessions,
            tokenGenerator: tokens,
            challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, ValidCode),
            CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.Verified));
        Assert.That(result.Response, Is.Not.Null);
        Assert.That(repository.AttemptedUser, Is.Not.Null);

        var user = repository.AttemptedUser!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Response!.Role, Is.EqualTo("student"));
            Assert.That(result.Response.DisplayName, Is.EqualTo(challenge.DisplayName));
            Assert.That(result.Response.ExpiresAt, Is.EqualTo(tokens.ExpiresAt));
            Assert.That(user.Email, Is.EqualTo(challenge.Email));
            Assert.That(user.DisplayName, Is.EqualTo(challenge.DisplayName));
            Assert.That(user.PasswordHash, Is.EqualTo(challenge.PasswordHash));
            Assert.That(user.PasswordSalt, Is.EqualTo(challenge.PasswordSalt));
            Assert.That(user.Role, Is.EqualTo(challenge.Role));
            Assert.That(challenges.Removed, Does.Contain(challenge));
            Assert.That(sessions.Added, Has.Count.EqualTo(1));
            Assert.That(sessions.Added[0].UserId, Is.EqualTo(user.Id));
            Assert.That(tokens.Subjects, Has.Count.EqualTo(1));
            Assert.That(tokens.Subjects[0].UserId, Is.EqualTo(user.Id));
            Assert.That(unitOfWork.BeginTransactionCount, Is.EqualTo(1));
            Assert.That(unitOfWork.CommitCount, Is.EqualTo(1));
            Assert.That(unitOfWork.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerifyRegistration_WhenUniqueEmailRaceIsLost_RollsBackAndReturnsDuplicateEmail()
    {
        var challenge = CreateChallenge();
        var repository = new FakeUserRepository { TryAddResult = false };
        var challenges = new FakeRegistrationChallengeRepository { Challenge = challenge };
        var sessions = new FakeAuthSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            repository,
            unitOfWork,
            authSessions: sessions,
            challenges: challenges);

        var result = await service.VerifyRegistrationAsync(
            new VerifyRegistrationCommand(challenge.Id, ValidCode),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(VerifyRegistrationOutcome.EmailAlreadyRegistered));
            Assert.That(result.Response, Is.Null);
            Assert.That(repository.AttemptedUser, Is.Not.Null);
            Assert.That(sessions.Added, Is.Empty);
            Assert.That(challenges.Removed, Does.Contain(challenge));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
            Assert.That(unitOfWork.CommitCount, Is.EqualTo(1));
            Assert.That(unitOfWork.DisposeCount, Is.EqualTo(1));
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
        var user = CreateUser("demo@example.test");
        user.PasswordHash = "stored-hash";
        user.PasswordSalt = "stored-salt";
        var repository = new FakeUserRepository { ExistingUser = user };
        var hasher = new FakePasswordHasher { VerifyStoredResult = true };
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

    [Test]
    public async Task GetProfile_WithKnownUser_ReturnsAccountDetails()
    {
        var user = CreateUser("profile@example.test");
        user.DisplayName = "Profile Owner";
        user.Role = UserRole.Landlord;
        var repository = new FakeUserRepository { ExistingUser = user };
        var service = CreateService(repository, new FakeUnitOfWork());

        var profile = await service.GetProfileAsync(user.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.UserId, Is.EqualTo(user.Id));
            Assert.That(profile.Email, Is.EqualTo(user.Email));
            Assert.That(profile.DisplayName, Is.EqualTo("Profile Owner"));
            Assert.That(profile.Role, Is.EqualTo("landlord"));
            Assert.That(profile.CreatedAt, Is.EqualTo(user.CreatedAt));
        });
    }

    [Test]
    public async Task GetProfile_WithUnknownUser_ReturnsNull()
    {
        var service = CreateService(new FakeUserRepository(), new FakeUnitOfWork());

        var profile = await service.GetProfileAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(profile, Is.Null);
    }

    [Test]
    public async Task UpdateDisplayName_WithKnownUser_TrimsAndSaves()
    {
        var user = CreateUser("profile@example.test");
        var repository = new FakeUserRepository { ExistingUser = user };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var profile = await service.UpdateDisplayNameAsync(
            user.Id,
            "  New Name  ",
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.DisplayName, Is.EqualTo("New Name"));
            Assert.That(user.DisplayName, Is.EqualTo("New Name"));
            Assert.That(unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdateDisplayName_WithUnknownUser_ReturnsNullWithoutSaving()
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(new FakeUserRepository(), unitOfWork);

        var profile = await service.UpdateDisplayNameAsync(
            Guid.NewGuid(),
            "New Name",
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(profile, Is.Null);
            Assert.That(unitOfWork.SaveCount, Is.Zero);
        });
    }

    private static AuthService CreateService(
        FakeUserRepository repository,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher? passwordHasher = null,
        FakeAuthSessionRepository? authSessions = null,
        FakeTokenGenerator? tokenGenerator = null,
        FakeRegistrationChallengeRepository? challenges = null,
        FakeEmailSender? emailSender = null,
        IOtpGenerator? otpGenerator = null)
        => new(
            repository,
            authSessions ?? new FakeAuthSessionRepository(),
            challenges ?? new FakeRegistrationChallengeRepository(),
            passwordHasher ?? new FakePasswordHasher(),
            tokenGenerator ?? new FakeTokenGenerator(),
            otpGenerator ?? new FakeOtpGenerator(),
            emailSender ?? new FakeEmailSender(),
            unitOfWork);

    private static RegistrationChallenge CreateChallenge(
        DateTime? expiresAt = null,
        int attemptCount = 0)
    {
        var now = DateTime.UtcNow;
        return new RegistrationChallenge
        {
            Id = Guid.NewGuid(),
            Email = "new@example.test",
            DisplayName = "New User",
            PasswordHash = "hash:Password1",
            PasswordSalt = "password-salt",
            Role = UserRole.Student,
            OtpHash = $"hash:{ValidCode}",
            OtpSalt = "otp-salt",
            AttemptCount = attemptCount,
            ExpiresAt = expiresAt ?? now.AddMinutes(AccountRules.OtpLifetimeMinutes),
            CreatedAt = now
        };
    }

    private static User CreateUser(string email)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Existing User",
            PasswordHash = "stored-hash",
            PasswordSalt = "stored-salt",
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ExistingUser { get; init; }

        public bool TryAddResult { get; init; } = true;

        public User? AttemptedUser { get; private set; }

        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(ExistingUser);

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(ExistingUser is { } user && user.Id == id ? user : null);

        public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
        {
            AttemptedUser = user;
            return Task.FromResult(TryAddResult);
        }
    }

    private sealed class FakeRegistrationChallengeRepository : IRegistrationChallengeRepository
    {
        public RegistrationChallenge? Challenge { get; init; }

        public bool ExhaustOnConsume { get; init; }

        public List<RegistrationChallenge> Added { get; } = [];

        public List<RegistrationChallenge> Removed { get; } = [];

        public List<Guid> Consumed { get; } = [];

        public List<string> RemovedByEmail { get; } = [];

        public List<DateTime> RemovedExpired { get; } = [];

        public Task AddAsync(RegistrationChallenge challenge, CancellationToken cancellationToken)
        {
            Added.Add(challenge);
            return Task.CompletedTask;
        }

        public Task<RegistrationChallenge?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Challenge);

        public Task<int?> TryConsumeAttemptAsync(
            Guid challengeId,
            int maxAttempts,
            CancellationToken cancellationToken)
        {
            if (ExhaustOnConsume || Challenge is null || Challenge.Id != challengeId)
            {
                return Task.FromResult<int?>(null);
            }

            Consumed.Add(challengeId);
            if (Challenge.AttemptCount >= maxAttempts)
            {
                return Task.FromResult<int?>(null);
            }

            Challenge.AttemptCount++;
            return Task.FromResult<int?>(Challenge.AttemptCount);
        }

        public Task RemoveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            RemovedByEmail.Add(normalizedEmail);
            return Task.CompletedTask;
        }

        public Task RemoveExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        {
            RemovedExpired.Add(cutoffUtc);
            return Task.CompletedTask;
        }

        public void Remove(RegistrationChallenge challenge) => Removed.Add(challenge);
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
        public bool VerifyStoredResult { get; init; }

        public int VerifyCount { get; private set; }

        public int DummyVerifyCount { get; private set; }

        public (string Hash, string Salt) Hash(string password)
            => ($"hash:{password}", $"salt:{password}");

        public bool Verify(string password, string hash, string salt)
        {
            VerifyCount++;

            // Fake hashes expose the input so OTP checks can be exercised without PBKDF2;
            // stored credentials keep their configured answer.
            return hash.StartsWith("hash:", StringComparison.Ordinal)
                ? hash == $"hash:{password}"
                : VerifyStoredResult;
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

    private sealed class FakeOtpGenerator : IOtpGenerator
    {
        public string Generate() => ValidCode;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<RegistrationOtpEmail> Sent { get; } = [];

        public bool Result { get; init; } = true;

        public Task<bool> SendRegistrationOtpAsync(
            RegistrationOtpEmail email,
            CancellationToken cancellationToken)
        {
            Sent.Add(email);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public int BeginTransactionCount { get; private set; }

        public int CommitCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginTransactionCount++;
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(this));
        }

        private sealed class FakeTransaction(FakeUnitOfWork owner) : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.CommitCount++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                owner.DisposeCount++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
