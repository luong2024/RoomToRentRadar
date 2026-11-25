using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class RegisterValidator
{
    public static bool TryValidate(RegisterRequest? request, out ProblemDetails? problem)
    {
        problem = null;

        if (request is null)
        {
            problem = AuthProblemFactory.InvalidRegistrationRequestProblem(
                "A registration request body is required.");
            return false;
        }

        if (!AccountRules.IsEmailValid(request.Email))
        {
            problem = AuthProblemFactory.InvalidRegistrationRequestProblem(
                "A valid email address is required.");
            return false;
        }

        if (!AccountRules.IsDisplayNameValid(request.DisplayName))
        {
            problem = AuthProblemFactory.InvalidRegistrationRequestProblem(
                $"Display name is required and must be at most {AccountRules.DisplayNameMaxLength} characters.");
            return false;
        }

        if (!AccountRules.IsPasswordValid(request.Password))
        {
            problem = AuthProblemFactory.InvalidRegistrationRequestProblem(
                $"Password must be {AccountRules.PasswordMinLength}-{AccountRules.PasswordMaxLength} characters long and contain both letters and digits.");
            return false;
        }

        var role = request.Role?.Trim().ToLowerInvariant();
        if (!RegistrationRoles.IsValid(role))
        {
            problem = AuthProblemFactory.UnsupportedRegistrationRoleProblem();
            return false;
        }

        return true;
    }
}
