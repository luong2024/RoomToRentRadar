namespace RoomToRentRadar.Features.Profile.Services;

/// <summary>
/// Holds an unsaved avatar only for the authenticated user who selected it.
/// A different session can never retrieve the pending value.
/// </summary>
internal sealed class PendingProfileAvatar
{
    private Guid? _ownerUserId;
    private string? _data;

    public void Set(Guid ownerUserId, string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        _ownerUserId = ownerUserId;
        _data = data;
    }

    public string? GetFor(Guid currentUserId)
    {
        if (_ownerUserId != currentUserId)
        {
            Clear();
            return null;
        }

        return _data;
    }

    public void Clear()
    {
        _ownerUserId = null;
        _data = null;
    }
}
