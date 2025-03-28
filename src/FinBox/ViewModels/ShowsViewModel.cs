using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinBox.Services;
using FinBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace FinBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class ShowsViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    private Guid? _collectionItemId;

    [ObservableProperty]
    public partial ObservableCollection<BaseItemDto>? Shows { get; set; }

    public ShowsViewModel(JellyfinApiClient jellyfinApiClient, NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;

        InitializeShows();
    }

    public void HandleParameters(Shows.Parameters parameters)
    {
        _collectionItemId = parameters.CollectionItemId;
        InitializeShows();
    }

    private async void InitializeShows()
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
            Shows = new ObservableCollection<BaseItemDto>(result.Items);
        }
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item)
    {
        _navigationManager.NavigateToItem(item);
    }
}