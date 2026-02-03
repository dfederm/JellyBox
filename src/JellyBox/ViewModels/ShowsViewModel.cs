using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class ShowsViewModel : ObservableObject, ILoadingViewModel
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly CardFactory _cardFactory;

    private Guid? _collectionItemId;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public ObservableCollection<Card> Shows { get; } = new();

    public ShowsViewModel(
        JellyfinApiClient jellyfinApiClient,
        CardFactory cardFactory)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _cardFactory = cardFactory;
    }

    public void HandleParameters(Shows.Parameters parameters)
    {
        _collectionItemId = parameters.CollectionItemId;
        _ = InitializeShowsAsync();
    }

    private async Task InitializeShowsAsync()
    {
        // Uninitialized
        if (_collectionItemId is null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            // TODO: Paginate?
            BaseItemDtoQueryResult? result = await _jellyfinApiClient.Items.GetAsync(parameters =>
            {
                parameters.QueryParameters.ParentId = _collectionItemId;
                parameters.QueryParameters.SortBy = [ItemSortBy.SortName];
                parameters.QueryParameters.SortOrder = [SortOrder.Ascending];
                parameters.QueryParameters.IncludeItemTypes = [BaseItemKind.Series];
                parameters.QueryParameters.Recursive = true;
                parameters.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio];
                parameters.QueryParameters.ImageTypeLimit = 1;
                parameters.QueryParameters.EnableImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Banner, ImageType.Thumb];
            });

            if (result?.Items is not null)
            {
                foreach (BaseItemDto item in result.Items)
                {
                    if (!item.Id.HasValue)
                    {
                        continue;
                    }

                    Shows.Add(_cardFactory.CreateFromItem(item, CardShape.Portrait, preferredImageType: null));
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}