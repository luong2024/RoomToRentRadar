namespace RoomToRentRadar.Services.Media;

public interface IAvatarPicker
{
    /// <summary>Opens the device gallery and stores the chosen image, returning its local path.</summary>
    Task<string?> PickAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
}
