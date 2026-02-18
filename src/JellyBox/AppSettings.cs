using Windows.Storage;

namespace JellyBox;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class AppSettings
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public string? ServerUrl
    {
        get => _settings.GetProperty<string>(nameof(ServerUrl));
        set => _settings.SetProperty(nameof(ServerUrl), value);
    }

    public string? AccessToken
    {
        get => _settings.GetProperty<string>(nameof(AccessToken));
        set => _settings.SetProperty(nameof(AccessToken), value);
    }

    public LibraryViewSettings GetLibraryViewSettings(Guid libraryId)
    {
        ApplicationDataContainer container = GetLibraryContainer(libraryId);
        return new LibraryViewSettings(
            container.GetProperty<string>(nameof(LibraryViewSettings.SortBy)),
            container.GetProperty<bool>(nameof(LibraryViewSettings.SortDescending)),
            ParseList(container.GetProperty<string>(nameof(LibraryViewSettings.StatusFilters))),
            ParseList(container.GetProperty<string>(nameof(LibraryViewSettings.GenreFilters))),
            ParseList(container.GetProperty<string>(nameof(LibraryViewSettings.YearFilters))),
            ParseList(container.GetProperty<string>(nameof(LibraryViewSettings.RatingFilters))));

        static string[] ParseList(string? value)
            => string.IsNullOrEmpty(value) ? [] : value.Split('\n');
    }

    public void SetLibraryViewSettings(Guid libraryId, LibraryViewSettings settings)
    {
        ApplicationDataContainer container = GetLibraryContainer(libraryId);
        container.SetProperty(nameof(LibraryViewSettings.SortBy), settings.SortBy);
        container.SetProperty(nameof(LibraryViewSettings.SortDescending), settings.SortDescending);
        container.SetProperty(nameof(LibraryViewSettings.StatusFilters), JoinList(settings.StatusFilters));
        container.SetProperty(nameof(LibraryViewSettings.GenreFilters), JoinList(settings.GenreFilters));
        container.SetProperty(nameof(LibraryViewSettings.YearFilters), JoinList(settings.YearFilters));
        container.SetProperty(nameof(LibraryViewSettings.RatingFilters), JoinList(settings.RatingFilters));

        static string? JoinList(string[] values)
            => values.Length > 0 ? string.Join('\n', values) : null;
    }

    private ApplicationDataContainer GetLibraryContainer(Guid libraryId)
        => _settings.CreateContainer($"Library_{libraryId}", ApplicationDataCreateDisposition.Always);
}

file static class ApplicationDataContainerExtensions
{
    internal static void SetProperty(this ApplicationDataContainer container, string propertyName, object? value)
        => container.Values[propertyName] = value;

    internal static T? GetProperty<T>(this ApplicationDataContainer container, string propertyName, T? defaultValue = default)
    {
        object value = container.Values[propertyName];
        return value is not null ? (T)value : defaultValue;
    }
}

internal sealed record LibraryViewSettings(
    string? SortBy,
    bool SortDescending,
    string[] StatusFilters,
    string[] GenreFilters,
    string[] YearFilters,
    string[] RatingFilters);
