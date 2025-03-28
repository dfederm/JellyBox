using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace JellyBox.Converters;

/// <summary>
/// Value converter that translates true to <see cref="Visibility.Visible"/> and false to
/// <see cref="Visibility.Collapsed"/>, or the reverse if the parameter is "Reverse".
/// </summary>
internal sealed partial class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => ((value is bool b && b)
            ^ (parameter as string ?? string.Empty).Equals("Reverse", StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => (value is Visibility visibility && visibility == Visibility.Visible)
            ^ (parameter as string ?? string.Empty).Equals("Reverse", StringComparison.OrdinalIgnoreCase);
}