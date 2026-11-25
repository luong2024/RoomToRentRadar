using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.SqliteDataAccess;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class UserRepositoryTests
{
    private ListingsApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
    }

    [SetUp]
    public Task SetUp() => _factory.ResetDataAsync();

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task TryAddAsync_DuplicateEmail_ReturnsFalseWithoutThrowing()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var first = NewUser("duplicate@example.test");
        var second = NewUser("duplicate@example.test");

        Assert.That(await repository.TryAddAsync(first, CancellationToken.None), Is.True);
        Assert.That(await repository.TryAddAsync(second, CancellationToken.None), Is.False);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(
            await dbContext.Users.CountAsync(user => user.Email == "duplicate@example.test"),
            Is.EqualTo(1));
    }

    private static User NewUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = User.NormalizeEmail(email),
        DisplayName = "Duplicate Tester",
        PasswordHash = "hash",
        PasswordSalt = "salt",
        Role = UserRole.Student,
        CreatedAt = DateTime.UtcNow
    };
}
