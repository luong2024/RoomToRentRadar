using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Application.Mappers;

public static class UserMapper
{
    public static UserProfileDto ToProfileDto(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserProfileDto(
            Id: user.Id,
            Email: user.Email.Value,
            FullName: user.FullName,
            Role: user.Role.ToString(),
            AuthProvider: user.AuthProvider.ToString(),
            PhoneNumber: user.PhoneNumber,
            StudentCode: user.StudentCode,
            IsVerified: user.IsVerified,
            CreatedAtUtc: user.CreatedAtUtc,
            HouseName: user.HouseName,
            HouseAddress: user.HouseAddress);
    }
}
