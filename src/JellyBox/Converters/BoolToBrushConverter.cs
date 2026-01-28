using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace JellyBox.Converters;

/// <summary>
/// Value converter that translates a bool into a brush color.
/// True returns the "on" color (e.g., red for active state), False returns the "off" color (e.g., white for inactive state).
/// </summary>
internal sealed partial class BoolToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush OnBrush = new(Colors.Red);
    private static readonly SolidColorBrush OffBrush = new(Colors.White);

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not bool b
            ? throw new InvalidOperationException($"{nameof(BoolToBrushConverter)} can only be used with bool")
            : b ? OnBrush : OffBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
