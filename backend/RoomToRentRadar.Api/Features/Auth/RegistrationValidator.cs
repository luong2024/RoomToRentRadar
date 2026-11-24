using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class RegistrationValidator
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static bool TryCreate(
        RegisterRequest request,
        out RegisterUserCommand command,
        out ProblemDetails? problem)
    {
        command = null!;
        problem = null;

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > AuthLimits.EmailMaxLength || !EmailValidator.IsValid(email))
        {
            problem = AuthProblemFactory.InvalidEmailProblem(
                $"email is required, must be a valid address and at most {AuthLimits.EmailMaxLength} characters.");
            return false;
        }

        var password = request.Password ?? string.Empty;
        if (password.Length < AuthLimits.PasswordMinLength
            || password.Length > AuthLimits.PasswordMaxLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            problem = AuthProblemFactory.WeakPasswordProblem();
            return false;
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is 0 or > AuthLimits.DisplayNameMaxLength)
        {
            problem = AuthProblemFactory.InvalidDisplayNameProblem(
                $"display_name is required and must contain at most {AuthLimits.DisplayNameMaxLength} characters.");
            return false;
        }

        if (!UserRoleWire.TryParse(request.Role?.Trim().ToLowerInvariant(), out var role)
            || role is not (UserRole.Student or UserRole.Landlord))
        {
            problem = AuthProblemFactory.InvalidRoleProblem();
            return false;
        }

        command = new RegisterUserCommand(email, password, displayName, role);
        return true;
    }
}
