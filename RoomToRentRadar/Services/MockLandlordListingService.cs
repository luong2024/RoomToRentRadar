using System;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Dịch vụ mô phỏng đăng tin phòng trọ khi Backend API T08.2 chưa khả dụng.
/// Trả về trạng thái PendingReview (IsVerified = false) đúng theo US08.
/// </summary>
public sealed class MockLandlordListingService : ILandlordListingService
{
    public Task<CreateListingSubmitResult> SubmitListingAsync(
        CreateListingRequestContract request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Photos.Count < 3)
        {
            throw new ArgumentException("Cần tối thiểu 3 ảnh phòng trọ (JPG/PNG <= 10MB).", nameof(request));
        }

        var response = new CreateListingResponseContract
        {
            ListingId = $"room-pending-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
            Status = "PendingReview",
            IsVerified = false,
            ModerationStatus = "PendingReview",
            SubmittedAt = DateTimeOffset.UtcNow,
            Message = "Tin đăng phòng trọ đã được lưu ở trạng thái PendingReview và đang chờ kiểm duyệt viên xác minh."
        };

        return Task.FromResult(new CreateListingSubmitResult(response, IsFromLiveApi: false));
    }
}
