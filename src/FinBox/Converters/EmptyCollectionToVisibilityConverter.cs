using System.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace FinBox.Converters;

internal sealed partial class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is not ICollection collection
            ? throw new InvalidOperationException($"{nameof(EmptyCollectionToVisibilityConverter)} can only be used with collection types")
            : collection.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new InvalidOperationException($"{nameof(EmptyCollectionToVisibilityConverter)} cannot be used in a two-way binding");
    }
}
