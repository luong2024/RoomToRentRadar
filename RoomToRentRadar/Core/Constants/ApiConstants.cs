namespace RoomToRentRadar.Core.Constants;

public static class ApiConstants
{
#if ANDROID
    public const string BaseAddress = "http://10.0.2.2:5080/";
#else
    public const string BaseAddress = "http://localhost:5080/";
#endif
}
