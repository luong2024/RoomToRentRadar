using RoomToRentRadar.Application.UseCases.Authentication;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests.Authentication;

public class AccountSecurityValidatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private InMemoryFakeUserRepository _userRepository = null!;
    private AccountSecurityValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryFakeUserRepository();
        _validator = new AccountSecurityValidator(_userRepository);
    }

    [Test]
    public async Task ValidateTokenSecurityStateAsync_WhenUserActiveAndStampAndRoleMatch_ShouldReturnTrue()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var isValid = await _validator.ValidateTokenSecurityStateAsync(
            user.Id,
            user.SecurityStamp,
            "STUDENT");

        Assert.That(isValid, Is.True);
    }

    [Test]
    public async Task ValidateTokenSecurityStateAsync_WhenUserDeactivated_ShouldReturnFalse()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        var originalStamp = user.SecurityStamp;
        user.Deactivate(FixedNow.AddMinutes(5));
        await _userRepository.AddAsync(user);

        var isValidWithOldStamp = await _validator.ValidateTokenSecurityStateAsync(
            user.Id,
            originalStamp,
            "STUDENT");

        var isValidWithNewStamp = await _validator.ValidateTokenSecurityStateAsync(
            user.Id,
            user.SecurityStamp,
            "STUDENT");

        Assert.Multiple(() =>
        {
            Assert.That(isValidWithOldStamp, Is.False);
            Assert.That(isValidWithNewStamp, Is.False);
        });
    }

    [Test]
    public async Task ValidateTokenSecurityStateAsync_WhenPasswordChanged_OldStampShouldReturnFalse()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "old-hash",
            UserRole.Student,
            FixedNow);
        var oldStamp = user.SecurityStamp;
        user.ChangePassword("new-hash", FixedNow.AddMinutes(10));
        await _userRepository.AddAsync(user);

        var oldTokenValid = await _validator.ValidateTokenSecurityStateAsync(user.Id, oldStamp, "STUDENT");
        var newTokenValid = await _validator.ValidateTokenSecurityStateAsync(user.Id, user.SecurityStamp, "STUDENT");

        Assert.Multiple(() =>
        {
            Assert.That(oldTokenValid, Is.False);
            Assert.That(newTokenValid, Is.True);
        });
    }

    [Test]
    public async Task ValidateTokenSecurityStateAsync_WhenRoleClaimDoesNotMatchDatabaseRole_ShouldReturnFalse()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("student@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(user);

        var isValid = await _validator.ValidateTokenSecurityStateAsync(
            user.Id,
            user.SecurityStamp,
            "ADMIN");

        Assert.That(isValid, Is.False);
    }
}
