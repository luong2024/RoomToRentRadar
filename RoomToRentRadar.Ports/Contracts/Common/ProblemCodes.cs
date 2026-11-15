namespace RoomToRentRadar.Ports.Contracts.Common;

/// <summary>Stable UPPER_SNAKE ProblemDetails error codes (ADR-012, docs/api/openapi.yaml); the last four are generic fallbacks.</summary>
public static class ProblemCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidSortOption = "INVALID_SORT_OPTION";
    public const string InvalidPriceRange = "INVALID_PRICE_RANGE";
    public const string InvalidAreaRange = "INVALID_AREA_RANGE";
    public const string InvalidDistanceRange = "INVALID_DISTANCE_RANGE";
    public const string CampusNotFound = "CAMPUS_NOT_FOUND";
    public const string DuplicateRoom = "DUPLICATE_ROOM";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string ForbiddenRole = "FORBIDDEN_ROLE";
    public const string LandlordNotApproved = "LANDLORD_NOT_APPROVED";
    public const string EmailTaken = "EMAIL_TAKEN";
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    public const string ListingNotAvailable = "LISTING_NOT_AVAILABLE";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string AlreadyReported = "ALREADY_REPORTED";
    public const string LimitExceeded = "LIMIT_EXCEEDED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string EtagMismatch = "ETAG_MISMATCH";
    public const string IfMatchRequired = "IF_MATCH_REQUIRED";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string RateLimited = "RATE_LIMITED";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string InternalError = "INTERNAL_ERROR";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        ValidationFailed,
        InvalidSortOption,
        InvalidPriceRange,
        InvalidAreaRange,
        InvalidDistanceRange,
        CampusNotFound,
        DuplicateRoom,
        InvalidCredentials,
        InvalidToken,
        AccountLocked,
        ForbiddenRole,
        LandlordNotApproved,
        EmailTaken,
        RoomNotFound,
        ListingNotAvailable,
        InvalidStateTransition,
        AlreadyReported,
        LimitExceeded,
        ConcurrencyConflict,
        EtagMismatch,
        IfMatchRequired,
        FileTooLarge,
        UnsupportedMediaType,
        RateLimited,
        NotFound,
        Forbidden,
        Unauthorized,
        InternalError
    };
}