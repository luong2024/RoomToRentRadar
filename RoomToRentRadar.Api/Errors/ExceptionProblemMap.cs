// ADR-012, system-architecture 7.2: single exception -> (status, errorCode, title) table. A rule not in the docs does not exist.
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadar.Api.Errors;

public sealed record ProblemMapping(int Status, string Title, string ErrorCode);

public static class ExceptionProblemMap
{
    // R15
    public static ProblemMapping InternalError { get; } =
        new(StatusCodes.Status500InternalServerError, "Internal Server Error", ProblemCodes.InternalError);

    public static ProblemMapping? Resolve(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            // R1: duplicate room is a conflict, not a generic validation 400.
            DomainValidationException { ErrorCode: ProblemCodes.DuplicateRoom } =>
                new(StatusCodes.Status409Conflict, "Conflict", ProblemCodes.DuplicateRoom),

            // R2
            DomainValidationException { ErrorCode: { } errorCode } =>
                new(StatusCodes.Status400BadRequest, "Validation Error", errorCode),

            // R3
            DomainValidationException =>
                new(StatusCodes.Status400BadRequest, "Validation Error", ProblemCodes.ValidationFailed),

            // R4
            InvalidCredentialsException =>
                new(StatusCodes.Status401Unauthorized, "Authentication Failed", ProblemCodes.InvalidCredentials),

            // R5
            InvalidTokenException =>
                new(StatusCodes.Status401Unauthorized, "Authentication Failed", ProblemCodes.InvalidToken),

            // R6
            AccountLockedException =>
                new(StatusCodes.Status403Forbidden, "Account Locked", ProblemCodes.AccountLocked),

            // R7
            ForbiddenRoleException =>
                new(StatusCodes.Status403Forbidden, "Access Forbidden", ProblemCodes.ForbiddenRole),

            // R8
            LandlordNotApprovedException =>
                new(StatusCodes.Status403Forbidden, "Access Forbidden", ProblemCodes.LandlordNotApproved),

            // R9: generic code on purpose (anti-IDOR); never confirm that a user id exists.
            UserNotFoundException =>
                new(StatusCodes.Status404NotFound, "Not Found", ProblemCodes.NotFound),

            // R10
            EntityNotFoundException entityNotFound =>
                new(StatusCodes.Status404NotFound, "Not Found", entityNotFound.ErrorCode),

            // R11
            DuplicateEmailException =>
                new(StatusCodes.Status409Conflict, "Conflict", ProblemCodes.EmailTaken),

            // R12
            ConcurrencyConflictException =>
                new(StatusCodes.Status409Conflict, "Conflict", ProblemCodes.ConcurrencyConflict),

            // R13
            InvalidStateTransitionException stateTransition =>
                new(StatusCodes.Status409Conflict, "Conflict", stateTransition.ErrorCode ?? ProblemCodes.InvalidStateTransition),

            // R14
            LimitExceededException limitExceeded =>
                new(StatusCodes.Status409Conflict, "Conflict", limitExceeded.ErrorCode ?? ProblemCodes.LimitExceeded),

            _ => null
        };
    }
}