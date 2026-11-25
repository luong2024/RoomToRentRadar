using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Moderation;

public sealed class ListingModerationService(
    IListingRepository listingRepository,
    IModerationAuditRepository auditRepository) : IListingModerationService
{
    private static readonly ConditionalWeakTable<IListingRepository, ConcurrentDictionary<string, SemaphoreSlim>> RepositoryLocks = new();

    public async Task<ModerationQueueResponse> GetModerationQueueAsync(
        string? moderationStatus = null,
        string? campusId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = NormalizeModerationFilter(moderationStatus);
        var all = (await listingRepository.GetAllCanonicalAsync(cancellationToken))
            .Where(l => !string.Equals(l.Status, ListingAvailabilityStatus.Archived, StringComparison.OrdinalIgnoreCase))
            .Where(l => string.IsNullOrWhiteSpace(campusId)
                        || string.Equals(l.CampusId, campusId.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        int pendingCount = all.Count(l => string.Equals(l.ModerationStatus, ListingModerationStatus.PendingReview, StringComparison.OrdinalIgnoreCase));
        int approvedCount = all.Count(l => string.Equals(l.ModerationStatus, ListingModerationStatus.Approved, StringComparison.OrdinalIgnoreCase));
        int rejectedCount = all.Count(l => string.Equals(l.ModerationStatus, ListingModerationStatus.Rejected, StringComparison.OrdinalIgnoreCase));

        IEnumerable<ListingDocument> filtered = string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase)
            ? all
            : all.Where(l => string.Equals(l.ModerationStatus, filter, StringComparison.OrdinalIgnoreCase));

        // Hàng đợi chờ duyệt ưu tiên tin cũ trước (FIFO), các trạng thái khác mới nhất trước.
        var ordered = string.Equals(filter, ListingModerationStatus.PendingReview, StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderBy(l => l.UpdatedAt).ToList()
            : filtered.OrderByDescending(l => l.UpdatedAt).ToList();

        var latestAudits = await auditRepository.GetLatestByListingIdsAsync(
            ordered.Select(l => l.Id),
            cancellationToken);

        return new ModerationQueueResponse
        {
            ModerationStatusFilter = filter,
            TotalCount = ordered.Count,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount,
            Items = ordered.Select(l => ToModerationItem(l, latestAudits.GetValueOrDefault(l.Id)?.Note)).ToList()
        };
    }

    public async Task<ModerationDecisionResponse> ModerateListingAsync(
        string listingId,
        string moderatorId,
        ModerationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            throw new ListingManagementValidationException("Mã phòng (listingId) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            throw new ListingManagementValidationException("Thiếu mã người kiểm duyệt.");
        }

        if (request is null)
        {
            throw new ListingManagementValidationException("Dữ liệu kiểm duyệt không được để trống.");
        }

        bool isApprove = ParseDecision(request.Decision);
        var trimmedNote = request.Note?.Trim() ?? string.Empty;

        if (!isApprove && string.IsNullOrWhiteSpace(trimmedNote))
        {
            throw new ListingManagementValidationException("Bắt buộc nhập lý do (note) khi từ chối tin đăng.");
        }

        var normalizedId = listingId.Trim();
        var gate = GetListingLock(normalizedId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var listing = await listingRepository.GetByIdAsync(normalizedId, cancellationToken)
                ?? throw new ListingManagementNotFoundException(normalizedId);

            if (string.Equals(listing.Status, ListingAvailabilityStatus.Archived, StringComparison.OrdinalIgnoreCase))
            {
                throw new ListingManagementValidationException("Không thể kiểm duyệt tin đăng đã bị lưu trữ (Archived).");
            }

            var prevModerationStatus = listing.ModerationStatus;
            var prevStatus = listing.Status;
            var now = DateTimeOffset.UtcNow;

            if (isApprove)
            {
                listing.ModerationStatus = ListingModerationStatus.Approved;
                listing.IsVerified = true;
                if (string.Equals(listing.Status, ListingAvailabilityStatus.PendingReview, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(listing.Status, ListingAvailabilityStatus.Rejected, StringComparison.OrdinalIgnoreCase))
                {
                    listing.Status = ListingAvailabilityStatus.Available;
                }

                if (string.IsNullOrWhiteSpace(trimmedNote))
                {
                    trimmedNote = "Đã kiểm duyệt và xác minh thông tin phòng trọ.";
                }
            }
            else
            {
                listing.ModerationStatus = ListingModerationStatus.Rejected;
                listing.IsVerified = false;
                listing.Status = ListingAvailabilityStatus.Rejected;
            }

            listing.UpdatedAt = now;
            await listingRepository.UpsertAsync(listing, cancellationToken);

            var auditEntry = new ModerationAuditDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                ListingId = listing.Id,
                ModeratorId = moderatorId.Trim(),
                Action = isApprove ? "Approve" : "Reject",
                PreviousModerationStatus = prevModerationStatus,
                NewModerationStatus = listing.ModerationStatus,
                PreviousStatus = prevStatus,
                NewStatus = listing.Status,
                Note = trimmedNote,
                CreatedAt = now
            };
            await auditRepository.AddAsync(auditEntry, cancellationToken);

            return new ModerationDecisionResponse
            {
                ListingId = listing.Id,
                PreviousModerationStatus = prevModerationStatus,
                ModerationStatus = listing.ModerationStatus,
                Status = listing.Status,
                IsVerified = listing.IsVerified,
                Note = trimmedNote,
                ModeratedAt = now,
                AuditLog = ToAuditDto(auditEntry)
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ModerationAuditLogDto>> GetModerationHistoryAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            throw new ListingManagementValidationException("Mã phòng (listingId) không được để trống.");
        }

        var normalizedId = listingId.Trim();
        var listing = await listingRepository.GetByIdAsync(normalizedId, cancellationToken)
            ?? throw new ListingManagementNotFoundException(normalizedId);

        var logs = await auditRepository.GetByListingIdAsync(listing.Id, cancellationToken);
        return logs.Select(ToAuditDto).ToList();
    }

    public async Task<LandlordListingsResponse> GetLandlordListingsAsync(
        string landlordId,
        string? status = null,
        string? moderationStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new ListingManagementValidationException("Mã chủ trọ (landlordId) không được để trống.");
        }

        var normalizedLandlordId = landlordId.Trim();
        var ownListings = (await listingRepository.GetAllCanonicalAsync(cancellationToken))
            .Where(l => string.Equals(l.Landlord.Id, normalizedLandlordId, StringComparison.OrdinalIgnoreCase))
            .Where(l => !string.Equals(l.Status, ListingAvailabilityStatus.Archived, StringComparison.OrdinalIgnoreCase))
            .ToList();

        IEnumerable<ListingDocument> filtered = ownListings;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusTrimmed = status.Trim();
            filtered = filtered.Where(l => string.Equals(l.Status, statusTrimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(moderationStatus))
        {
            var modTrimmed = moderationStatus.Trim();
            filtered = filtered.Where(l => string.Equals(l.ModerationStatus, modTrimmed, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered.OrderByDescending(l => l.UpdatedAt).ToList();
        var latestAudits = await auditRepository.GetLatestByListingIdsAsync(
            ordered.Select(l => l.Id),
            cancellationToken);

        return new LandlordListingsResponse
        {
            LandlordId = normalizedLandlordId,
            TotalCount = ordered.Count,
            PendingReviewCount = ownListings.Count(l => string.Equals(l.Status, ListingAvailabilityStatus.PendingReview, StringComparison.OrdinalIgnoreCase)),
            AvailableCount = ownListings.Count(l => string.Equals(l.Status, ListingAvailabilityStatus.Available, StringComparison.OrdinalIgnoreCase)),
            RentedCount = ownListings.Count(l => string.Equals(l.Status, ListingAvailabilityStatus.Rented, StringComparison.OrdinalIgnoreCase)),
            HiddenCount = ownListings.Count(l => string.Equals(l.Status, ListingAvailabilityStatus.Hidden, StringComparison.OrdinalIgnoreCase)),
            RejectedCount = ownListings.Count(l => string.Equals(l.Status, ListingAvailabilityStatus.Rejected, StringComparison.OrdinalIgnoreCase)),
            Items = ordered.Select(l => ToModerationItem(l, latestAudits.GetValueOrDefault(l.Id)?.Note)).ToList()
        };
    }

    public async Task<UpdateListingAvailabilityResponse> UpdateLandlordAvailabilityAsync(
        string listingId,
        string landlordId,
        UpdateListingAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            throw new ListingManagementValidationException("Mã phòng (listingId) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new ListingManagementValidationException("Mã chủ trọ (landlordId) không được để trống.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ListingManagementValidationException("Trạng thái mới (status) không được để trống.");
        }

        var targetStatus = NormalizeLandlordTargetStatus(request.Status);
        var normalizedId = listingId.Trim();
        var normalizedLandlordId = landlordId.Trim();

        var gate = GetListingLock(normalizedId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var listing = await listingRepository.GetByIdAsync(normalizedId, cancellationToken)
                ?? throw new ListingManagementNotFoundException(normalizedId);

            if (!string.Equals(listing.Landlord.Id, normalizedLandlordId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ListingManagementForbiddenException(
                    $"Bạn không có quyền chỉnh sửa tin đăng '{listing.Id}'.");
            }

            if (string.Equals(listing.Status, ListingAvailabilityStatus.Archived, StringComparison.OrdinalIgnoreCase))
            {
                throw new ListingManagementValidationException("Tin đăng đã bị xóa/lưu trữ, không thể đổi trạng thái.");
            }

            // US08 AC2: Chủ trọ không được tự chuyển phòng chưa duyệt sang Available.
            if (string.Equals(targetStatus, ListingAvailabilityStatus.Available, StringComparison.OrdinalIgnoreCase)
                && (!listing.IsVerified
                    || !string.Equals(listing.ModerationStatus, ListingModerationStatus.Approved, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ListingManagementValidationException(
                    "Phòng chưa được kiểm duyệt (Approved) nên chưa thể chuyển sang trạng thái Available.");
            }

            var prevStatus = listing.Status;
            listing.Status = targetStatus;
            listing.UpdatedAt = DateTimeOffset.UtcNow;

            await listingRepository.UpsertAsync(listing, cancellationToken);

            return new UpdateListingAvailabilityResponse
            {
                ListingId = listing.Id,
                LandlordId = listing.Landlord.Id,
                PreviousStatus = prevStatus,
                Status = listing.Status,
                ModerationStatus = listing.ModerationStatus,
                IsVerified = listing.IsVerified,
                UpdatedAt = listing.UpdatedAt
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ArchiveListingResponse> ArchiveLandlordListingAsync(
        string listingId,
        string landlordId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listingId))
        {
            throw new ListingManagementValidationException("Mã phòng (listingId) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(landlordId))
        {
            throw new ListingManagementValidationException("Mã chủ trọ (landlordId) không được để trống.");
        }

        var normalizedId = listingId.Trim();
        var normalizedLandlordId = landlordId.Trim();

        var gate = GetListingLock(normalizedId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var listing = await listingRepository.GetByIdAsync(normalizedId, cancellationToken)
                ?? throw new ListingManagementNotFoundException(normalizedId);

            if (!string.Equals(listing.Landlord.Id, normalizedLandlordId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ListingManagementForbiddenException(
                    $"Bạn không có quyền xóa tin đăng '{listing.Id}'.");
            }

            listing.Status = ListingAvailabilityStatus.Archived;
            listing.IsVerified = false;
            listing.UpdatedAt = DateTimeOffset.UtcNow;

            await listingRepository.UpsertAsync(listing, cancellationToken);

            return new ArchiveListingResponse
            {
                ListingId = listing.Id,
                LandlordId = listing.Landlord.Id,
                Status = listing.Status,
                IsVerified = listing.IsVerified,
                ArchivedAt = listing.UpdatedAt
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetListingLock(string listingId)
    {
        var locks = RepositoryLocks.GetValue(
            listingRepository,
            static _ => new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase));
        return locks.GetOrAdd(listingId, static _ => new SemaphoreSlim(1, 1));
    }

    private static string NormalizeModerationFilter(string? moderationStatus)
    {
        if (string.IsNullOrWhiteSpace(moderationStatus))
        {
            return ListingModerationStatus.PendingReview;
        }

        return moderationStatus.Trim().ToLowerInvariant() switch
        {
            "pending" or "pendingreview" => ListingModerationStatus.PendingReview,
            "approved" => ListingModerationStatus.Approved,
            "rejected" => ListingModerationStatus.Rejected,
            "all" => "All",
            _ => throw new ListingManagementValidationException(
                $"Trạng thái lọc kiểm duyệt '{moderationStatus}' không hợp lệ. Chỉ chấp nhận PendingReview, Approved, Rejected hoặc All.")
        };
    }

    private static bool ParseDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ListingManagementValidationException("Quyết định kiểm duyệt (decision) không được để trống.");
        }

        return decision.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => true,
            "reject" or "rejected" => false,
            _ => throw new ListingManagementValidationException(
                $"Quyết định '{decision}' không hợp lệ. Chỉ chấp nhận 'Approve' hoặc 'Reject'.")
        };
    }

    private static string NormalizeLandlordTargetStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "available" => ListingAvailabilityStatus.Available,
            "rented" => ListingAvailabilityStatus.Rented,
            "hidden" => ListingAvailabilityStatus.Hidden,
            _ => throw new ListingManagementValidationException(
                $"Trạng thái '{status}' không hợp lệ. Chủ trọ chỉ được chuyển sang Available, Rented hoặc Hidden.")
        };

    private static ModerationListingItemDto ToModerationItem(ListingDocument listing, string? latestNote) => new()
    {
        ListingId = listing.Id,
        CampusId = listing.CampusId,
        Title = listing.Title,
        LandlordId = listing.Landlord.Id,
        LandlordName = listing.Landlord.Name,
        Rent = listing.Rent,
        Area = listing.Area,
        RoomType = listing.RoomType,
        Address = listing.Address,
        Status = listing.Status,
        IsVerified = listing.IsVerified,
        ModerationStatus = listing.ModerationStatus,
        PhotoCount = listing.Photos.Count,
        LatestModerationNote = latestNote,
        UpdatedAt = listing.UpdatedAt
    };

    private static ModerationAuditLogDto ToAuditDto(ModerationAuditDocument doc) => new()
    {
        Id = doc.Id,
        ListingId = doc.ListingId,
        ModeratorId = doc.ModeratorId,
        Action = doc.Action,
        PreviousModerationStatus = doc.PreviousModerationStatus,
        NewModerationStatus = doc.NewModerationStatus,
        PreviousStatus = doc.PreviousStatus,
        NewStatus = doc.NewStatus,
        Note = doc.Note,
        CreatedAt = doc.CreatedAt
    };
}
