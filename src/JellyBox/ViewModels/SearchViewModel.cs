using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class SearchViewModel : ObservableObject, ILoadingViewModel
#pragma warning restore CA1812
{
    private const int ResultLimit = 100;

    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly CardFactory _cardFactory;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string Query { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultsTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ResultsSubtitle { get; private set; }

    [ObservableProperty]
    public partial bool ShowEmptyState { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<Card>? Items { get; private set; }

    public SearchViewModel(
        JellyfinApiClient jellyfinApiClient,
        CardFactory cardFactory)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _cardFactory = cardFactory;
    }

    public void HandleParameters(Search.Parameters parameters)
    {
        Query = parameters.Query.Trim();
        ResultsTitle = $"Results for \"{Query}\"";
        Items = null;
        ShowEmptyState = false;
        ResultsSubtitle = null;
        _ = LoadResultsAsync();
    }

    private async Task LoadResultsAsync()
    {
        IsLoading = true;

        try
        {
            BaseItemDtoQueryResult? result = await _jellyfinApiClient.Items.GetAsync(parameters =>
            {
                parameters.QueryParameters.SearchTerm = Query;
                parameters.QueryParameters.IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series];
                parameters.QueryParameters.Recursive = true;
                parameters.QueryParameters.SortBy = [ItemSortBy.SortName];
                parameters.QueryParameters.Limit = ResultLimit;
                parameters.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount];
                parameters.QueryParameters.ImageTypeLimit = 1;
                parameters.QueryParameters.EnableImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Banner, ImageType.Thumb];
            });

            if (result?.Items is null || result.Items.Count == 0)
            {
                Items = [];
                ShowEmptyState = true;
                ResultsSubtitle = "No movies or TV shows matched your search.";
                return;
            }

            List<Card> items = new(result.Items.Count);
            foreach (BaseItemDto item in result.Items)
            {
                if (!item.Id.HasValue)
                {
                    continue;
                }

                if (item.Type is not (BaseItemDto_Type.Movie or BaseItemDto_Type.Series))
                {
                    continue;
                }

                items.Add(_cardFactory.CreateFromItem(item, CardShape.Portrait, preferredImageType: null));
            }

            Items = items;
            ShowEmptyState = items.Count == 0;
            ResultsSubtitle = items.Count == 0
                ? "No movies or TV shows matched your search."
                : items.Count == 1
                    ? "1 result"
                    : $"{items.Count} results";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in SearchViewModel.LoadResultsAsync: {ex}");
            Items = [];
            ShowEmptyState = true;
            ResultsSubtitle = "Something went wrong while searching. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}