using System.Globalization;

namespace RoomToRentRadar.Shared.Converters;

public class StringHasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RoleBorderBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        return isSelected
            ? new SolidColorBrush(Color.FromArgb("#2563EB")) // Modern Royal Blue
            : new SolidColorBrush(Color.FromArgb("#E2E8F0")); // Subtle border
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RoleBackgroundBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = value is true;
        return isSelected
            ? new SolidColorBrush(Color.FromArgb("#EFF6FF")) // Soft blue selection tint
            : new SolidColorBrush(Color.FromArgb("#F8FAFC")); // Clean light background
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class EyeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isHidden = value is true;
        // When password is hidden, show open eye icon; when visible, show eye with strike
        return isHidden ? "👁" : "👁‍🗨";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
