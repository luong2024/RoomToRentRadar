using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Services;

/// <summary>
/// Dịch vụ mô phỏng cập nhật nội quy phòng trọ có tăng version &amp; lưu snapshot lịch sử (T11.2)
/// khi Backend API T11.1 chưa chạy.
/// </summary>
public sealed class MockHouseRulesService : IHouseRulesService
{
    public Task<HouseRulesUpdateResult> UpdateHouseRulesAsync(
        string listingId,
        UpdateHouseRulesRequestContract request,
        HouseRulesResponseContract? currentState = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseVersion = currentState?.Version > 0 ? currentState.Version : 1;
        var history = new List<HouseRulesVersionSnapshotContract>();

        if (currentState is not null)
        {
            history.Add(new HouseRulesVersionSnapshotContract
            {
                Version = currentState.Version,
                UpdatedAt = currentState.UpdatedAt,
                QuietHours = currentState.QuietHours,
                GuestPolicy = currentState.GuestPolicy,
                CookingRules = currentState.CookingRules,
                ParkingRules = currentState.ParkingRules
            });

            if (currentState.History is { Count: > 0 })
            {
                history.AddRange(currentState.History);
            }
        }

        var updated = new HouseRulesResponseContract
        {
            ListingId = string.IsNullOrWhiteSpace(listingId) ? "room-2026-001" : listingId,
            Version = baseVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            QuietHours = request.QuietHours.Trim(),
            GuestPolicy = request.GuestPolicy.Trim(),
            CookingRules = request.CookingRules.Trim(),
            ParkingRules = request.ParkingRules.Trim(),
            History = history
        };

        return Task.FromResult(new HouseRulesUpdateResult(updated, IsFromLiveApi: false));
    }
}
