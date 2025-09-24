using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DiscordEmotify.Gui.Converters;

public class BoolToTextConverter : IValueConverter
{
    public static BoolToTextConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string)?.Split('|', 2) ?? Array.Empty<string>();
        var whenTrue = parts.Length > 0 ? parts[0] : "On";
        var whenFalse = parts.Length > 1 ? parts[1] : "Off";
        var b = value as bool? ?? false;
        return b ? whenTrue : whenFalse;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
