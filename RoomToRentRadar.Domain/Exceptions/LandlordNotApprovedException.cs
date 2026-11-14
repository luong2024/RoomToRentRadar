namespace RoomToRentRadar.Domain.Exceptions;

public class LandlordNotApprovedException : Exception
{
    public string UserId { get; }

    public LandlordNotApprovedException(string userId)
        : base($"Landlord account '{userId}' is pending Admin approval and cannot publish listings yet.")
    {
        UserId = userId;
    }
}