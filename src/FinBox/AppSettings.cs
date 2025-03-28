using Windows.Storage;

namespace FinBox;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class AppSettings
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    public string? ServerUrl
    {
        get => GetProperty<string>(nameof(ServerUrl));
        set => SetProperty(nameof(ServerUrl), value);
    }

    public string? AccessToken
    {
        get => GetProperty<string>(nameof(AccessToken));
        set => SetProperty(nameof(AccessToken), value);
    }

    private static void SetProperty(string propertyName, object? value)
        => ApplicationData.Current.LocalSettings.Values[propertyName] = value;

    private static T? GetProperty<T>(string propertyName, T? defaultValue = default)
    {
        object value = ApplicationData.Current.LocalSettings.Values[propertyName];
        return value is not null ? (T)value : defaultValue;
    }
}
