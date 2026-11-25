using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Profile;

/// <summary>T13.4 — updateMyProfile use case (BR-CMP-02, BR-I18N-02).</summary>
[TestFixture]
public class UpdateMyProfileUseCaseTests
{
    private IUserRepository _userRepository = null!;
    private ICampusRepository _campusRepository = null!;
    private UpdateMyProfileUseCase _sut = null!;
    private User _user = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _campusRepository = Substitute.For<ICampusRepository>();
        _sut = new UpdateMyProfileUseCase(_userRepository, _campusRepository);

        _user = User.RegisterLocal(
            "student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, null,
            new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc));
        _userRepository.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _campusRepository.GetByIdAsync("TNUT", Arg.Any<CancellationToken>()).Returns(
            new Campus("TNUT", "Trường Đại học Kỹ thuật Công nghiệp", "TNUT", 21.54954, 105.84350, isDefault: false, sortOrder: 8));
    }

    private static UpdateMyProfileRequest Campus(string? id) => new(null, null, id, null);

    private Task AssertNoWriteAsync() =>
        _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());

    [Test]
    public async Task Execute_ChangeCampus_PersistsAndReturnsNewCampus()
    {
        var result = await _sut.ExecuteAsync(_user.Id, Campus("TNUT"));

        Assert.That(result.SelectedCampusId, Is.EqualTo("TNUT"));
        await _userRepository.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_UnknownCampus_Throws400WithCampusNotFound_NoWrite()
    {
        var ex = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(_user.Id, Campus("NOPE")));

        Assert.That(ex!.ErrorCode, Is.EqualTo("CAMPUS_NOT_FOUND"));
        await AssertNoWriteAsync();
    }

    [Test]
    public async Task Execute_LowercaseCampusId_TreatedAsUnknown()
    {
        // A case-insensitive store must not let "tnut" through: the stored id has to match exactly.
        _campusRepository.GetByIdAsync("tnut", Arg.Any<CancellationToken>()).Returns(
            new Campus("TNUT", "Name", "TNUT", 21.5, 105.8, isDefault: false, sortOrder: 8));

        var ex = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(_user.Id, Campus("tnut")));

        Assert.That(ex!.ErrorCode, Is.EqualTo("CAMPUS_NOT_FOUND"));
        await AssertNoWriteAsync();
    }

    [Test]
    public async Task Execute_CampusIdWithSpaces_Trimmed()
    {
        var result = await _sut.ExecuteAsync(_user.Id, Campus("  TNUT  "));

        Assert.That(result.SelectedCampusId, Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task Execute_BlankCampusId_IsValidationError_NoWrite()
    {
        var ex = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(_user.Id, Campus("   ")));

        Assert.That(ex!.ErrorCode, Is.Null);
        await AssertNoWriteAsync();
    }

    [Test]
    public async Task Execute_EmptyRequest_NoWrite_ReturnsCurrentProfile()
    {
        var result = await _sut.ExecuteAsync(_user.Id, new UpdateMyProfileRequest(null, null, null, null));

        Assert.That(result.SelectedCampusId, Is.EqualTo("ICTU"));
        await AssertNoWriteAsync();
    }

    [Test]
    public void Execute_UserMissing_ThrowsUserNotFound()
    {
        Assert.ThrowsAsync<UserNotFoundException>(
            async () => await _sut.ExecuteAsync("missing", Campus("TNUT")));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Execute_BlankUserId_Throws(string userId)
    {
        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(userId, Campus("TNUT")));
    }

    [Test]
    public void Execute_RepositoryConcurrencyConflict_Propagates()
    {
        _userRepository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException("conflict"));

        Assert.ThrowsAsync<ConcurrencyConflictException>(
            async () => await _sut.ExecuteAsync(_user.Id, Campus("TNUT")));
    }

    [Test]
    public async Task Execute_LanguageOnly_Updates()
    {
        var result = await _sut.ExecuteAsync(_user.Id, new UpdateMyProfileRequest(null, null, null, "vi"));

        Assert.That(result.PreferredLanguage, Is.EqualTo("vi"));
        await _userRepository.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }
}
