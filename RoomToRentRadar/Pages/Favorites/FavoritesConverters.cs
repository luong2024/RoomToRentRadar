using System.Globalization;

namespace RoomToRentRadar.Pages.Favorites
{
    public class ListingStatusToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            return status switch
            {
                "Available" => Color.FromArgb("#2E7D32"),      // Forest green
                "Unavailable" => Color.FromArgb("#C62828"),    // Crimson red
                "PendingReview" => Color.FromArgb("#EF6C00"),  // Amber orange
                _ => Color.FromArgb("#757575")
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ListingStatusToBadgeBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            return status switch
            {
                "Available" => Color.FromArgb("#E8F5E9"),      // Soft green
                "Unavailable" => Color.FromArgb("#FFEBEE"),    // Soft red
                "PendingReview" => Color.FromArgb("#FFF3E0"),  // Soft orange
                _ => Color.FromArgb("#EEEEEE")
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ListingStatusToTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            return status switch
            {
                "Available" => "● Còn phòng",
                "Unavailable" => "● Đã hết phòng",
                "PendingReview" => "● Chờ duyệt",
                _ => status ?? string.Empty
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

