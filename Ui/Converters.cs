using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace texAi.Ui;

/// <summary>Shows an element only when the bound value is false.</summary>
internal sealed class InverseBoolToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Shows an element only when the bound value is not null.</summary>
internal sealed class NotNullToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Turns a HotkeyAction into the label the user knows it by. The enum names are
/// fine in code but "Tone" alone does not say what pressing it does.
/// </summary>
internal sealed class ActionLabel : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        HotkeyAction.Grammar => "Fix grammar",
        HotkeyAction.Translate => "Translate to English",
        HotkeyAction.Rewrite => "Rewrite",
        HotkeyAction.Tone => $"Change tone to {SettingsStore.Current.Tone.ToLowerInvariant()}",
        null => "texAi",
        _ => value.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Amber for a model too big to sit comfortably in 6GB, muted otherwise.</summary>
internal sealed class TightFitBrush : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.Resources[value is true ? "Accent" : "TextMuted"];

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
