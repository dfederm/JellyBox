using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinBox.Services;
using FinBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace FinBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class MoviesViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    private Guid? _collectionItemId;

    [ObservableProperty]
    public partial ObservableCollection<BaseItemDto>? Movies { get; set; }

    public MoviesViewModel(JellyfinApiClient jellyfinApiClient, NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;

        InitializeMovies();
    }

    public void HandleParameters(Movies.Parameters parameters)
    {
        _collectionItemId = parameters.CollectionItemId;
        InitializeMovies();
    }

    private async void InitializeMovies()
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
            Movies = new ObservableCollection<BaseItemDto>(result.Items);
        }
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item)
    {
        _navigationManager.NavigateToItem(item);
    }
}