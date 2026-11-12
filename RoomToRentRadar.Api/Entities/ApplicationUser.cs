using Microsoft.AspNetCore.Identity;

namespace RoomToRentRadar.Api.Entities;

/// <summary>
/// Identity user for RoomToRentRadar (US01 / T01.1).
/// Uses an int primary key so the "User ID" claim in the JWT matches
/// UserAccount.Id (int) used by the MAUI app.
/// Email, PasswordHash and PhoneNumber come from IdentityUser; the role is stored
/// in Identity's AspNetUserRoles table (see AppRoles), not as a column here.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>Display name, e.g. "Nguyễn Văn A".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Campus the user belongs to. Only ICTU for now, kept for parity with UserAccount.</summary>
    public string SelectedCampus { get; set; } = "ICTU";

    /// <summary>Account creation time, stored in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
