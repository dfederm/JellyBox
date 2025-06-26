using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Models;
using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace JellyBox.ViewModels;

internal sealed record MediaInfoItem(string Text);

internal sealed record MediaStreamOption(string DisplayText, int? Index)
{
    public static MediaStreamOption SubtitlesOff { get; } = new("Off", -1);
}

internal sealed record MediaSourceInfoWrapper(string DisplayText, MediaSourceInfo Value);

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class ItemDetailsViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private static readonly SolidColorBrush OnBrush = new SolidColorBrush(Colors.Red);
    private static readonly SolidColorBrush OffBrush = new SolidColorBrush(Colors.White);

    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    [ObservableProperty]
    public partial BaseItemDto? Item { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial Uri? BackdropImageUri { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MediaInfoItem>? MediaInfo { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MediaSourceInfoWrapper>? SourceContainers { get; set; }

    [ObservableProperty]
    public partial MediaSourceInfoWrapper? SelectedSourceContainer { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MediaStreamOption>? VideoStreams { get; set; }

    [ObservableProperty]
    public partial MediaStreamOption? SelectedVideoStream { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MediaStreamOption>? AudioStreams { get; set; }

    [ObservableProperty]
    public partial MediaStreamOption? SelectedAudioStream { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MediaStreamOption>? SubtitleStreams { get; set; }

    [ObservableProperty]
    public partial MediaStreamOption? SelectedSubtitleStream { get; set; }

    [ObservableProperty]
    public partial string? TagLine { get; set; }

    [ObservableProperty]
    public partial string? Overview { get; set; }

    [ObservableProperty]
    public partial string? Tags { get; set; }

    [ObservableProperty]
    public partial bool IsPlayed { get; set; }

    [ObservableProperty]
    public partial Brush? PlayStateBrush { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial Brush? FavoriteBrush { get; set; }

    [ObservableProperty]
    public partial List<Section>? Sections { get; set; }

    public ItemDetailsViewModel(JellyfinApiClient jellyfinApiClient, NavigationManager navigationManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;
    }

    internal async void HandleParameters(ItemDetails.Parameters parameters)
    {
        Item = await _jellyfinApiClient.Items[parameters.ItemId].GetAsync();

        Name = Item!.Name;
        BackdropImageUri = _jellyfinApiClient.GetItemBackdropImageUrl(Item, 1920);

        List<MediaInfoItem> mediaInfo = new();
        if (Item.ProductionYear.HasValue)
        {
            mediaInfo.Add(new MediaInfoItem(Item.ProductionYear.Value.ToString()));
        }

        if (Item.RunTimeTicks.HasValue)
        {
            mediaInfo.Add(new MediaInfoItem(GetDisplayDuration(Item.RunTimeTicks.Value)));
        }

        if (!string.IsNullOrEmpty(Item.OfficialRating))
        {
            // TODO: Style correctly
            mediaInfo.Add(new MediaInfoItem(Item.OfficialRating));
        }

        if (Item.CommunityRating.HasValue)
        {
            // TODO: Style correctly
            mediaInfo.Add(new MediaInfoItem(Item.CommunityRating.Value.ToString("F1")));
        }

        if (Item.CriticRating.HasValue)
        {
            // TODO: Style correctly
            mediaInfo.Add(new MediaInfoItem(Item.CriticRating.Value.ToString()));
        }

        if (Item.RunTimeTicks.HasValue)
        {
            mediaInfo.Add(new MediaInfoItem(GetEndsAt(Item.RunTimeTicks.Value)));
        }

        MediaInfo = new ObservableCollection<MediaInfoItem>(mediaInfo);

        if (Item.MediaSources is not null && Item.MediaSources.Count > 0)
        {
            SourceContainers = new ObservableCollection<MediaSourceInfoWrapper>(Item.MediaSources.Select(s => new MediaSourceInfoWrapper(s.Name!, s)));

            // This will trigger OnSelectedSourceContainerChanged, which populates the video, audio, and subtitle drop-downs.
            SelectedSourceContainer = SourceContainers[0];
        }

        TagLine = Item.Taglines is not null && Item.Taglines.Count > 0 ? Item.Taglines[0] : null;
        Overview = Item.Overview;
        Tags = Item.Tags is not null ? $"Tags: {string.Join(", ", Item.Tags)}" : null;

        UpdateUserData();

        Task<Section?>[] sectionTasks =
        [
            GetNextUpSectionAsync(),
            GetChildrenSectionAsync(),
            // TODO: Cast & Crew -->
            // TODO: More Like This -->
        ];

        List<Section> sections = new(sectionTasks.Length);
        foreach (Task<Section?> sectionTask in sectionTasks)
        {
            Section? section = await sectionTask;
            if (section is not null)
            {
                sections.Add(section);
            }
        }

        Sections = sections;
    }

    partial void OnSelectedSourceContainerChanged(MediaSourceInfoWrapper? value)
    {
        if (value is not null)
        {
            DetermineVideoOptions(value.Value);
            DetermineAudioOptions(value.Value);
            DetermineSubtitleOptions(value.Value);
        }
    }

    private void DetermineVideoOptions(MediaSourceInfo mediaSourceInfo)
    {
        List<MediaStream> videoStreams = mediaSourceInfo.MediaStreams!
            .Where(s => s.Type == MediaStream_Type.Video)
            .OrderBy(s => s, MediaStreamComparer.Instance)
            .ToList();
        int? selectedIndex = videoStreams.Count > 0 ? videoStreams[0].Index : -1;

        MediaStreamOption? selectedOption = null;
        List<MediaStreamOption> options = new(videoStreams.Count);
        foreach (MediaStream videoStream in videoStreams)
        {
            string? displayTitle = videoStream.DisplayTitle;
            if (string.IsNullOrEmpty(displayTitle))
            {
                // DisplayTitle isn't always populated for video
                // TODO: Get the resolution text and codec. See /src/controllers/itemDetails/index.js::renderVideoSelections
                displayTitle = "TODO";
            }

            MediaStreamOption option = new(displayTitle, videoStream.Index);
            options.Add(option);

            if (selectedOption is null || videoStream.Index == selectedIndex)
            {
                selectedOption = option;
            }
        }

        VideoStreams = new ObservableCollection<MediaStreamOption>(options);
        SelectedVideoStream = selectedOption;
    }

    private void DetermineAudioOptions(MediaSourceInfo mediaSourceInfo)
    {
        List<MediaStream> audioStreams = mediaSourceInfo.MediaStreams!
            .Where(s => s.Type == MediaStream_Type.Audio)
            .OrderBy(s => s, MediaStreamComparer.Instance)
            .ToList();
        int? selectedIndex = mediaSourceInfo.DefaultAudioStreamIndex;

        MediaStreamOption? selectedOption = null;
        List<MediaStreamOption> options = new(audioStreams.Count);
        foreach (MediaStream audioStream in audioStreams)
        {
            MediaStreamOption option = new(audioStream.DisplayTitle!, audioStream.Index);
            options.Add(option);

            if (selectedOption is null || audioStream.Index == selectedIndex)
            {
                selectedOption = option;
            }
        }

        AudioStreams = new ObservableCollection<MediaStreamOption>(options);
        SelectedAudioStream = selectedOption;
    }

    private void DetermineSubtitleOptions(MediaSourceInfo mediaSourceInfo)
    {
        List<MediaStream> subtitleStreams = mediaSourceInfo.MediaStreams!
            .Where(s => s.Type == MediaStream_Type.Subtitle)
            .OrderBy(s => s, MediaStreamComparer.Instance)
            .ToList();

        MediaStreamOption? selectedOption = null;
        List<MediaStreamOption> options = new(subtitleStreams.Count + 1);
        options.Add(MediaStreamOption.SubtitlesOff);

        int selectedIndex;
        if (mediaSourceInfo.DefaultSubtitleStreamIndex.HasValue)
        {
            selectedIndex = mediaSourceInfo.DefaultSubtitleStreamIndex.Value;
        }
        else
        {
            selectedIndex = -1;
            selectedOption = MediaStreamOption.SubtitlesOff;
        }

        foreach (MediaStream subtitleStream in subtitleStreams)
        {
            MediaStreamOption option = new(subtitleStream.DisplayTitle!, subtitleStream.Index);
            options.Add(option);

            if (selectedOption is null || subtitleStream.Index == selectedIndex)
            {
                selectedOption = option;
            }
        }

        SubtitleStreams = new ObservableCollection<MediaStreamOption>(options);
        SelectedSubtitleStream = selectedOption;
    }

    public void Play()
    {
        // TODO: Support playlists
        if (SelectedSourceContainer is not null)
        {
            _navigationManager.NavigateToVideo(
                Item!,
                SelectedSourceContainer.Value.Id!,
                SelectedAudioStream?.Index,
                SelectedSubtitleStream?.Index);
        }
    }

    public async void PlayTrailer()
    {
        if (Item is null)
        {
            return;
        }

        if (Item.LocalTrailerCount > 0)
        {
            List<BaseItemDto>? localTrailers = await _jellyfinApiClient.Items[Item.Id!.Value].LocalTrailers.GetAsync();
            if (localTrailers is not null && localTrailers.Count > 0)
            {
                // TODO play all the trailers instead of just the first?
                _navigationManager.NavigateToVideo(
                    localTrailers[0],
                    mediaSourceId: null,
                    audioStreamIndex: null,
                    subtitleStreamIndex: null);
                return;
            }
        }

        if (Item.RemoteTrailers is not null && Item.RemoteTrailers.Count > 0)
        {
            // TODO play all the trailers instead of just the first?
            Uri videoUri = GetWebVideoUri(Item.RemoteTrailers[0].Url!);

            _navigationManager.NavigateToWebVideo(videoUri);
            return;
        }
    }

    public async void TogglePlayed()
    {
        if (Item is null)
        {
            return;
        }

        Item.UserData = Item.UserData!.Played.GetValueOrDefault()
            ? await _jellyfinApiClient.UserPlayedItems[Item.Id!.Value].DeleteAsync()
            : await _jellyfinApiClient.UserPlayedItems[Item.Id!.Value].PostAsync();
        UpdateUserData();
    }

    public async void ToggleFavorite()
    {
        if (Item is null)
        {
            return;
        }

        Item.UserData = Item.UserData!.IsFavorite.GetValueOrDefault()
            ? await _jellyfinApiClient.UserFavoriteItems[Item.Id!.Value].DeleteAsync()
            : await _jellyfinApiClient.UserFavoriteItems[Item.Id!.Value].PostAsync();
        UpdateUserData();
    }

    // Return a string in '{}h {}m' format for duration.
    private static string GetDisplayDuration(long ticks)
    {
        int totalMinutes = (int)Math.Round(ticks / 600000000d);
        if (totalMinutes == 0)
        {
            totalMinutes = 1;
        }

        double totalHours = totalMinutes / 60;
        double remainderMinutes = totalMinutes % 60;

        StringBuilder sb = new();
        if (totalHours > 0)
        {
            sb.Append(totalHours);
            sb.Append("h ");
        }

        sb.Append(remainderMinutes);
        sb.Append('m');

        return sb.ToString();
    }

    private static string GetEndsAt(long ticks)
    {
        DateTime endDate = DateTime.Now + TimeSpan.FromTicks(ticks);
        return $"Ends at {endDate:t}";
    }

    private void UpdateUserData()
    {
        if (Item is null)
        {
            return;
        }

        IsPlayed = Item.UserData!.Played.GetValueOrDefault();
        PlayStateBrush = IsPlayed ? OnBrush : OffBrush;

        IsFavorite = Item.UserData.IsFavorite.GetValueOrDefault();
        FavoriteBrush = IsFavorite ? OnBrush : OffBrush;
    }

    private async Task<Section?> GetNextUpSectionAsync()
    {
        if (Item is null)
        {
            return null;
        }

        if (Item.Type != BaseItemDto_Type.Series)
        {
            return null;
        }

        BaseItemDtoQueryResult? result = await _jellyfinApiClient.Shows.NextUp.GetAsync(request =>
        {
            request.QueryParameters.SeriesId = Item.Id;
        });
        if (result?.Items is null || result.Items.Count == 0)
        {
            return null;
        }

        return new Section
        {
            Name = "Next Up",
            Items = result.Items,
            NavigateToItemCommand = NavigateToItemCommand,
        };
    }

    private async Task<Section?> GetChildrenSectionAsync()
    {
        if (Item is null)
        {
            return null;
        }

        if (!Item.IsFolder.GetValueOrDefault())
        {
            return null;
        }

        BaseItemDtoQueryResult? result;
        if (Item.Type == BaseItemDto_Type.Series)
        {
            result = await _jellyfinApiClient.Shows[Item.Id!.Value].Seasons.GetAsync(request =>
            {
                request.QueryParameters.Fields = [ItemFields.ItemCounts, ItemFields.PrimaryImageAspectRatio, ItemFields.CanDelete, ItemFields.MediaSourceCount];
            });
        }
        else if (Item.Type == BaseItemDto_Type.Season)
        {
            result = await _jellyfinApiClient.Shows[Item.SeriesId!.Value].Episodes.GetAsync(request =>
            {
                request.QueryParameters.SeasonId = Item.Id;
                request.QueryParameters.Fields = [ItemFields.ItemCounts, ItemFields.PrimaryImageAspectRatio, ItemFields.CanDelete, ItemFields.MediaSourceCount, ItemFields.Overview];
            });
        }
        else
        {
            result = await _jellyfinApiClient.Items.GetAsync(request =>
            {
                request.QueryParameters.ParentId = Item.Id;
                request.QueryParameters.Fields = [ItemFields.ItemCounts, ItemFields.PrimaryImageAspectRatio, ItemFields.CanDelete, ItemFields.MediaSourceCount];

                if (Item.Type == BaseItemDto_Type.MusicAlbum)
                {
                    request.QueryParameters.SortBy = [ItemSortBy.ParentIndexNumber, ItemSortBy.IndexNumber, ItemSortBy.SortName];
                }
                else if (Item.Type != BaseItemDto_Type.BoxSet)
                {
                    request.QueryParameters.SortBy = [ItemSortBy.SortName];
                }
                else if (Item.Type == BaseItemDto_Type.MusicArtist)
                {
                    request.QueryParameters.SortBy = [ItemSortBy.PremiereDate, ItemSortBy.ProductionYear, ItemSortBy.SortName];
                }
            });
        }

        if (result?.Items is null)
        {
            return null;
        }

        if (Item.Type == BaseItemDto_Type.Episode && result.Items.Count < 2)
        {
            return null;
        }

        string sectionName = Item.Type switch
        {
            BaseItemDto_Type.Series => "Seasons",
            BaseItemDto_Type.Season => "Episodes",
            BaseItemDto_Type.MusicAlbum => "Tracks",
            _ => "Items"
        };

        // TODO: Support list view for Type == MusicAlbum | Season
        return new Section
        {
            Name = sectionName,
            Items = result.Items,
            NavigateToItemCommand = NavigateToItemCommand,
        };
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item)
    {
        _navigationManager.NavigateToItem(item);
    }

    private static Uri GetWebVideoUri(string url)
    {
        Match match = YouTubeRegex.Match(url);
        if (match.Success)
        {
            string youtubeBaseUrl = match.Groups["urlBase"].Value;
            string youtubeVideoId = match.Groups["id"].Value;

            // Use the embed url with autoplay enabled.
            return new($"{youtubeBaseUrl}/embed/{youtubeVideoId}?rel=0&autoplay=1");
        }

        // Fallback to the full url.
        return new Uri(url);
    }

    private static readonly Regex YouTubeRegex = new(
        @"(?<urlBase>https://www.youtube.com)/watch\?v=(?<id>[^&]+)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private sealed class MediaStreamComparer : IComparer<MediaStream>
    {
        private MediaStreamComparer()
        {
        }

        public static MediaStreamComparer Instance { get; } = new MediaStreamComparer();

        public int Compare(MediaStream? x, MediaStream? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null && y is not null)
            {
                return -1;
            }

            if (x is not null && y is null)
            {
                return 1;
            }

            int cmp = Compare(x!.IsExternal.GetValueOrDefault(), y!.IsExternal.GetValueOrDefault());
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = Compare(x.IsForced.GetValueOrDefault(), y.IsForced.GetValueOrDefault());
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = Compare(x.IsDefault.GetValueOrDefault(), y.IsDefault.GetValueOrDefault());
            if (cmp != 0)
            {
                return cmp;
            }

            return x.Index.GetValueOrDefault() - y.Index.GetValueOrDefault();

            static int Compare(bool x, bool y) => (x ? 1 : 0) - (y ? 1 : 0);
        }
    }
}