using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Authentication;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests.Authentication;

public class EmailAuthenticationUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private InMemoryFakeUserRepository _userRepository = null!;
    private InMemoryFakeRefreshTokenRepository _refreshTokenRepository = null!;
    private FakePasswordHasher _passwordHasher = null!;
    private FakeTokenService _tokenService = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private FixedTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryFakeUserRepository();
        _refreshTokenRepository = new InMemoryFakeRefreshTokenRepository();
        _passwordHasher = new FakePasswordHasher();
        _tokenService = new FakeTokenService();
        _unitOfWork = new FakeUnitOfWork();
        _timeProvider = new FixedTimeProvider(FixedNow);
    }

    [Test]
    public async Task RegisterUser_WhenValidStudent_ShouldNormalizeEmailHashPasswordAndPersist()
    {
        var useCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);
        var command = new RegisterUserCommand("  Student.One@ICTU.edu.vn ", "Password123", "student");

        var result = await useCase.ExecuteAsync(command);

        var savedUser = await _userRepository.GetByEmailAsync(EmailAddress.Create("student.one@ictu.edu.vn"));

        Assert.Multiple(() =>
        {
            Assert.That(result.UserId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(result.Email, Is.EqualTo("student.one@ictu.edu.vn"));
            Assert.That(result.Role, Is.EqualTo("STUDENT"));
            Assert.That(result.CreatedAt, Is.EqualTo(FixedNow));
            Assert.That(savedUser, Is.Not.Null);
            Assert.That(savedUser!.PasswordHash, Is.EqualTo("HASHED::Password123"));
            Assert.That(_unitOfWork.SaveChangesCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RegisterUser_WhenDuplicateEmail_ShouldThrowDuplicateEmailException()
    {
        var useCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);
        await useCase.ExecuteAsync(new RegisterUserCommand("user@example.com", "Password123", "STUDENT"));

        Assert.ThrowsAsync<DuplicateEmailException>(async () =>
            await useCase.ExecuteAsync(new RegisterUserCommand("USER@example.com", "Password456", "LANDLORD")));
    }

    [Test]
    public void RegisterUser_WhenRoleIsAdmin_ShouldThrowAdminSelfRegistrationForbiddenException()
    {
        var useCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);

        Assert.ThrowsAsync<AdminSelfRegistrationForbiddenException>(async () =>
            await useCase.ExecuteAsync(new RegisterUserCommand("admin@example.com", "Password123", "ADMIN")));
    }

    [Test]
    public void RegisterUser_WhenRoleIsInvalid_ShouldThrowInvalidRoleException()
    {
        var useCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);

        Assert.ThrowsAsync<InvalidRoleException>(async () =>
            await useCase.ExecuteAsync(new RegisterUserCommand("user@example.com", "Password123", "GUEST")));
    }

    [TestCase("short1")]
    [TestCase("allletterswithoutdigits")]
    [TestCase("1234567890")]
    public void RegisterUser_WhenPasswordViolatesPolicy_ShouldThrowRequestValidationException(string weakPassword)
    {
        var useCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await useCase.ExecuteAsync(new RegisterUserCommand("user@example.com", weakPassword, "STUDENT")));
    }

    [Test]
    public async Task LoginWithEmail_WhenCredentialsValid_ShouldIssueTokensPersistRefreshTokenAndUpdateLastLogin()
    {
        var registerUseCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);
        await registerUseCase.ExecuteAsync(new RegisterUserCommand("landlord@example.com", "Password123", "LANDLORD"));

        var loginTime = FixedNow.AddMinutes(10);
        _timeProvider.AdvanceTo(loginTime);

        var loginUseCase = new LoginWithEmailUseCase(
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await loginUseCase.ExecuteAsync(new LoginWithEmailCommand("LANDLORD@example.com", "Password123"));

        var user = await _userRepository.GetByEmailAsync(EmailAddress.Create("landlord@example.com"));
        var persistedRefresh = await _refreshTokenRepository.GetByTokenHashAsync("HASH_OF_raw-refresh-1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Email, Is.EqualTo("landlord@example.com"));
            Assert.That(result.Role, Is.EqualTo("LANDLORD"));
            Assert.That(result.AccessToken, Is.EqualTo("access-token-1"));
            Assert.That(result.RefreshToken, Is.EqualTo("raw-refresh-1"));
            Assert.That(user!.LastLoginAt, Is.EqualTo(loginTime));
            Assert.That(persistedRefresh, Is.Not.Null);
            Assert.That(persistedRefresh!.UserId, Is.EqualTo(user.Id));
            Assert.That(persistedRefresh.IsActive(loginTime), Is.True);
        });
    }

    [Test]
    public async Task LoginWithEmail_WhenWrongPasswordOrNonExistentEmail_ShouldReturnIdenticalGenericError()
    {
        var registerUseCase = new RegisterUserUseCase(_userRepository, _passwordHasher, _unitOfWork, _timeProvider);
        await registerUseCase.ExecuteAsync(new RegisterUserCommand("student@example.com", "Password123", "STUDENT"));

        var loginUseCase = new LoginWithEmailUseCase(
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var wrongPasswordEx = Assert.ThrowsAsync<AuthenticationFailedException>(async () =>
            await loginUseCase.ExecuteAsync(new LoginWithEmailCommand("student@example.com", "WrongPassword123")));

        var unknownEmailEx = Assert.ThrowsAsync<AuthenticationFailedException>(async () =>
            await loginUseCase.ExecuteAsync(new LoginWithEmailCommand("nonexistent@example.com", "WrongPassword123")));

        Assert.That(wrongPasswordEx!.Message, Is.EqualTo(unknownEmailEx!.Message));
    }

    [Test]
    public async Task LoginWithEmail_WhenUserInactive_ShouldThrowUserInactiveException()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("inactive@example.com"),
            _passwordHasher.HashPassword("Password123"),
            UserRole.Student,
            FixedNow);
        user.Deactivate(FixedNow.AddMinutes(1));
        await _userRepository.AddAsync(user);

        var loginUseCase = new LoginWithEmailUseCase(
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<UserInactiveException>(async () =>
            await loginUseCase.ExecuteAsync(new LoginWithEmailCommand("inactive@example.com", "Password123")));
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void AdvanceTo(DateTimeOffset utcNow) => _utcNow = utcNow;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"HASHED::{password}";

    public bool VerifyPassword(string password, string passwordHash) =>
        passwordHash == $"HASHED::{password}";
}

internal sealed class FakeTokenService : ITokenService
{
    private int _counter;

    public GeneratedAccessToken GenerateAccessToken(User user, DateTimeOffset utcNow)
    {
        _counter++;
        return new GeneratedAccessToken($"access-token-{_counter}", utcNow.AddMinutes(15));
    }

    public GeneratedRefreshToken GenerateRefreshToken(DateTimeOffset utcNow)
    {
        var raw = $"raw-refresh-{_counter}";
        return new GeneratedRefreshToken(raw, HashRefreshToken(raw), utcNow.AddDays(7));
    }

    public string HashRefreshToken(string rawRefreshToken) => $"HASH_OF_{rawRefreshToken}";
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public int TransactionCallCount { get; private set; }

    public Exception? SimulatedSaveChangesException { get; set; }

    public Action? OnSaveChanges { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnSaveChanges?.Invoke();
        if (SimulatedSaveChangesException is not null)
        {
            throw SimulatedSaveChangesException;
        }

        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCallCount++;
        await operation(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        TransactionCallCount++;
        return await operation(cancellationToken);
    }
}

internal sealed class InMemoryFakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetByExternalLoginAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        var normProvider = provider.Trim().ToUpperInvariant();
        var normSub = providerSubject.Trim();
        var match = _users.FirstOrDefault(u =>
            u.ExternalLogins.Any(el => el.Provider == normProvider && el.ProviderSubject == normSub));
        return Task.FromResult(match);
    }

    public Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Email == email));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public void Update(User user)
    {
    }
}

internal sealed class InMemoryFakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public IReadOnlyList<RefreshToken> AllTokens => _tokens;

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetByFamilyAsync(
        Guid tokenFamily,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            _tokens.Where(t => t.TokenFamily == tokenFamily).ToList());

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        _tokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public void Update(RefreshToken refreshToken)
    {
    }
}
