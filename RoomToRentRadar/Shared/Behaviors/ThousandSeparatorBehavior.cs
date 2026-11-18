using System.Globalization;

namespace RoomToRentRadar.Shared.Behaviors;

/// <summary>
/// A MAUI Entry behavior that auto-formats numeric input with thousand separators (e.g. "50,000") in real time.
/// </summary>
public sealed class ThousandSeparatorBehavior : Behavior<Entry>
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    private bool _isUpdating;

    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.TextChanged += OnTextChanged;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        base.OnDetachingFrom(bindable);
        bindable.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || sender is not Entry entry)
        {
            return;
        }

        var newText = e.NewTextValue;
        if (string.IsNullOrWhiteSpace(newText))
        {
            return;
        }

        // Extract only numeric digits
        var rawDigits = new string(newText.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(rawDigits))
        {
            _isUpdating = true;
            try
            {
                entry.Text = string.Empty;
            }
            finally
            {
                _isUpdating = false;
            }
            return;
        }

        if (decimal.TryParse(rawDigits, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            var formatted = number.ToString("N0", ViCulture);
            if (!string.Equals(entry.Text, formatted, StringComparison.Ordinal))
            {
                _isUpdating = true;
                try
                {
                    entry.Text = formatted;
                    entry.CursorPosition = formatted.Length;
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
    }
}
