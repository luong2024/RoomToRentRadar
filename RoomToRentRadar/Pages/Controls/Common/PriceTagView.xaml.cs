using System.Globalization;

namespace RoomToRentRadar.Pages.Controls.Common;

public partial class PriceTagView : ContentView
{
    private static readonly CultureInfo VietnameseCulture = new("vi-VN");

    public static readonly BindableProperty PriceProperty =
        BindableProperty.Create(nameof(Price), typeof(decimal), typeof(PriceTagView), 0m,
            propertyChanged: (b, o, n) => ((PriceTagView)b).UpdateFormattedPrice());

    public static readonly BindableProperty PeriodProperty =
        BindableProperty.Create(nameof(Period), typeof(string), typeof(PriceTagView), "/ tháng");

    public static readonly BindableProperty FormattedPriceProperty =
        BindableProperty.Create(nameof(FormattedPrice), typeof(string), typeof(PriceTagView), "0 đ");

    public decimal Price
    {
        get => (decimal)GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    public string Period
    {
        get => (string)GetValue(PeriodProperty);
        set => SetValue(PeriodProperty, value);
    }

    public string FormattedPrice
    {
        get => (string)GetValue(FormattedPriceProperty);
        private set => SetValue(FormattedPriceProperty, value);
    }

    public PriceTagView()
    {
        InitializeComponent();
        UpdateFormattedPrice();
    }

    private void UpdateFormattedPrice()
    {
        FormattedPrice = $"{Price.ToString("N0", VietnameseCulture)} đ";
    }
}

