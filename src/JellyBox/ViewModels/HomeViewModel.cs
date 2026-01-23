using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Models;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class HomeViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    public ObservableCollection<Section> Sections { get; } = new();

    public HomeViewModel(JellyfinApiClient jellyfinApiClient, NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;
    }

    public async void Initialize()
    {
        try
        {
            Task<Section?>[] sectionTasks =
            [
                GetUserViewsSectionAsync(),
                GetResumeSectionAsync("Continue Watching", MediaType.Video),
                GetResumeSectionAsync("Continue Listening", MediaType.Audio),
                GetResumeSectionAsync("Continue Reading", MediaType.Book),
                // TODO: LiveTV Section,
                GetNextUpSectionAsync(),
                // TODO: LatestMedia Sections
            ];

            Section?[] sections = await Task.WhenAll(sectionTasks);
            foreach (Section? section in sections)
            {
                if (section is not null)
                {
                    Sections.Add(section);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in HomeViewModel.Initialize: {ex}");
        }
    }

    private async Task<Section?> GetUserViewsSectionAsync()
    {
        BaseItemDtoQueryResult? result = await _jellyfinApiClient.UserViews.GetAsync();
        return CreateSection("My Media", result, CardShape.Backdrop, preferredImageType: null);
    }

    private async Task<Section?> GetResumeSectionAsync(string name, MediaType mediaType)
    {
        BaseItemDtoQueryResult? result = await _jellyfinApiClient.UserItems.Resume.GetAsync(requestConfig =>
        {
            requestConfig.QueryParameters.Limit = 12;
            requestConfig.QueryParameters.Fields = [ItemFields.PrimaryImageAspectRatio];
            requestConfig.QueryParameters.ImageTypeLimit = 1;
            requestConfig.QueryParameters.EnableImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Thumb];
            requestConfig.QueryParameters.EnableTotalRecordCount = false;
            requestConfig.QueryParameters.MediaTypes = [mediaType];
        });

        return CreateSection(name, result, CardShape.Backdrop, ImageType.Thumb);
    }

    private async Task<Section?> GetNextUpSectionAsync()
    {
        BaseItemDtoQueryResult? result = await _jellyfinApiClient.Shows.NextUp.GetAsync();
        return CreateSection("Next Up", result, CardShape.Backdrop, ImageType.Thumb);
    }

    private Section? CreateSection(
        string name,
        BaseItemDtoQueryResult? result,
        CardShape cardShape,
        ImageType? preferredImageType)
    {
        if (result?.Items is null)
        {
            return null;
        }

        List<Card> cards = new(result.Items.Count);
        foreach (BaseItemDto item in result.Items)
        {
            if (!item.Id.HasValue)
            {
                continue;
            }

            cards.Add(new Card
            {
                Item = item,
                Shape = cardShape,
                PreferredImageType = preferredImageType,
            });
        }

        return new Section
        {
            Name = name,
            Cards = cards,
            NavigateToCardCommand = NavigateToCardCommand,
        };
    }

    [RelayCommand]
    private void NavigateToCard(Card card)
    {
        _navigationManager.NavigateToItem(card.Item);
    }
}