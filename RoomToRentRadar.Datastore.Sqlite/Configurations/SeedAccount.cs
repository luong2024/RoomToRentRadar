using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Datastore.Sqlite.Configurations;

/// <summary>
/// Pre-hashed account entry loaded from configuration (<c>Admin:SeedAccounts</c> or
/// development-only <c>DemoData:Accounts</c>) to be seeded idempotently into <c>Users</c>.
/// </summary>
public sealed record SeedAccount(
    string Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsVerified,
    string PasswordHash,
    string? PhoneNumber = null,
    string? StudentCode = null);
