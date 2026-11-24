namespace RoomToRentRadar.Domain.Exceptions;

public abstract class RoomGalleryDomainException : DomainException
{
    protected RoomGalleryDomainException(string message) : base(message)
    {
    }
}

public sealed class InvalidRoomPhotoException : RoomGalleryDomainException
{
    public InvalidRoomPhotoException(string message) : base(message)
    {
    }
}

public sealed class RoomPhotoNotFoundException : RoomGalleryDomainException
{
    public RoomPhotoNotFoundException(string message) : base(message)
    {
    }
}

public sealed class RoomPhotoQuotaExceededException : RoomGalleryDomainException
{
    public RoomPhotoQuotaExceededException(string message) : base(message)
    {
    }
}

public sealed class RoomGalleryAccessDeniedException : RoomGalleryDomainException
{
    public RoomGalleryAccessDeniedException(string message) : base(message)
    {
    }
}

public sealed class PhotoStorageNotConfiguredException : RoomGalleryDomainException
{
    public PhotoStorageNotConfiguredException(string message) : base(message)
    {
    }
}
