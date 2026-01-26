using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class MoviesViewModel : ObservableObject
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly JellyfinImageResolver _imageResolver;
    private readonly NavigationManager _navigationManager;

    private Guid? _collectionItemId;

    public ObservableCollection<Card> Movies { get; } = new();

    public MoviesViewModel(
        JellyfinApiClient jellyfinApiClient,
        JellyfinImageResolver imageResolver,
        NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _imageResolver = imageResolver;
        _navigationManager = navigationManager;
    }

    public void HandleParameters(Movies.Parameters parameters)
    {
        _collectionItemId = parameters.CollectionItemId;
        _ = InitializeMoviesAsync();
    }

    private async Task InitializeMoviesAsync()
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
            parameters.QueryParameters.SortBy = [ItemSortBy.SortName, ItemSortBy.ProductionYear];
            parameters.QueryParameters.SortOrder = [SortOrder.Ascending];
            parameters.QueryParameters.IncludeItemTypes = [BaseItemKind.Movie];
            parameters.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio, ItemFields.MediaSourceCount];
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

                Movies.Add(CardFactory.CreateFromItem(item, CardShape.Portrait, preferredImageType: null, _imageResolver, _navigationManager));
            }
        }
    }
}