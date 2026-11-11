using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class GetRoomGalleryUseCase : IGetRoomGalleryUseCase
{
    private readonly IRoomPhotoRepository _repository;

    public GetRoomGalleryUseCase(IRoomPhotoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<RoomGalleryDto> ExecuteAsync(string roomId, CancellationToken ct = default)
    {
        if (!PhotoFileRules.IsValidRoomId(roomId))
        {
            throw new DomainValidationException("Room id is invalid.");
        }

        var photos = await _repository.ListByRoomAsync(roomId, ct);

        var dtos = new List<RoomPhotoDto>(photos.Count);
        for (var index = 0; index < photos.Count; index++)
        {
            var photo = photos[index];
            dtos.Add(new RoomPhotoDto(
                Id: photo.Id,
                Url: photo.Url,
                IsCover: index == 0,
                UploadedAtUtc: photo.UploadedAtUtc));
        }

        return new RoomGalleryDto(
            RoomId: roomId,
            Photos: dtos,
            TotalCount: photos.Count,
            HasMinimumPhotos: photos.Count >= PhotoFileRules.MinPhotosToPublish);
    }
}
