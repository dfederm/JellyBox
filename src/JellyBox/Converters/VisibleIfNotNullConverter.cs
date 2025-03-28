using System.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace JellyBox.Converters;

/// <summary>
/// Value converter that translates a non-null value or non-empty collection to <see cref="Visibility.Visible"/>
/// and a null value or empty collection to <see cref="Visibility.Collapsed"/>.
/// </summary>
internal sealed partial class VisibleIfNotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not null && (value is not ICollection collection || collection.Count != 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new InvalidOperationException($"{nameof(VisibleIfNotNullConverter)} cannot be used in a two-way binding");
}