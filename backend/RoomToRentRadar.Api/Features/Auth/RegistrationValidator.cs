using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Features.Auth;

public static class RegistrationValidator
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public static bool TryCreate(
        RegisterRequest request,
        out BeginRegistrationCommand command,
        out ProblemDetails? problem)
    {
        command = null!;
        problem = null;

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > AccountRules.EmailMaxLength || !EmailValidator.IsValid(email))
        {
            problem = AuthProblemFactory.InvalidEmailProblem(
                $"email is required, must be a valid address and at most {AccountRules.EmailMaxLength} characters.");
            return false;
        }

        var password = request.Password ?? string.Empty;
        if (password.Length < AccountRules.PasswordMinLength
            || password.Length > AccountRules.PasswordMaxLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            problem = AuthProblemFactory.WeakPasswordProblem();
            return false;
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is 0 or > AccountRules.DisplayNameMaxLength)
        {
            problem = AuthProblemFactory.InvalidDisplayNameProblem(
                $"display_name is required and must contain at most {AccountRules.DisplayNameMaxLength} characters.");
            return false;
        }

        if (!UserRoleWire.TryParse(request.Role?.Trim().ToLowerInvariant(), out var role)
            || !UserRoleWire.IsSelfService(role))
        {
            problem = AuthProblemFactory.InvalidRoleProblem();
            return false;
        }

        command = new BeginRegistrationCommand(email, password, displayName, role);
        return true;
    }

    public static bool TryCreate(
        VerifyRegistrationRequest request,
        out VerifyRegistrationCommand command,
        out ProblemDetails? problem)
    {
        command = null!;
        problem = null;

        var code = request.Code?.Trim();
        if (request.ChallengeId == Guid.Empty || !AccountRules.IsOtpCodeValid(code))
        {
            problem = AuthProblemFactory.InvalidOtpRequestProblem();
            return false;
        }

        command = new VerifyRegistrationCommand(request.ChallengeId, code!);
        return true;
    }
}
