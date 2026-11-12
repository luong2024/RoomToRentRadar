using RoomToRentRadar.Api.Entities;

namespace RoomToRentRadar.Api.Services;

/// <summary>
/// Creates signed JWT access tokens for authenticated users (US01 / T01.1, AC2).
/// Kept behind an interface so AuthController can be unit-tested with a fake token service.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Builds a token containing the user's ID and role.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="role">The user's role (Student, Landlord or Admin).</param>
    /// <returns>The encoded token and its expiry time in UTC.</returns>
    (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string role);
}
