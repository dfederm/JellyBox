using Windows.UI.Xaml.Data;

namespace JellyBox.Converters;

/// <summary>
/// Value converter that translates a a bool into its negated value.
/// </summary>
internal sealed partial class NegateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not bool b
            ? throw new InvalidOperationException($"{nameof(NegateConverter)} can only be used with bool")
            : !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => Convert(value, targetType, parameter, language);
}