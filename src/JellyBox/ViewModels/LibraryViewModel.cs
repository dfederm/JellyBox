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
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly CardFactory _cardFactory;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Card>? Items { get; set; }

    public LibraryViewModel(
        JellyfinApiClient jellyfinApiClient,
        CardFactory cardFactory)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _cardFactory = cardFactory;
    }

    public void HandleParameters(Library.Parameters parameters)
    {
        Title = parameters.Title;
        Items = null;
        _ = InitializeAsync(parameters.CollectionItemId, parameters.ItemKind);
    }

    private async Task InitializeAsync(Guid collectionItemId, BaseItemKind itemKind)
    {
        IsLoading = true;

        try
        {
            // TODO: Paginate?
            BaseItemDtoQueryResult? result = await _jellyfinApiClient.Items.GetAsync(parameters =>
            {
                parameters.QueryParameters.ParentId = collectionItemId;
                parameters.QueryParameters.SortBy = itemKind == BaseItemKind.Movie
                    ? [ItemSortBy.SortName, ItemSortBy.ProductionYear]
                    : [ItemSortBy.SortName];
                parameters.QueryParameters.SortOrder = [SortOrder.Ascending];
                parameters.QueryParameters.IncludeItemTypes = [itemKind];
                parameters.QueryParameters.Recursive = itemKind == BaseItemKind.Series;
                parameters.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount];
                parameters.QueryParameters.ImageTypeLimit = 1;
                parameters.QueryParameters.EnableImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Banner, ImageType.Thumb];
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LibraryViewModel.InitializeAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
