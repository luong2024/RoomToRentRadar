using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Validation;

/// <summary>
/// Photos of one create/update listing payload after BR-MED-01..03 validation: the rows in request
/// order, their normalized ids (used for attach/remove) and whether any unattached photo was added
/// (BR-LST-05 material change → <c>NeedsReReview</c>).
/// </summary>
/// <remarks>
/// Deliberately outside the <c>UseCases</c> namespace: it is a validation result, not a use case, so the
/// SRP architecture rule (one public <c>ExecuteAsync</c> per UseCase class) does not apply to it.
/// </remarks>
internal sealed class ValidatedListingPhotos
{
    public ValidatedListingPhotos(
        IReadOnlyList<RoomPhoto> orderedPhotos,
        IReadOnlyList<string> orderedIds,
        bool hasAddedNewPhotos)
    {
        OrderedPhotos = orderedPhotos;
        OrderedIds = orderedIds;
        HasAddedNewPhotos = hasAddedNewPhotos;
    }

    public IReadOnlyList<RoomPhoto> OrderedPhotos { get; }
    public IReadOnlyList<string> OrderedIds { get; }
    public bool HasAddedNewPhotos { get; }
}
