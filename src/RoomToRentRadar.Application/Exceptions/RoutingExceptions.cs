namespace RoomToRentRadar.Application.Exceptions;

public sealed class RouteNotFoundException : NotFoundException
{
    public RouteNotFoundException()
        : base("No route could be found between the specified locations.")
    {
    }
}

public sealed class RoutingProviderUnavailableException : Exception
{
    public RoutingProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class RoutingProviderTimeoutException : Exception
{
    public RoutingProviderTimeoutException(Exception? innerException = null)
        : base("The routing provider timed out while calculating the route.", innerException)
    {
    }
}
