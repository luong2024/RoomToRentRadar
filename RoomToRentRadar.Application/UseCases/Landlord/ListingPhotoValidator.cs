using RoomToRentRadar.Application.Validation;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// Shared <c>photoIds</c> validation of createListing / updateListing: every id must be owned by the
/// caller (anti-IDOR), still unattached (or already attached to this very room on update) and younger
/// than 24 h when unattached; the mix must be 3–20 Standard + 0–3 Panorama (BR-MED-01, BR-MED-03).
/// </summary>
internal static class ListingPhotoValidator
{
    public static async Task<ValidatedListingPhotos> ValidateAsync(
        IReadOnlyList<string>? photoIds,
        string landlordId,
        string? roomId,
        IRoomPhotoRepository photoRepository,
        DateTime utcNow,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photoRepository);

        if (photoIds is null || photoIds.Count == 0)
        {
            throw Validation("A listing requires between 3 and 20 standard photos.");
        }

        if (photoIds.Count > PhotoFileRules.MaxStandardPhotos + PhotoFileRules.MaxPanoramaPhotos)
        {
            throw Validation(
                $"A listing can have at most {PhotoFileRules.MaxStandardPhotos + PhotoFileRules.MaxPanoramaPhotos} photos.");
        }

        var normalizedIds = new List<string>(photoIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var photoId in photoIds)
        {
            if (string.IsNullOrWhiteSpace(photoId))
            {
                throw Validation("Photo ids cannot be empty.");
            }

            var normalizedId = photoId.Trim();
            if (!seen.Add(normalizedId))
            {
                throw Validation($"Photo '{normalizedId}' appears more than once.");
            }

            normalizedIds.Add(normalizedId);
        }

        var ownedPhotos = await photoRepository.ListOwnedByIdsAsync(landlordId, normalizedIds, ct).ConfigureAwait(false);
        var photoById = ownedPhotos.ToDictionary(photo => photo.Id, StringComparer.Ordinal);
        var expiryCutoff = utcNow - PhotoFileRules.UnattachedPhotoLifetime;

        var orderedPhotos = new List<RoomPhoto>(normalizedIds.Count);
        var hasAddedNewPhotos = false;
        foreach (var photoId in normalizedIds)
        {
            if (!photoById.TryGetValue(photoId, out var photo))
            {
                // GUARDRAILS §1.5.6: another landlord's photo is indistinguishable from a missing one.
                throw Validation($"Photo '{photoId}' was not found.");
            }

            if (roomId is null)
            {
                if (photo.RoomId is not null)
                {
                    throw Validation($"Photo '{photoId}' is already attached to a room.");
                }
            }
            else if (photo.RoomId is not null && !string.Equals(photo.RoomId, roomId, StringComparison.Ordinal))
            {
                throw Validation($"Photo '{photoId}' is already attached to another room.");
            }

            if (photo.RoomId is null && photo.CreatedAtUtc < expiryCutoff)
            {
                // BR-MED-03: an unattached upload only lives 24 h; the caller must upload it again.
                throw Validation($"Photo '{photoId}' has expired; upload it again.");
            }

            hasAddedNewPhotos |= !string.Equals(photo.RoomId, roomId, StringComparison.Ordinal);
            orderedPhotos.Add(photo);
        }

        var standardCount = orderedPhotos.Count(photo => photo.Kind == PhotoKind.Standard);
        if (standardCount < PhotoFileRules.MinStandardPhotos || standardCount > PhotoFileRules.MaxStandardPhotos)
        {
            throw Validation(
                $"A listing requires between {PhotoFileRules.MinStandardPhotos} and {PhotoFileRules.MaxStandardPhotos} standard photos (found {standardCount}).");
        }

        var panoramaCount = orderedPhotos.Count(photo => photo.Kind == PhotoKind.Panorama);
        if (panoramaCount > PhotoFileRules.MaxPanoramaPhotos)
        {
            throw Validation(
                $"A listing can have at most {PhotoFileRules.MaxPanoramaPhotos} panorama photos (found {panoramaCount}).");
        }

        return new ValidatedListingPhotos(orderedPhotos, normalizedIds, hasAddedNewPhotos);
    }

    private static DomainValidationException Validation(string message) =>
        new(message, ProblemCodes.ValidationFailed);
}
