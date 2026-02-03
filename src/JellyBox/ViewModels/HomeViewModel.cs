using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Models;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class HomeViewModel : ObservableObject, ILoadingViewModel
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly CardFactory _cardFactory;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Section>? Sections { get; set; }

    public HomeViewModel(
        JellyfinApiClient jellyfinApiClient,
        CardFactory cardFactory)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _cardFactory = cardFactory;
    }

    public async void Initialize()
    {
        IsLoading = true;
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

            Section?[] results = await Task.WhenAll(sectionTasks);

            List<Section> sections = new(results.Length);
            foreach (Section? section in results)
            {
                if (section is not null)
                {
                    sections.Add(section);
                }
            }

            Sections = sections;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in HomeViewModel.Initialize: {ex}");
        }
        finally
        {
            IsLoading = false;
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

            cards.Add(_cardFactory.CreateFromItem(item, cardShape, preferredImageType));
        }

        return new Section
        {
            Name = name,
            Cards = cards,
        };
    }
}