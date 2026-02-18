using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jellyfin.Sdk.Generated.Models;
using Windows.UI.Xaml.Media;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class LibraryViewModel
#pragma warning restore CA1812
{
    // Sort state
    [ObservableProperty]
    public partial List<SortOption> SortOptions { get; set; } = [];

    [ObservableProperty]
    public partial SortOption? SelectedSortOption { get; set; }

    [ObservableProperty]
    public partial bool IsSortDescending { get; set; }

    // Filter state
    [ObservableProperty]
    public partial List<FilterItem> StatusFilters { get; set; } = [];

    [ObservableProperty]
    public partial List<FilterItem> GenreFilters { get; set; } = [];

    [ObservableProperty]
    public partial List<FilterItem> YearFilters { get; set; } = [];

    [ObservableProperty]
    public partial List<FilterItem> RatingFilters { get; set; } = [];

    [ObservableProperty]
    public partial bool HasActiveFilters { get; set; }

    partial void OnIsSortDescendingChanged(bool value)
    {
        SaveViewSettings();
        _ = RefreshItemsAsync();
    }

    [RelayCommand]
    private void SelectSortOption(SortOption option)
    {
        SelectedSortOption = option;
        SaveViewSettings();
        _ = RefreshItemsAsync();
    }

    [RelayCommand]
    private void ToggleSortOrder() => IsSortDescending = !IsSortDescending;

    [RelayCommand]
    private void ClearFilters()
    {
        _suppressRefresh = true;
        try
        {
            UnselectAll(StatusFilters);
            UnselectAll(GenreFilters);
            UnselectAll(YearFilters);
            UnselectAll(RatingFilters);
        }
        finally
        {
            _suppressRefresh = false;
        }

        UpdateHasActiveFilters();
        SaveViewSettings();
        _ = RefreshItemsAsync();

        static void UnselectAll(List<FilterItem> filters)
        {
            foreach (FilterItem filter in filters)
            {
                filter.IsSelected = false;
            }
        }
    }

    private void OnFilterChanged()
    {
        if (_suppressRefresh)
        {
            return;
        }

        UpdateHasActiveFilters();
        SaveViewSettings();
        _ = RefreshItemsAsync();
    }

    private void UpdateHasActiveFilters()
    {
        HasActiveFilters = StatusFilters.Any(f => f.IsSelected)
            || GenreFilters.Any(f => f.IsSelected)
            || YearFilters.Any(f => f.IsSelected)
            || RatingFilters.Any(f => f.IsSelected);
    }

    private void InitializeStatusFilters()
    {
        StatusFilters =
        [
            new FilterItem(OnFilterChanged, "Unplayed", ItemFilter.IsUnplayed),
            new FilterItem(OnFilterChanged, "Played", ItemFilter.IsPlayed),
            new FilterItem(OnFilterChanged, "Resumable", ItemFilter.IsResumable),
            new FilterItem(OnFilterChanged, "Favorites", ItemFilter.IsFavorite),
        ];
    }

    private async Task LoadFilterValuesAsync()
    {
        try
        {
            QueryFiltersLegacy? filters = await _jellyfinApiClient.Items.Filters.GetAsync(parameters =>
            {
                parameters.QueryParameters.ParentId = _collectionItemId;
                parameters.QueryParameters.IncludeItemTypes = [_itemKind];
            });

            if (filters is not null)
            {
                _suppressRefresh = true;

                try
                {
                    if (filters.Genres is not null)
                    {
                        GenreFilters = [.. filters.Genres.Where(g => g is not null).OrderBy(g => g, StringComparer.CurrentCulture).Select(g => new FilterItem(OnFilterChanged, g!))];
                    }

                    if (filters.Years is not null)
                    {
                        YearFilters = [.. filters.Years.Where(y => y.HasValue).OrderByDescending(y => y!.Value).Select(y => new FilterItem(OnFilterChanged, y!.Value.ToString()))];
                    }

                    if (filters.OfficialRatings is not null)
                    {
                        RatingFilters = [.. filters.OfficialRatings.Where(r => r is not null).Select(r => new FilterItem(OnFilterChanged, r!))];
                    }

                    // Restore persisted filter selections
                    if (_savedViewSettings is not null)
                    {
                        RestoreFilterSelections(GenreFilters, _savedViewSettings.GenreFilters);
                        RestoreFilterSelections(YearFilters, _savedViewSettings.YearFilters);
                        RestoreFilterSelections(RatingFilters, _savedViewSettings.RatingFilters);
                        _savedViewSettings = null;
                    }
                }
                finally
                {
                    _suppressRefresh = false;
                }

                UpdateHasActiveFilters();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading filter values: {ex}");
        }
    }

    private static List<SortOption> GetSortOptions(BaseItemKind itemKind) => itemKind switch
    {
        BaseItemKind.Movie =>
        [
            new("Name", ItemSortBy.SortName),
            new("Community Rating", ItemSortBy.CommunityRating),
            new("Critic Rating", ItemSortBy.CriticRating),
            new("Date Added", ItemSortBy.DateCreated),
            new("Date Played", ItemSortBy.DatePlayed),
            new("Parental Rating", ItemSortBy.OfficialRating),
            new("Play Count", ItemSortBy.PlayCount),
            new("Release Date", ItemSortBy.PremiereDate),
            new("Runtime", ItemSortBy.Runtime),
        ],
        BaseItemKind.Series =>
        [
            new("Name", ItemSortBy.SortName),
            new("Community Rating", ItemSortBy.CommunityRating),
            new("Date Added", ItemSortBy.DateCreated),
            new("Date Episode Added", ItemSortBy.DateLastContentAdded),
            new("Date Played", ItemSortBy.SeriesDatePlayed),
            new("Parental Rating", ItemSortBy.OfficialRating),
            new("Release Date", ItemSortBy.PremiereDate),
        ],
        _ =>
        [
            new("Name", ItemSortBy.SortName),
            new("Date Added", ItemSortBy.DateCreated),
            new("Release Date", ItemSortBy.PremiereDate),
        ],
    };

    private static void RestoreFilterSelections(List<FilterItem> filters, string[] savedLabels)
    {
        if (savedLabels.Length == 0)
        {
            return;
        }

        HashSet<string> labelSet = new(savedLabels, StringComparer.Ordinal);
        foreach (FilterItem filter in filters)
        {
            if (labelSet.Contains(filter.Label))
            {
                filter.IsSelected = true;
            }
        }
    }

    // x:Bind function binding helpers
    public static string GetSortDirectionGlyph(bool isDescending) => isDescending ? Glyphs.SortDescending : Glyphs.SortAscending;

    public static string GetSortDirectionLabel(bool isDescending) => isDescending ? "Descending" : "Ascending";

    public static Brush GetFilterBorderBrush(bool hasActiveFilters)
        => hasActiveFilters
            ? (Brush)Windows.UI.Xaml.Application.Current.Resources["AccentColor"]
            : (Brush)Windows.UI.Xaml.Application.Current.Resources["BorderSubtle"];

    public static Windows.UI.Xaml.DependencyObject GetFilterXYFocusRight(
        bool hasActiveFilters,
        Windows.UI.Xaml.DependencyObject filterButton,
        Windows.UI.Xaml.DependencyObject clearFiltersButton)
        => hasActiveFilters ? clearFiltersButton : filterButton;
}

internal sealed record SortOption(string Label, ItemSortBy SortBy)
{
    public override string ToString() => Label;
}

internal sealed partial class FilterItem : ObservableObject
{
    private readonly Action _onChanged;

    public FilterItem(Action onChanged, string label, ItemFilter? itemFilter = null)
    {
        _onChanged = onChanged;
        Label = label;
        ItemFilter = itemFilter;
    }

    public string Label { get; }

    public ItemFilter? ItemFilter { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _onChanged();
}
