namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Base exception for house rules domain rule violations.
/// </summary>
public abstract class HouseRulesDomainException : DomainException
{
    protected HouseRulesDomainException(string message)
        : base(message)
    {
    }
}

public sealed class HouseRulesValidationException : HouseRulesDomainException
{
    public HouseRulesValidationException(string message)
        : base(message)
    {
    }
}

public sealed class HouseRulesOwnershipException : HouseRulesDomainException
{
    public HouseRulesOwnershipException(string message)
        : base(message)
    {
    }
}
