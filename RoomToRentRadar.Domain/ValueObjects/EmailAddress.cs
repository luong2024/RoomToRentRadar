namespace RoomToRentRadar.Domain.ValueObjects;

public sealed record EmailAddress
{
    public const string IctuDomain = "@ictu.edu.vn";

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string rawEmail) =>
        throw new NotImplementedException();

    public bool IsIctuStudentEmail() =>
        throw new NotImplementedException();

    public override string ToString() => Value;
}

