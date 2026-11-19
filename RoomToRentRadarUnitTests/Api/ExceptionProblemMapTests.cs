// ADR-012, system-architecture 7.2: every mapping row R1-R19 has a status + errorCode + title test.
using System.Runtime.CompilerServices;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class ExceptionProblemMapTests
{
    private static IEnumerable<TestCaseData> MappingRows()
    {
        yield return new TestCaseData(
            new DomainValidationException("Duplicate room.", ProblemCodes.DuplicateRoom),
            409, ProblemCodes.DuplicateRoom, "Conflict").SetName("R1_DuplicateRoom_409");
        yield return new TestCaseData(
            new DomainValidationException("Invalid sort option.", ProblemCodes.InvalidSortOption),
            400, ProblemCodes.InvalidSortOption, "Validation Error").SetName("R2_ValidationWithCode_400");
        yield return new TestCaseData(
            new DomainValidationException("missing"),
            400, ProblemCodes.ValidationFailed, "Validation Error").SetName("R3_ValidationWithoutCode_400");
        yield return new TestCaseData(
            new InvalidCredentialsException(),
            401, ProblemCodes.InvalidCredentials, "Authentication Failed").SetName("R4_InvalidCredentials_401");
        yield return new TestCaseData(
            new InvalidTokenException(),
            401, ProblemCodes.InvalidToken, "Authentication Failed").SetName("R5_InvalidToken_401");
        yield return new TestCaseData(
            new AccountLockedException(DateTime.UtcNow.AddMinutes(15)),
            403, ProblemCodes.AccountLocked, "Account Locked").SetName("R6_AccountLocked_403");
        yield return new TestCaseData(
            new ForbiddenRoleException("Role is not allowed."),
            403, ProblemCodes.ForbiddenRole, "Access Forbidden").SetName("R7_ForbiddenRole_403");
        yield return new TestCaseData(
            new LandlordNotApprovedException("landlord-1"),
            403, ProblemCodes.LandlordNotApproved, "Access Forbidden").SetName("R8_LandlordNotApproved_403");
        yield return new TestCaseData(
            new UserNotFoundException("u-1"),
            404, ProblemCodes.NotFound, "Not Found").SetName("R9_UserNotFound_404");
        yield return new TestCaseData(
            new EntityNotFoundException(ProblemCodes.RoomNotFound),
            404, ProblemCodes.RoomNotFound, "Not Found").SetName("R10_EntityNotFound_404");
        yield return new TestCaseData(
            new DuplicateEmailException("user@ictu.edu.vn"),
            409, ProblemCodes.EmailTaken, "Conflict").SetName("R11_DuplicateEmail_409");
        yield return new TestCaseData(
            new ConcurrencyConflictException(),
            409, ProblemCodes.ConcurrencyConflict, "Conflict").SetName("R12_ConcurrencyConflict_409");
        yield return new TestCaseData(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."),
            409, ProblemCodes.InvalidStateTransition, "Conflict").SetName("R13_InvalidStateTransition_409");
        yield return new TestCaseData(
            new LimitExceededException("Too many photos."),
            409, ProblemCodes.LimitExceeded, "Conflict").SetName("R14_LimitExceeded_409");
        yield return new TestCaseData(
            new IfMatchRequiredException(),
            428, ProblemCodes.IfMatchRequired, "Precondition Required").SetName("R16_IfMatchRequired_428");
        yield return new TestCaseData(
            new EtagMismatchException(),
            412, ProblemCodes.EtagMismatch, "Precondition Failed").SetName("R17_EtagMismatch_412");
        yield return new TestCaseData(
            new FileTooLargeException(),
            413, ProblemCodes.FileTooLarge, "Payload Too Large").SetName("R18_FileTooLarge_413");
        yield return new TestCaseData(
            new UnsupportedMediaTypeException(),
            415, ProblemCodes.UnsupportedMediaType, "Unsupported Media Type").SetName("R19_UnsupportedMediaType_415");
    }

    [TestCaseSource(nameof(MappingRows))]
    public void Resolve_MappedException_ReturnsExpectedStatusErrorCodeAndTitle(
        Exception exception,
        int expectedStatus,
        string expectedErrorCode,
        string expectedTitle)
    {
        var mapping = ExceptionProblemMap.Resolve(exception);

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(expectedStatus));
            Assert.That(mapping.ErrorCode, Is.EqualTo(expectedErrorCode));
            Assert.That(mapping.Title, Is.EqualTo(expectedTitle));
        });
    }

    [Test]
    public void Resolve_UnknownException_ReturnsNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ExceptionProblemMap.Resolve(new InvalidOperationException()), Is.Null);
            Assert.That(ExceptionProblemMap.Resolve(new ArgumentException()), Is.Null);
            Assert.That(ExceptionProblemMap.Resolve(new NullReferenceException()), Is.Null);
        });
    }

    [Test]
    public void Resolve_NullException_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ExceptionProblemMap.Resolve(null!));
    }

    [Test]
    public void Resolve_DomainValidationWithDuplicateRoomCode_Is409NotValidation400()
    {
        var mapping = ExceptionProblemMap.Resolve(
            new DomainValidationException("Duplicate room.", ProblemCodes.DuplicateRoom));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(409));
            Assert.That(mapping.Status, Is.Not.EqualTo(400));
            Assert.That(mapping.ErrorCode, Is.EqualTo(ProblemCodes.DuplicateRoom));
        });
    }

    [TestCase(ProblemCodes.InvalidSortOption)]
    [TestCase("SOMETHING_CUSTOM")]
    public void Resolve_DomainValidationWithOtherCode_UsesThatCodeVerbatim(string errorCode)
    {
        var mapping = ExceptionProblemMap.Resolve(new DomainValidationException("Invalid input.", errorCode));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(400));
            Assert.That(mapping.ErrorCode, Is.EqualTo(errorCode));
        });
    }

    [Test]
    public void Resolve_InvalidStateTransitionWithCustomCode_UsesCustomCode()
    {
        var mapping = ExceptionProblemMap.Resolve(
            new InvalidStateTransitionException("Listing is not available.", ProblemCodes.ListingNotAvailable));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(409));
            Assert.That(mapping.ErrorCode, Is.EqualTo(ProblemCodes.ListingNotAvailable));
        });
    }

    [Test]
    public void Resolve_LimitExceededWithCustomCode_UsesCustomCode()
    {
        var mapping = ExceptionProblemMap.Resolve(
            new LimitExceededException("File too large.", ProblemCodes.FileTooLarge));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(409));
            Assert.That(mapping.ErrorCode, Is.EqualTo(ProblemCodes.FileTooLarge));
        });
    }

    [TestCase(ProblemCodes.RoomNotFound)]
    [TestCase(ProblemCodes.CampusNotFound)]
    public void Resolve_EntityNotFound_UsesItsOwnCode(string errorCode)
    {
        var mapping = ExceptionProblemMap.Resolve(new EntityNotFoundException(errorCode));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(404));
            Assert.That(mapping.ErrorCode, Is.EqualTo(errorCode));
        });
    }

    [Test]
    public void Resolve_UserNotFound_IsGenericNotFoundNotUserSpecific()
    {
        var mapping = ExceptionProblemMap.Resolve(new UserNotFoundException("u-1"));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(404));
            Assert.That(mapping.ErrorCode, Is.EqualTo(ProblemCodes.NotFound));
            Assert.That(mapping.Title, Is.EqualTo("Not Found"));
        });
    }

    [Test]
    public void InternalError_Is500InternalError()
    {
        var mapping = ExceptionProblemMap.InternalError;

        Assert.Multiple(() =>
        {
            Assert.That(mapping.Status, Is.EqualTo(500));
            Assert.That(mapping.ErrorCode, Is.EqualTo(ProblemCodes.InternalError));
            Assert.That(mapping.Title, Is.EqualTo("Internal Server Error"));
        });
    }

    [Test]
    public void AllDomainExceptionTypes_HaveAMapRow()
    {
        var exceptionTypes = typeof(DomainValidationException).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.Namespace == "RoomToRentRadar.Domain.Exceptions"
                && typeof(Exception).IsAssignableFrom(type))
            .ToList();

        var missing = exceptionTypes
            .Where(type => ExceptionProblemMap.Resolve((Exception)RuntimeHelpers.GetUninitializedObject(type)) is null)
            .Select(type => type.Name)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(exceptionTypes.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(missing, Is.Empty, $"Missing rows in ExceptionProblemMap: {string.Join(", ", missing)}");
        });
    }

    [Test]
    public void Resolve_EveryMappedErrorCode_IsOneOfProblemCodes()
    {
        Exception[] fixedCodeExceptions =
        [
            new DomainValidationException("Duplicate room.", ProblemCodes.DuplicateRoom),
            new DomainValidationException("missing"),
            new InvalidCredentialsException(),
            new InvalidTokenException(),
            new AccountLockedException(DateTime.UtcNow.AddMinutes(15)),
            new ForbiddenRoleException("Role is not allowed."),
            new LandlordNotApprovedException("landlord-1"),
            new UserNotFoundException("u-1"),
            new DuplicateEmailException("user@ictu.edu.vn"),
            new ConcurrencyConflictException(),
            new InvalidStateTransitionException("Cannot transition."),
            new LimitExceededException("Too many photos."),
            new IfMatchRequiredException(),
            new EtagMismatchException(),
            new FileTooLargeException(),
            new UnsupportedMediaTypeException()
        ];

        Assert.Multiple(() =>
        {
            foreach (var exception in fixedCodeExceptions)
            {
                var errorCode = ExceptionProblemMap.Resolve(exception)?.ErrorCode;
                Assert.That(errorCode, Is.Not.Null, $"{exception.GetType().Name} has no mapping");
                Assert.That(ProblemCodes.All, Does.Contain(errorCode));
            }

            Assert.That(ProblemCodes.All, Does.Contain(ExceptionProblemMap.InternalError.ErrorCode));
        });
    }
}