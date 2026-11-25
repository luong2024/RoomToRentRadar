using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RoomToRentRadar.Models;

/// <summary>
/// Model phòng trọ dùng trong màn hình so sánh tối đa 3 phòng (US10 / T10.1).
/// Pure C# để chạy trực tiếp trong RoomToRentRadarUnitTests (net10.0).
/// </summary>
public class RoomComparisonItemModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private bool _isInComparison;
    private bool _isBestRent;
    private bool _isLargestArea;
    private bool _isClosestToIctu;
    private bool _isHighestSecurity;

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ShortTitle { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public decimal Deposit { get; set; }
    public double Area { get; set; }
    public double DistanceMeters { get; set; }
    public string RoomType { get; set; } = "Phòng trọ";
    public string Address { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = true;
    public double SecurityScore { get; set; } = 4.6;
    public string AmenitiesSummary { get; set; } = "Wifi, Nóng lạnh, WC khép kín";
    public string QuietHours { get; set; } = "23:00 - 05:30";

    public decimal PricePerSqm => Area > 0 ? Rent / (decimal)Area : 0m;

    public bool IsInComparison
    {
        get => _isInComparison;
        set
        {
            if (SetProperty(ref _isInComparison, value))
            {
                OnPropertyChanged(nameof(PickerActionText));
                OnPropertyChanged(nameof(PickerButtonBgHex));
            }
        }
    }

    public bool IsBestRent
    {
        get => _isBestRent;
        set => SetProperty(ref _isBestRent, value);
    }

    public bool IsLargestArea
    {
        get => _isLargestArea;
        set => SetProperty(ref _isLargestArea, value);
    }

    public bool IsClosestToIctu
    {
        get => _isClosestToIctu;
        set => SetProperty(ref _isClosestToIctu, value);
    }

    public bool IsHighestSecurity
    {
        get => _isHighestSecurity;
        set => SetProperty(ref _isHighestSecurity, value);
    }

    public string PickerActionText => IsInComparison ? "✓ Đã chọn" : "+ Thêm";

    public string PickerButtonBgHex => IsInComparison ? "#0D9488" : "#512BD4";

    public string FormattedRent => string.Format(ViCulture, "{0:N0} đ", Rent);

    public string FormattedDeposit => string.Format(ViCulture, "{0:N0} đ", Deposit);

    public string FormattedArea => $"{Area:0.#} m²";

    public string FormattedPricePerSqm => string.Format(ViCulture, "{0:N0} đ/m²", Math.Round(PricePerSqm));

    public string FormattedDistance => DistanceMeters < 1000
        ? $"{DistanceMeters:0} m"
        : $"{DistanceMeters / 1000.0:0.1} km";

    public string FormattedSecurity => $"★ {SecurityScore:0.0}/5.0";

    public string VerifiedText => IsVerified ? "✓ Đã xác minh" : "Chưa xác minh";

    public string ResolvedImageUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ThumbnailUrl)
                && !ThumbnailUrl.Equals("placeholder_room.png", StringComparison.OrdinalIgnoreCase)
                && !ThumbnailUrl.Contains("cdn.example", StringComparison.OrdinalIgnoreCase))
            {
                return ThumbnailUrl;
            }

            return Id.ToLowerInvariant() switch
            {
                var s when s.EndsWith("002", StringComparison.OrdinalIgnoreCase) => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=600&h=400&q=80",
                var s when s.EndsWith("003", StringComparison.OrdinalIgnoreCase) => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=600&h=400&q=80",
                var s when s.EndsWith("004", StringComparison.OrdinalIgnoreCase) => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=600&h=400&q=80",
                var s when s.EndsWith("005", StringComparison.OrdinalIgnoreCase) => "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=600&h=400&q=80",
                var s when s.EndsWith("006", StringComparison.OrdinalIgnoreCase) => "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=600&h=400&q=80",
                _ => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=600&h=400&q=80"
            };
        }
    }

    public static RoomComparisonItemModel FromListingItem(RoomListingItemModel item, int index = 0)
    {
        var security = Math.Round(4.4 + ((index % 5) * 0.12), 1);
        var amenities = item.RoomType switch
        {
            var rt when rt.Contains("Studio", StringComparison.OrdinalIgnoreCase) => "Wifi, Điều hòa, Nóng lạnh, Ban công, Vân tay",
            var rt when rt.Contains("Chung cư", StringComparison.OrdinalIgnoreCase) => "Wifi, Thang máy, Điều hòa, Bếp từ, Bảo vệ",
            var rt when rt.Contains("Gác", StringComparison.OrdinalIgnoreCase) => "Wifi, Nóng lạnh, Gác lửng, Chỗ để xe",
            _ => "Wifi, Nóng lạnh, WC khép kín, Để xe"
        };

        var shortTitle = item.Title.Length > 26
            ? $"{item.Title[..24].TrimEnd()}…"
            : item.Title;

        return new RoomComparisonItemModel
        {
            Id = item.Id,
            Title = item.Title,
            ShortTitle = shortTitle,
            Rent = item.Rent,
            Deposit = item.Rent,
            Area = item.Area,
            DistanceMeters = item.DistanceMeters,
            RoomType = item.RoomType,
            Address = item.Address,
            ThumbnailUrl = item.ThumbnailUrl,
            IsVerified = item.IsVerified,
            SecurityScore = Math.Min(5.0, security),
            AmenitiesSummary = amenities,
            QuietHours = index % 2 == 0 ? "23:00 - 05:30" : "Tự do 24/7"
        };
    }
}

/// <summary>
/// Một dòng thuộc tính đối chiếu cạnh nhau trong bảng ComparisonGrid (T10.1).
/// </summary>
public sealed class ComparisonAttributeRowModel
{
    public string AttributeKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string AttributeLabel { get; set; } = string.Empty;

    public string DisplayLabel => $"{Icon} {AttributeLabel}";

    public string Value1 { get; set; } = "-";
    public string Value2 { get; set; } = "-";
    public string Value3 { get; set; } = "-";

    public bool HasSlot2 { get; set; }
    public bool HasSlot3 { get; set; }

    public string Slot1BgHex { get; set; } = "#F8FAFC";
    public string Slot2BgHex { get; set; } = "#F8FAFC";
    public string Slot3BgHex { get; set; } = "#F8FAFC";

    public string Slot1TextHex { get; set; } = "#0F172A";
    public string Slot2TextHex { get; set; } = "#0F172A";
    public string Slot3TextHex { get; set; } = "#0F172A";
}
