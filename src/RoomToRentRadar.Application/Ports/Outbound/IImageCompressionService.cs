using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IImageCompressionService
{
    Task<ProcessedImageResult> ProcessAsync(
        byte[] rawBytes,
        string declaredContentType,
        bool is360,
        CancellationToken cancellationToken = default);
}
