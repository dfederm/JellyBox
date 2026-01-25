using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Models;
using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class ShowsViewModel : ObservableObject
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly JellyfinImageResolver _imageResolver;
    private readonly NavigationManager _navigationManager;

    private Guid? _collectionItemId;

    public ObservableCollection<Card> Shows { get; } = new();

    public ShowsViewModel(
        JellyfinApiClient jellyfinApiClient,
        JellyfinImageResolver imageResolver,
        NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _imageResolver = imageResolver;
        _navigationManager = navigationManager;
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

                Shows.Add(CardFactory.CreateFromItem(item, CardShape.Portrait, preferredImageType: null, _imageResolver));
            }
        }
    }

    [RelayCommand]
    private void NavigateToCard(Card card)
    {
        _navigationManager.NavigateToItem(card.Item);
    }
}