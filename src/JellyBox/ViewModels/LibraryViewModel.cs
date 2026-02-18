using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class LibraryViewModel : ObservableObject, ILoadingViewModel
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly AppSettings _appSettings;
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly CardFactory _cardFactory;

    private Guid _collectionItemId;
    private BaseItemKind _itemKind;
    private bool _suppressRefresh;
    private LibraryViewSettings? _savedViewSettings;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Card>? Items { get; set; }

    public LibraryViewModel(
        AppSettings appSettings,
        JellyfinApiClient jellyfinApiClient,
        CardFactory cardFactory)
    {
        _appSettings = appSettings;
        _jellyfinApiClient = jellyfinApiClient;
        _cardFactory = cardFactory;
    }

    public void HandleParameters(Library.Parameters parameters)
    {
        _suppressRefresh = true;

        Title = parameters.Title;
        _collectionItemId = parameters.CollectionItemId;
        _itemKind = parameters.ItemKind;
        Items = null;

        SortOptions = GetSortOptions(_itemKind);

        // Restore persisted view settings or use defaults
        _savedViewSettings = _appSettings.GetLibraryViewSettings(_collectionItemId);
        SortOption? restoredSort = _savedViewSettings.SortBy is not null
            ? SortOptions.FirstOrDefault(o => o.SortBy.ToString() == _savedViewSettings.SortBy)
            : null;
        SelectedSortOption = restoredSort ?? SortOptions[0];
        IsSortDescending = _savedViewSettings.SortDescending;

        InitializeStatusFilters();
        RestoreFilterSelections(StatusFilters, _savedViewSettings.StatusFilters);

        _suppressRefresh = false;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;

        try
        {
            // Load filter values first so saved selections are restored before querying items
            await LoadFilterValuesAsync();
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in LibraryViewModel.InitializeAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshItemsAsync()
    {
        // Don't refresh if we haven't initialized yet
        if (SelectedSortOption is null)
        {
            return;
        }

        IsLoading = true;

        try
        {
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in LibraryViewModel.RefreshItemsAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadItemsAsync()
    {
        string[] selectedGenres = [.. GenreFilters.Where(f => f.IsSelected).Select(f => f.Label)];
        int?[] selectedYears = [.. YearFilters.Where(f => f.IsSelected).Select(f => (int?)int.Parse(f.Label))];
        string[] selectedRatings = [.. RatingFilters.Where(f => f.IsSelected).Select(f => f.Label)];
        ItemFilter[] selectedStatusFilters = [.. StatusFilters.Where(f => f.IsSelected && f.ItemFilter.HasValue).Select(f => f.ItemFilter!.Value)];

        ItemSortBy sortBy = SelectedSortOption?.SortBy ?? ItemSortBy.SortName;
        SortOrder sortOrder = IsSortDescending ? SortOrder.Descending : SortOrder.Ascending;

        // TODO: Paginate?
        BaseItemDtoQueryResult? result = await _jellyfinApiClient.Items.GetAsync(parameters =>
        {
            parameters.QueryParameters.ParentId = _collectionItemId;
            parameters.QueryParameters.SortBy = [sortBy];
            parameters.QueryParameters.SortOrder = [sortOrder];
            parameters.QueryParameters.IncludeItemTypes = [_itemKind];
            parameters.QueryParameters.Recursive = true;
            parameters.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount];
            parameters.QueryParameters.ImageTypeLimit = 1;
            parameters.QueryParameters.EnableImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Banner, ImageType.Thumb];

            if (selectedGenres.Length > 0)
            {
                parameters.QueryParameters.Genres = selectedGenres;
            }

            if (selectedYears.Length > 0)
            {
                parameters.QueryParameters.Years = selectedYears;
            }

            if (selectedRatings.Length > 0)
            {
                parameters.QueryParameters.OfficialRatings = selectedRatings;
            }

            if (selectedStatusFilters.Length > 0)
            {
                parameters.QueryParameters.Filters = selectedStatusFilters;
            }
        });

        if (result?.Items is not null)
        {
            List<Card> items = new(result.Items.Count);
            foreach (BaseItemDto item in result.Items)
            {
                if (!item.Id.HasValue)
                {
                    continue;
                }

                items.Add(_cardFactory.CreateFromItem(item, CardShape.Portrait, preferredImageType: null));
            }

            Items = items;
        }
        else
        {
            Items = [];
        }
    }

    private void SaveViewSettings()
    {
        if (_suppressRefresh || SelectedSortOption is null)
        {
            return;
        }

        _appSettings.SetLibraryViewSettings(
            _collectionItemId,
            new LibraryViewSettings(
                SelectedSortOption.SortBy.ToString(),
                IsSortDescending,
                GetSelectedLabels(StatusFilters),
                GetSelectedLabels(GenreFilters),
                GetSelectedLabels(YearFilters),
                GetSelectedLabels(RatingFilters)));

        static string[] GetSelectedLabels(List<FilterItem> filters)
            => [.. filters.Where(f => f.IsSelected).Select(f => f.Label)];
    }
}
