namespace RoomToRentRadar.Datastore.Sqlite.Tables;

public sealed class UserTable
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = string.Empty;
    public string AuthProvider { get; set; } = string.Empty;
    public string? SsoSubjectId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? HouseName { get; set; }
    public string? HouseAddress { get; set; }
    public string? StudentCode { get; set; }
    public bool IsVerified { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

