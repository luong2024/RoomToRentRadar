using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.HouseRules;

internal static class HouseRulesMappingExtensions
{
    public static RoomHouseRulesDto ToDto(this RoomHouseRules rules) =>
        new(
            RoomId: rules.RoomId,
            LandlordId: rules.LandlordId,
            Version: rules.CurrentVersion,
            QuietHours: rules.CurrentPolicy.QuietHours,
            GuestPolicy: rules.CurrentPolicy.GuestPolicy,
            CookingRules: rules.CurrentPolicy.CookingRules,
            ParkingRules: rules.CurrentPolicy.ParkingRules,
            AdditionalRules: rules.CurrentPolicy.AdditionalRules,
            UpdatedAtUtc: rules.UpdatedAtUtc,
            UpdatedByUserId: rules.UpdatedByUserId,
            IsLegacyFallback: false);

    public static RoomHouseRulesDto CreateLegacyFallbackDto(Room room) =>
        new(
            RoomId: room.Id,
            LandlordId: room.LandlordId,
            Version: 0,
            QuietHours: "Not specified",
            GuestPolicy: "Not specified",
            CookingRules: "Not specified",
            ParkingRules: "Not specified",
            AdditionalRules: room.HouseRules ?? [],
            UpdatedAtUtc: room.LastUpdatedDate,
            UpdatedByUserId: room.LandlordId,
            IsLegacyFallback: true);

    public static HouseRulesVersionDto ToDto(this HouseRulesRevision revision) =>
        new(
            RoomId: revision.RoomId,
            Version: revision.Version,
            QuietHours: revision.Policy.QuietHours,
            GuestPolicy: revision.Policy.GuestPolicy,
            CookingRules: revision.Policy.CookingRules,
            ParkingRules: revision.Policy.ParkingRules,
            AdditionalRules: revision.Policy.AdditionalRules,
            ChangeNote: revision.ChangeNote,
            UpdatedAtUtc: revision.UpdatedAtUtc,
            UpdatedByUserId: revision.UpdatedByUserId);
}
