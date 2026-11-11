using System.Globalization;

namespace RoomToRentRadar.Models;

public record Campus(string Id, string Name, double Latitude, double Longitude);
public enum ListingStatus { Available, Rented }
public record RoomListing(int Id, string Title, decimal MonthlyRent, double Area,
    double Latitude, double Longitude, ListingStatus Status, bool IsVerified);
public record RoomListingItem(RoomListing Room, double DistanceKm)
{
    public string Title => Room.Title;
    public string ReferenceText => $"Phòng cho thuê · Mã RR-{Room.Id:0000}";
    public string Illustration => $"room_{Room.Id % 3 + 1}.png";
    public string RentText => $"{Room.MonthlyRent.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ/tháng";
    public string RentAmountText => $"{Room.MonthlyRent.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ";
    public string AreaText => $"{Room.Area:0.#} m²";
    public string DistanceText => DistanceKm < 1 ? $"{DistanceKm * 1000:0} m đến campus" : $"{DistanceKm:0.00} km đến campus";
    public bool IsVerified => Room.IsVerified;
}
public record ListingPage(IReadOnlyList<RoomListingItem> Items, bool HasMore);
