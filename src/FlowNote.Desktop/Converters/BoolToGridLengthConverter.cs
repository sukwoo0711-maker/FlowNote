using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowNote.Desktop.Converters;

public sealed class BoolToGridLengthConverter : IValueConverter
{
    public double TrueWidth { get; set; } = 320;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? new GridLength(TrueWidth) : new GridLength(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
