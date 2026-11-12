using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Inbound.WebApi.Authorization;

public static class AuthorizationPolicies
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string StudentOnly = "StudentOnly";
    public const string LandlordOnly = "LandlordOnly";
    public const string AdminOnly = "AdminOnly";

    public static class Roles
    {
        public const string Student = UserRoleExtensions.StudentRoleName;
        public const string Landlord = UserRoleExtensions.LandlordRoleName;
        public const string Admin = UserRoleExtensions.AdminRoleName;
    }
}
