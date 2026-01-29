using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Controls;
using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class VideoViewModel : ObservableObject
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly JellyfinImageResolver _imageResolver;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly DeviceProfileManager _deviceProfileManager;
    private readonly DispatcherTimer _progressTimer;
    private MediaPlayerElement? _playerElement;
    private CustomMediaTransportControls? _transportControls;
    private PlaybackProgressInfo? _playbackProgressInfo;
    private BaseItemDto? _currentItem;
    private MediaSourceInfo? _currentMediaSource;
    private MediaPlaybackItem? _currentPlaybackItem;
    private double _volumeBeforeMute = 1.0;
    private bool _isDirectPlay;

    public VideoViewModel(
        JellyfinApiClient jellyfinApiClient,
        JellyfinImageResolver imageResolver,
        JellyfinSdkSettings sdkClientSettings,
        DeviceProfileManager deviceProfileManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
        _imageResolver = imageResolver;
        _sdkClientSettings = sdkClientSettings;
        _deviceProfileManager = deviceProfileManager;

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _progressTimer.Tick += (sender, e) => TimerTick();
    }

    public Uri? BackdropImageUri { get; set => SetProperty(ref field, value); }

    public bool ShowBackdropImage { get; set => SetProperty(ref field, value); }

    private static readonly TimeSpan RewindInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FastForwardInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rewind playback by 10 seconds.
    /// </summary>
    [RelayCommand]
    public void Rewind()
    {
        if (_playerElement?.MediaPlayer?.PlaybackSession is null)
        {
            return;
        }

        TimeSpan newPosition = _playerElement.MediaPlayer.PlaybackSession.Position - RewindInterval;
        if (newPosition < TimeSpan.Zero)
        {
            newPosition = TimeSpan.Zero;
        }

        _playerElement.MediaPlayer.PlaybackSession.Position = newPosition;
        UpdateEndsAtText();
    }

    /// <summary>
    /// Fast forward playback by 30 seconds.
    /// </summary>
    [RelayCommand]
    public void FastForward()
    {
        if (_playerElement?.MediaPlayer?.PlaybackSession is null)
        {
            return;
        }

        TimeSpan duration = _playerElement.MediaPlayer.PlaybackSession.NaturalDuration;
        TimeSpan newPosition = _playerElement.MediaPlayer.PlaybackSession.Position + FastForwardInterval;

        if (newPosition > duration)
        {
            newPosition = duration;
        }

        _playerElement.MediaPlayer.PlaybackSession.Position = newPosition;
        UpdateEndsAtText();
    }

    /// <summary>
    /// Toggle play/pause.
    /// </summary>
    [RelayCommand]
    public void TogglePlayPause()
    {
        if (_playerElement?.MediaPlayer is null)
        {
            return;
        }

        if (_playerElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            _playerElement.MediaPlayer.Pause();
        }
        else
        {
            _playerElement.MediaPlayer.Play();
        }
    }

    /// <summary>
    /// Toggle favorite status for current item.
    /// </summary>
    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        try
        {
            if (_currentItem is null)
            {
                return;
            }

            bool wasFavorite = _currentItem.UserData?.IsFavorite ?? false;
            _currentItem.UserData = wasFavorite
                ? await _jellyfinApiClient.UserFavoriteItems[_currentItem.Id!.Value].DeleteAsync()
                : await _jellyfinApiClient.UserFavoriteItems[_currentItem.Id!.Value].PostAsync();

            bool isFavorite = _currentItem.UserData?.IsFavorite ?? false;
            _transportControls?.IsFavorite = isFavorite;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ToggleFavoriteAsync: {ex}");
        }
    }

    /// <summary>
    /// Toggle mute on/off.
    /// </summary>
    [RelayCommand]
    public void ToggleMute()
    {
        if (_playerElement?.MediaPlayer is null)
        {
            return;
        }

        if (_playerElement.MediaPlayer.IsMuted)
        {
            // Unmute - restore previous volume
            _playerElement.MediaPlayer.IsMuted = false;
            _playerElement.MediaPlayer.Volume = _volumeBeforeMute;
        }
        else
        {
            // Mute - save current volume
            _volumeBeforeMute = _playerElement.MediaPlayer.Volume;
            _playerElement.MediaPlayer.IsMuted = true;
        }

        UpdateTransportControlsVolumeState();
    }

    /// <summary>
    /// Change volume to a specific value.
    /// </summary>
    [RelayCommand]
    public void ChangeVolume(double volume)
    {
        if (_playerElement?.MediaPlayer is null)
        {
            return;
        }

        _playerElement.MediaPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
        _playerElement.MediaPlayer.IsMuted = false;
        UpdateTransportControlsVolumeState();
    }

    private void UpdateTransportControlsVolumeState()
    {
        if (_transportControls is null || _playerElement?.MediaPlayer is null)
        {
            return;
        }

        _transportControls.Volume = _playerElement.MediaPlayer.Volume;
        _transportControls.IsMuted = _playerElement.MediaPlayer.IsMuted;
    }

    /// <summary>
    /// Adjust volume by a delta (-1.0 to 1.0).
    /// </summary>
    public void AdjustVolume(double delta)
    {
        if (_playerElement?.MediaPlayer is null)
        {
            return;
        }

        double newVolume = _playerElement.MediaPlayer.Volume + delta;
        ChangeVolume(newVolume);
    }

    public async void PlayVideo(Video.Parameters parameters, MediaPlayerElement playerElement, CustomMediaTransportControls transportControls)
    {
        try
        {
            _transportControls = transportControls;
            _currentItem = parameters.Item;
            BaseItemDto item = _currentItem;
            _playerElement = playerElement;

            // Bind commands to transport controls
            _transportControls.PlayPauseCommand = TogglePlayPauseCommand;
            _transportControls.RewindCommand = RewindCommand;
            _transportControls.FastForwardCommand = FastForwardCommand;
            _transportControls.ToggleFavoriteCommand = ToggleFavoriteCommand;
            _transportControls.ToggleMuteCommand = ToggleMuteCommand;
            _transportControls.ChangeVolumeCommand = ChangeVolumeCommand;
            _transportControls.SelectAudioTrackCommand = SelectAudioTrackCommand;
            _transportControls.SelectSubtitleTrackCommand = SelectSubtitleTrackCommand;
            _transportControls.ChangePlaybackSpeedCommand = ChangePlaybackSpeedCommand;
            _transportControls.ChangeStretchModeCommand = ChangeStretchModeCommand;
            _transportControls.ShowPlaybackInfoCommand = ShowPlaybackInfoCommand;

            // Initialize state
            _transportControls.IsFavorite = item.UserData?.IsFavorite ?? false;

            // Calculate initial "Ends at" from metadata
            // TODO: Once resume functionality is implemented, use the actual start position
            // (e.g., item.UserData?.PlaybackPositionTicks) instead of assuming start from beginning.
            if (item.RunTimeTicks.HasValue)
            {
                TimeSpan remaining = TimeSpan.FromTicks(item.RunTimeTicks.Value);
                DateTime endTime = DateTime.Now + remaining;
                _transportControls.EndsAtText = $"Ends at {endTime:t}";
            }

            BackdropImageUri = _imageResolver.GetBackdropImageUri(item, 1920);
            ShowBackdropImage = true;

            _playbackProgressInfo = new PlaybackProgressInfo
            {
                ItemId = item.Id!.Value,
                AudioStreamIndex = parameters.AudioStreamIndex,
                SubtitleStreamIndex = parameters.SubtitleStreamIndex,
            };

            // Create the MediaPlayer and set up event handlers (first-time only)
#pragma warning disable CA2000 // Dispose objects before losing scope. Disposed in StopVideo.
            _playerElement.SetMediaPlayer(new MediaPlayer());
#pragma warning restore CA2000 // Dispose objects before losing scope

            // Initialize volume state on transport controls
            UpdateTransportControlsVolumeState();

            _playerElement.MediaPlayer.MediaEnded += async (mp, o) =>
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    UpdatePositionTicks);

                await ReportStoppedAsync();
            };

            _playerElement.MediaPlayer.PlaybackSession.PlaybackStateChanged += async (session, obj) =>
            {
                if (session.PlaybackState == MediaPlaybackState.None)
                {
                    // The calls below throw in this scenario
                    return;
                }

                if (session.PlaybackState == MediaPlaybackState.Playing && ShowBackdropImage)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () => ShowBackdropImage = false);
                }

                _playbackProgressInfo.CanSeek = session.CanSeek;
                _playbackProgressInfo.PositionTicks = session.Position.Ticks;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        // Update buffering state
                        _transportControls.IsBuffering = session.PlaybackState == MediaPlaybackState.Buffering;

                        if (session.PlaybackState == MediaPlaybackState.Playing)
                        {
                            _playbackProgressInfo.IsPaused = false;
                            _transportControls.IsPlaying = true;
                            UpdateEndsAtText();
                        }
                        else if (session.PlaybackState == MediaPlaybackState.Paused)
                        {
                            _playbackProgressInfo.IsPaused = true;
                            _transportControls.IsPlaying = false;
                        }
                    });

                // TODO: Only update if something actually changed?
                await ReportProgressAsync();
            };

            // TODO: Once resume functionality is implemented, use the actual start position
            // (e.g., item.UserData?.PlaybackPositionTicks) instead of assuming start from beginning.
            await StartPlaybackAsync(parameters.MediaSourceId, TimeSpan.Zero);

            await ReportStartedAsync();

            _progressTimer.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in PlayVideo: {ex}");
        }
    }

    public async void StopVideo()
    {
        try
        {
            _progressTimer.Stop();

            UpdatePositionTicks();

            MediaPlayer? player = _playerElement?.MediaPlayer;
            if (player is not null)
            {
                player.Pause();

                MediaPlaybackItem mediaPlaybackItem = (MediaPlaybackItem)player.Source;

                // Detach components from each other
                _playerElement!.SetMediaPlayer(null);
                player.Source = null;

                // Dispose components
                mediaPlaybackItem.Source.Dispose();
                player.Dispose();
            }

            // Clear command bindings
            if (_transportControls != null)
            {
                _transportControls.PlayPauseCommand = null;
                _transportControls.RewindCommand = null;
                _transportControls.FastForwardCommand = null;
                _transportControls.ToggleFavoriteCommand = null;
                _transportControls.ToggleMuteCommand = null;
                _transportControls.ChangeVolumeCommand = null;
                _transportControls.SelectAudioTrackCommand = null;
                _transportControls.SelectSubtitleTrackCommand = null;
                _transportControls.ChangePlaybackSpeedCommand = null;
                _transportControls.ChangeStretchModeCommand = null;
                _transportControls.ShowPlaybackInfoCommand = null;
            }

            _currentItem = null;
            _currentMediaSource = null;
            CleanupCurrentPlaybackItem();

            await DisplayModeManager.SetDefaultDisplayModeAsync();

            await ReportStoppedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in StopVideo: {ex}");
        }
    }

    private async Task ReportStartedAsync()
    {
        if (_playbackProgressInfo is null)
        {
            return;
        }

        await _jellyfinApiClient.Sessions.Playing.PostAsync(
                new PlaybackStartInfo
                {
                    ItemId = _playbackProgressInfo.ItemId,
                    MediaSourceId = _playbackProgressInfo.MediaSourceId,
                    PlaySessionId = _playbackProgressInfo.PlaySessionId,
                    AudioStreamIndex = _playbackProgressInfo.AudioStreamIndex,
                    SubtitleStreamIndex = _playbackProgressInfo.SubtitleStreamIndex,
                });
    }

    private async Task ReportStoppedAsync()
    {
        if (_playbackProgressInfo is null)
        {
            return;
        }

        await _jellyfinApiClient.Sessions.Playing.Stopped.PostAsync(
                new PlaybackStopInfo
                {
                    ItemId = _playbackProgressInfo.ItemId,
                    MediaSourceId = _playbackProgressInfo.MediaSourceId,
                    PlaySessionId = _playbackProgressInfo.PlaySessionId,
                    PositionTicks = _playbackProgressInfo.PositionTicks,
                });
    }

    private void UpdatePositionTicks()
    {
        if (_playbackProgressInfo is null || _playerElement is null)
        {
            return;
        }

        long currentTicks = _playerElement.MediaPlayer.PlaybackSession.Position.Ticks;
        if (currentTicks < 0)
        {
            currentTicks = 0;
        }

        _playbackProgressInfo.PositionTicks = currentTicks;
    }

    private void PopulateTrackLists(MediaSourceInfo mediaSourceInfo, int? selectedAudioIndex, int? selectedSubtitleIndex)
    {
        if (mediaSourceInfo.MediaStreams is null || _transportControls is null)
        {
            return;
        }

        // Audio tracks - UWP index matches iteration order
        List<TrackInfo> audioTracks = [];
        int defaultAudioIndex = selectedAudioIndex ?? mediaSourceInfo.DefaultAudioStreamIndex ?? -1;
        int uwpAudioIndex = 0;
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams.Where(s => s.Type == MediaStream_Type.Audio))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            audioTracks.Add(new TrackInfo(
                stream.Index!.Value,
                uwpAudioIndex,
                displayName,
                stream.IsExternal.GetValueOrDefault(),
                stream.Codec));
            uwpAudioIndex++;
        }
        _transportControls.AudioTracks = audioTracks;
        _transportControls.SelectedAudioIndex = defaultAudioIndex;

        // Subtitle tracks - only include supported formats.
        // UWP's TimedMetadataTracks collection orders embedded tracks first, then external tracks
        // in the order they were added to ExternalTimedTextSources.
        // See: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/media-playback-with-mediasource
        List<TrackInfo> subtitleTracks = [];
        int defaultSubtitleIndex = selectedSubtitleIndex ?? mediaSourceInfo.DefaultSubtitleStreamIndex ?? -1;
        int uwpSubtitleIndex = 0;

        // First pass: embedded subtitles (supported formats only)
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && !s.IsExternal.GetValueOrDefault()))
        {
            if (!IsSubtitleFormatSupported(stream))
            {
                // If the default subtitle is unsupported, clear the selection
                if (stream.Index == defaultSubtitleIndex)
                {
                    defaultSubtitleIndex = -1;
                }

                continue;
            }

            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                uwpSubtitleIndex,
                displayName,
                IsExternal: false,
                stream.Codec));
            uwpSubtitleIndex++;
        }

        // Second pass: external subtitles (supported formats only)
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && s.IsExternal.GetValueOrDefault()))
        {
            if (!IsSubtitleFormatSupported(stream))
            {
                // If the default subtitle is unsupported, clear the selection
                if (stream.Index == defaultSubtitleIndex)
                {
                    defaultSubtitleIndex = -1;
                }

                continue;
            }

            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                uwpSubtitleIndex,
                displayName,
                IsExternal: true,
                stream.Codec));
            uwpSubtitleIndex++;
        }

        _transportControls.SubtitleTracks = subtitleTracks;
        _transportControls.SelectedSubtitleIndex = defaultSubtitleIndex;
    }

    private void OnTimedMetadataTracksChanged(MediaPlaybackItem sender, IVectorChangedEventArgs args)
    {
        // Capture the sender reference for use in the dispatcher callback
        MediaPlaybackItem playbackItem = sender;

        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
            () =>
            {
                try
                {
                    // Verify the playback item is still current
                    if (playbackItem != _currentPlaybackItem)
                    {
                        return;
                    }

                    // Present the selected subtitle track if one is selected
                    int selectedIndex = _playbackProgressInfo?.SubtitleStreamIndex ?? -1;
                    int? uwpIndex = GetSubtitleUwpIndex(selectedIndex);
                    if (uwpIndex.HasValue && uwpIndex.Value < playbackItem.TimedMetadataTracks.Count)
                    {
                        playbackItem.TimedMetadataTracks.SetPresentationMode((uint)uwpIndex.Value, TimedMetadataTrackPresentationMode.PlatformPresented);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnTimedMetadataTracksChanged: {ex}");
                }
            });
    }

    /// <summary>
    /// Looks up the precomputed UWP track index for a given Jellyfin subtitle stream index.
    /// </summary>
    private int? GetSubtitleUwpIndex(int jellyfinIndex)
        => _transportControls?.SubtitleTracks?.FirstOrDefault(t => t.JellyfinIndex == jellyfinIndex)?.UwpTrackIndex;

    [RelayCommand]
    private void SelectAudioTrack(TrackInfo track)
    {
        _playbackProgressInfo?.AudioStreamIndex = track.JellyfinIndex;
        _transportControls?.SelectedAudioIndex = track.JellyfinIndex;

        // Try seamless switch if we're in direct play
        if (_isDirectPlay
            && _currentPlaybackItem is not null
            && track.UwpTrackIndex.HasValue
            && track.UwpTrackIndex.Value < _currentPlaybackItem.AudioTracks.Count)
        {
            _currentPlaybackItem.AudioTracks.SelectedIndex = track.UwpTrackIndex.Value;
            Debug.WriteLine($"Seamless audio track switch to Jellyfin index {track.JellyfinIndex} (UWP index {track.UwpTrackIndex})");
            return;
        }

        // Fall back to restarting playback (e.g., for transcoding scenarios)
        Debug.WriteLine($"Restarting playback for audio track {track.JellyfinIndex}");
        RestartPlaybackWithCurrentPosition();
    }

    [RelayCommand]
    private void SelectSubtitleTrack(TrackInfo track)
    {
        // JellyfinIndex of -1 means "off"
        _playbackProgressInfo?.SubtitleStreamIndex = track.JellyfinIndex;
        _transportControls?.SelectedSubtitleIndex = track.JellyfinIndex;

        // Try seamless switch - set each track's mode in a single pass
        if (_isDirectPlay
            && _currentPlaybackItem is not null
            && (!track.UwpTrackIndex.HasValue || track.UwpTrackIndex.Value < _currentPlaybackItem.TimedMetadataTracks.Count))
        {
            for (uint i = 0; i < _currentPlaybackItem.TimedMetadataTracks.Count; i++)
            {
                var mode = i == track.UwpTrackIndex
                    ? TimedMetadataTrackPresentationMode.PlatformPresented
                    : TimedMetadataTrackPresentationMode.Disabled;
                _currentPlaybackItem.TimedMetadataTracks.SetPresentationMode(i, mode);
            }

            Debug.WriteLine(track.UwpTrackIndex.HasValue
                ? $"Seamless subtitle switch to Jellyfin index {track.JellyfinIndex} (UWP index {track.UwpTrackIndex})"
                : "Subtitles disabled");
            return;
        }

        // Fall back to restarting playback (e.g., for transcoding scenarios or unmapped tracks)
        Debug.WriteLine($"Restarting playback for subtitle track {track.JellyfinIndex}");
        RestartPlaybackWithCurrentPosition();
    }

    #region Playback Setup Helpers

    /// <summary>
    /// Creates a MediaSource from a URI, handling adaptive streaming if needed.
    /// </summary>
    private static async Task<MediaSource> CreateMediaSourceAsync(Uri mediaUri, bool isAdaptive)
    {
        if (isAdaptive)
        {
            AdaptiveMediaSourceCreationResult result = await AdaptiveMediaSource.CreateFromUriAsync(mediaUri);
            if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                AdaptiveMediaSource ams = result.MediaSource;
                ams.InitialBitrate = ams.AvailableBitrates.Max();
                return MediaSource.CreateFromAdaptiveMediaSource(ams);
            }
        }

        return MediaSource.CreateFromUri(mediaUri);
    }

    /// <summary>
    /// Cleans up the current playback item, unsubscribing from events and disposing resources.
    /// </summary>
    private void CleanupCurrentPlaybackItem()
    {
        if (_currentPlaybackItem is not null)
        {
            _currentPlaybackItem.TimedMetadataTracksChanged -= OnTimedMetadataTracksChanged;
            _currentPlaybackItem.Source?.Dispose();
        }

        _currentPlaybackItem = null;
        _isDirectPlay = false;
    }

    /// <summary>
    /// Fetches playback info from the Jellyfin API.
    /// </summary>
    private async Task<(MediaSourceInfo? MediaSource, string? PlaySessionId)> GetPlaybackInfoAsync(
        Guid itemId,
        string? mediaSourceId,
        int? audioStreamIndex,
        int? subtitleStreamIndex)
    {
        DeviceProfile deviceProfile = _deviceProfileManager.Profile;

        // Note: This mutates the shared device profile. That's probably OK as long as all accesses do this.
        // TODO: Look into making a copy instead.
        deviceProfile.MaxStreamingBitrate = await DetectBitrateAsync();

        PlaybackInfoDto playbackInfo = new()
        {
            DeviceProfile = deviceProfile,
            MediaSourceId = mediaSourceId,
            AudioStreamIndex = audioStreamIndex,
            SubtitleStreamIndex = subtitleStreamIndex,
        };

        PlaybackInfoResponse? response = await _jellyfinApiClient.Items[itemId].PlaybackInfo.PostAsync(playbackInfo);

        if (response?.MediaSources is null || response.MediaSources.Count == 0)
        {
            return (null, null);
        }

        return (response.MediaSources[0], response.PlaySessionId);
    }

    #endregion

    private async void RestartPlaybackWithCurrentPosition()
    {
        if (_playerElement?.MediaPlayer is null || _currentItem is null || _currentMediaSource is null)
        {
            return;
        }

        try
        {
            // Save current position before restarting
            TimeSpan currentPosition = _playerElement.MediaPlayer.PlaybackSession.Position;
            await StartPlaybackAsync(_currentMediaSource.Id, currentPosition);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restarting playback: {ex}");
        }
    }

    /// <summary>
    /// Starts or restarts playback with the specified media source and position.
    /// </summary>
    /// <param name="mediaSourceId">The media source ID to play.</param>
    /// <param name="startPosition">The position to start playback from.</param>
    private async Task StartPlaybackAsync(string? mediaSourceId, TimeSpan startPosition)
    {
        if (_currentItem is null || _playbackProgressInfo is null)
        {
            return;
        }

        // Get playback info with current track selections
        (MediaSourceInfo? mediaSourceInfo, string? playSessionId) = await GetPlaybackInfoAsync(
            _currentItem.Id!.Value,
            mediaSourceId,
            _playbackProgressInfo.AudioStreamIndex,
            _playbackProgressInfo.SubtitleStreamIndex);

        if (mediaSourceInfo is null)
        {
            return;
        }

        _currentMediaSource = mediaSourceInfo;

        // Update play session and media source ID
        _playbackProgressInfo.PlaySessionId = playSessionId;
        _playbackProgressInfo.MediaSourceId = mediaSourceInfo.Id;

        // Populate audio and subtitle track lists
        PopulateTrackLists(mediaSourceInfo, _playbackProgressInfo.AudioStreamIndex, _playbackProgressInfo.SubtitleStreamIndex);

        // Create new playback item
        MediaPlaybackItem? playbackItem = await CreatePlaybackItemAsync(mediaSourceInfo, startPosition);
        if (playbackItem is null)
        {
            return;
        }

        // Set display mode based on video stream properties (may change on restart if transcoding)
        MediaStream videoStream = mediaSourceInfo.MediaStreams!.First(stream => stream.Type == MediaStream_Type.Video);
        await DisplayModeManager.SetBestDisplayModeAsync(
            (uint)videoStream.Width!.Value,
            (uint)videoStream.Height!.Value,
            (double)videoStream.RealFrameRate!.Value,
            videoStream.VideoRangeType!.Value);

        // Start playback
        _playerElement!.Source = playbackItem;
        _playerElement.MediaPlayer!.Play();
    }

    /// <summary>
    /// Creates a MediaPlaybackItem and sets it up with event handlers.
    /// Cleans up any existing playback item first.
    /// </summary>
    private async Task<MediaPlaybackItem?> CreatePlaybackItemAsync(MediaSourceInfo mediaSourceInfo, TimeSpan startPosition)
    {
        Uri? mediaUri = BuildMediaUri(mediaSourceInfo);
        if (mediaUri is null)
        {
            return null;
        }

        bool isAdaptive = !mediaSourceInfo.SupportsDirectPlay.GetValueOrDefault()
                       && !mediaSourceInfo.SupportsDirectStream.GetValueOrDefault()
                       && mediaSourceInfo.TranscodingSubProtocol == MediaSourceInfo_TranscodingSubProtocol.Hls;

        MediaSource mediaSource = await CreateMediaSourceAsync(mediaUri, isAdaptive);

        // Add all external subtitles upfront for seamless switching
        AddAllExternalSubtitles(mediaSource, mediaSourceInfo);

        MediaPlaybackItem? playbackItem = new(mediaSource, startPosition);

        // Cleanup old playback item if any
        CleanupCurrentPlaybackItem();

        _currentPlaybackItem = playbackItem;
        _isDirectPlay = mediaSourceInfo.SupportsDirectPlay.GetValueOrDefault() || mediaSourceInfo.SupportsDirectStream.GetValueOrDefault();

        // Subscribe to track change events for presenting default subtitle
        playbackItem.TimedMetadataTracksChanged += OnTimedMetadataTracksChanged;

        return playbackItem;
    }

    /// <summary>
    /// Determines if a subtitle format is supported by UWP MediaPlayer.
    /// </summary>
    /// <param name="stream">The subtitle media stream to check.</param>
    /// <returns>True if the subtitle format is supported for playback.</returns>
    private static bool IsSubtitleFormatSupported(MediaStream stream)
    {
        if (stream.Codec is null)
        {
            return false;
        }

        return stream.IsExternal.GetValueOrDefault()
            ? DeviceProfileManager.SupportedExternalSubtitleFormats.Contains(stream.Codec)
            : DeviceProfileManager.SupportedEmbeddedSubtitleFormats.Contains(stream.Codec);
    }

    /// <summary>
    /// Adds all external subtitles in supported formats to the media source for seamless switching.
    /// UWP TimedTextSource only supports SRT (subrip) and VTT formats.
    /// External subtitles are added in the same order as they appear in MediaStreams.
    /// </summary>
    private void AddAllExternalSubtitles(MediaSource mediaSource, MediaSourceInfo mediaSourceInfo)
    {
        if (mediaSourceInfo.MediaStreams is null)
        {
            return;
        }

        foreach (MediaStream subtitleTrack in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && s.IsExternal.GetValueOrDefault()))
        {
            if (!IsSubtitleFormatSupported(subtitleTrack))
            {
                Debug.WriteLine($"Skipping unsupported external subtitle format: {subtitleTrack.Codec} (index {subtitleTrack.Index})");
                continue;
            }

            string? subtitleUrl = subtitleTrack.DeliveryUrl;
            if (string.IsNullOrEmpty(subtitleUrl))
            {
                Debug.WriteLine($"Subtitle track {subtitleTrack.Index} has no delivery URL.");
                continue;
            }

            if (!subtitleTrack.IsExternalUrl.GetValueOrDefault())
            {
                subtitleUrl = _sdkClientSettings.ServerUrl + subtitleUrl;
            }

            if (Uri.TryCreate(subtitleUrl, UriKind.Absolute, out Uri? subtitleUri))
            {
                TimedTextSource timedTextSource = TimedTextSource.CreateFromUri(subtitleUri);
                mediaSource.ExternalTimedTextSources.Add(timedTextSource);
                Debug.WriteLine($"Added external subtitle (index {subtitleTrack.Index}): {subtitleUri}");
            }
            else
            {
                Debug.WriteLine($"Failed to parse subtitle URL: {subtitleUrl}");
            }
        }
    }

    private Uri? BuildMediaUri(MediaSourceInfo mediaSourceInfo)
    {
        if (mediaSourceInfo.SupportsDirectPlay.GetValueOrDefault() || mediaSourceInfo.SupportsDirectStream.GetValueOrDefault())
        {
            RequestInformation request = _jellyfinApiClient.Videos[_currentItem!.Id!.Value].StreamWithContainer(mediaSourceInfo.Container).ToGetRequestInformation(
                parameters =>
                {
                    parameters.QueryParameters.Static = true;
                    parameters.QueryParameters.MediaSourceId = mediaSourceInfo.Id;
                    parameters.QueryParameters.DeviceId = new EasClientDeviceInformation().Id.ToString();

                    if (mediaSourceInfo.ETag is not null)
                    {
                        parameters.QueryParameters.Tag = mediaSourceInfo.ETag;
                    }

                    if (mediaSourceInfo.LiveStreamId is not null)
                    {
                        parameters.QueryParameters.LiveStreamId = mediaSourceInfo.LiveStreamId;
                    }
                });
            Uri mediaUri = _jellyfinApiClient.BuildUri(request);
            return new Uri($"{mediaUri.AbsoluteUri}&api_key={_sdkClientSettings.AccessToken}");
        }
        else if (mediaSourceInfo.SupportsTranscoding.GetValueOrDefault() && !string.IsNullOrEmpty(mediaSourceInfo.TranscodingUrl))
        {
            if (Uri.TryCreate(_sdkClientSettings.ServerUrl + mediaSourceInfo.TranscodingUrl, UriKind.Absolute, out Uri? mediaUri))
            {
                return mediaUri;
            }
        }

        return null;
    }

    [RelayCommand]
    private void ChangeStretchMode(Stretch stretch)
    {
        _playerElement?.Stretch = stretch;
    }

    [RelayCommand]
    private void ChangePlaybackSpeed(double speed)
    {
        _playerElement?.MediaPlayer?.PlaybackSession?.PlaybackRate = speed;
        _transportControls?.PlaybackSpeed = speed;

        UpdateEndsAtText();
    }

    [RelayCommand]
    private async Task ShowPlaybackInfoAsync()
    {
        try
        {
            if (_currentMediaSource is null || _currentItem is null)
            {
                return;
            }

            // Build playback info text
            StringBuilder info = new();

            info.AppendLine($"Title: {_currentItem.Name}");
            info.AppendLine();

            // Media source info
            info.AppendLine("Media Source:");
            if (!string.IsNullOrEmpty(_currentMediaSource.Container))
            {
                info.AppendLine($"  Container: {_currentMediaSource.Container.ToUpperInvariant()}");
            }
            if (_currentMediaSource.Size.HasValue)
            {
                info.AppendLine($"  Size: {FormatFileSize(_currentMediaSource.Size.Value)}");
            }
            if (_currentMediaSource.Bitrate.HasValue)
            {
                info.AppendLine($"  Bitrate: {FormatBitrate(_currentMediaSource.Bitrate.Value)}");
            }
            info.AppendLine();

            // Video stream info
            MediaStream? videoStream = _currentMediaSource.MediaStreams?
                .FirstOrDefault(s => s.Type == MediaStream_Type.Video);
            if (videoStream != null)
            {
                info.AppendLine("Video:");
                string videoCodec = videoStream.Codec?.ToUpperInvariant() ?? "Unknown";
                if (!string.IsNullOrEmpty(videoStream.Profile))
                {
                    videoCodec += $" {videoStream.Profile}";
                }
                info.AppendLine($"  Codec: {videoCodec}");
                info.AppendLine($"  Resolution: {videoStream.Width}x{videoStream.Height}");
                if (videoStream.RealFrameRate.HasValue)
                {
                    info.AppendLine($"  Frame Rate: {videoStream.RealFrameRate:F2} fps");
                }
                if (videoStream.BitRate.HasValue)
                {
                    info.AppendLine($"  Bitrate: {FormatBitrate(videoStream.BitRate.Value)}");
                }
                if (videoStream.VideoRangeType.HasValue)
                {
                    string rangeDisplay = videoStream.VideoDoViTitle ?? videoStream.VideoRangeType.Value.ToString();
                    info.AppendLine($"  Range: {rangeDisplay}");
                }
                if (!string.IsNullOrEmpty(videoStream.PixelFormat))
                {
                    info.AppendLine($"  Pixel Format: {videoStream.PixelFormat}");
                }
                info.AppendLine();
            }

            // Audio stream info
            int audioIndex = _playbackProgressInfo?.AudioStreamIndex ?? _currentMediaSource.DefaultAudioStreamIndex ?? -1;
            MediaStream? audioStream = _currentMediaSource.MediaStreams?
                .FirstOrDefault(s => s.Type == MediaStream_Type.Audio && s.Index == audioIndex);
            if (audioStream != null)
            {
                info.AppendLine("Audio:");
                string audioCodec = audioStream.Codec?.ToUpperInvariant() ?? "Unknown";
                if (!string.IsNullOrEmpty(audioStream.Profile))
                {
                    audioCodec += $" {audioStream.Profile}";
                }
                info.AppendLine($"  Codec: {audioCodec}");
                if (audioStream.Channels.HasValue)
                {
                    info.AppendLine($"  Channels: {audioStream.Channels}");
                }
                if (audioStream.BitRate.HasValue)
                {
                    info.AppendLine($"  Bitrate: {FormatBitrate(audioStream.BitRate.Value)}");
                }
                if (audioStream.SampleRate.HasValue)
                {
                    info.AppendLine($"  Sample Rate: {audioStream.SampleRate} Hz");
                }
                if (audioStream.BitDepth.HasValue)
                {
                    info.AppendLine($"  Bit Depth: {audioStream.BitDepth}");
                }
                if (!string.IsNullOrEmpty(audioStream.Language))
                {
                    info.AppendLine($"  Language: {audioStream.Language}");
                }
                info.AppendLine();
            }

            // Subtitle stream info
            int subtitleIndex = _playbackProgressInfo?.SubtitleStreamIndex ?? _currentMediaSource.DefaultSubtitleStreamIndex ?? -1;
            if (subtitleIndex >= 0)
            {
                MediaStream? subtitleStream = _currentMediaSource.MediaStreams?
                    .FirstOrDefault(s => s.Type == MediaStream_Type.Subtitle && s.Index == subtitleIndex);
                if (subtitleStream != null)
                {
                    info.AppendLine("Subtitles:");
                    info.AppendLine($"  Format: {subtitleStream.Codec?.ToUpperInvariant()}");
                    if (!string.IsNullOrEmpty(subtitleStream.Language))
                    {
                        info.AppendLine($"  Language: {subtitleStream.Language}");
                    }
                    info.AppendLine($"  Delivery: {(subtitleStream.IsExternal == true ? "External" : "Embedded")}");
                    info.AppendLine();
                }
            }

            // Playback method
            info.AppendLine("Playback:");
            string playMethod;
            if (_currentMediaSource.SupportsDirectPlay.GetValueOrDefault())
            {
                playMethod = "Direct Play";
            }
            else if (_currentMediaSource.SupportsDirectStream.GetValueOrDefault())
            {
                playMethod = "Direct Stream";
            }
            else
            {
                playMethod = "Transcoding";
            }
            info.AppendLine($"  Method: {playMethod}");

            // Transcoding info
            if (playMethod == "Transcoding")
            {
                if (!string.IsNullOrEmpty(_currentMediaSource.TranscodingContainer))
                {
                    info.AppendLine($"  Container: {_currentMediaSource.TranscodingContainer.ToUpperInvariant()}");
                }
                if (!string.IsNullOrEmpty(_currentMediaSource.TranscodingUrl))
                {
                    // Parse transcode reasons from URL if available
                    if (_currentMediaSource.TranscodingUrl.Contains("TranscodeReasons=", StringComparison.Ordinal))
                    {
                        int start = _currentMediaSource.TranscodingUrl.IndexOf("TranscodeReasons=", StringComparison.Ordinal) + 17;
                        int end = _currentMediaSource.TranscodingUrl.IndexOf('&', start);
                        if (end == -1)
                        {
                            end = _currentMediaSource.TranscodingUrl.Length;
                        }

                        string reasons = Uri.UnescapeDataString(_currentMediaSource.TranscodingUrl[start..end]);
                        if (!string.IsNullOrEmpty(reasons))
                        {
                            info.AppendLine($"  Reasons: {reasons.Replace(",", ", ", StringComparison.Ordinal)}");
                        }
                    }
                }
            }

            // Player info
            if (_playerElement?.MediaPlayer != null)
            {
                info.AppendLine();
                info.AppendLine("Player:");
                info.AppendLine($"  State: {_playerElement.MediaPlayer.PlaybackSession.PlaybackState}");
                info.AppendLine($"  Playback Rate: {_playerElement.MediaPlayer.PlaybackSession.PlaybackRate}x");
                info.AppendLine($"  Volume: {_playerElement.MediaPlayer.Volume * 100:F0}%");
            }

            ContentDialog dialog = new()
            {
                Title = "Playback Info",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = info.ToString(),
                        FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                CloseButtonText = "Close",
                XamlRoot = _playerElement?.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing playback info: {ex}");
        }
    }

    private static string FormatBitrate(long bitrate)
    {
        if (bitrate >= 1_000_000)
        {
            return $"{bitrate / 1_000_000.0:F1} Mbps";
        }
        return $"{bitrate / 1000} kbps";
    }

    private static string FormatFileSize(long bytes)
    {
        const long GB = 1024L * 1024 * 1024;
        const long MB = 1024L * 1024;

        if (bytes >= GB)
        {
            return $"{bytes / (double)GB:F2} GB";
        }
        return $"{bytes / (double)MB:F1} MB";
    }

    private async void TimerTick()
    {
        try
        {
            UpdatePositionTicks();

            // Only report progress when playing.
            if (_playerElement is not null
                && _playerElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                await ReportProgressAsync();
            }
        }
        catch (Exception ex)
        {
            // Timer callbacks with async void can crash the app if exceptions propagate.
            // Log and suppress to prevent app termination.
            Debug.WriteLine($"Error in TimerTick: {ex}");
        }
    }

    private void UpdateEndsAtText()
    {
        if (_transportControls is null || _playerElement?.MediaPlayer?.PlaybackSession is null)
        {
            return;
        }

        MediaPlaybackSession session = _playerElement.MediaPlayer.PlaybackSession;
        TimeSpan duration = session.NaturalDuration;

        // Fall back to item metadata if MediaPlayer duration not yet available
        if (duration == TimeSpan.Zero && _currentItem?.RunTimeTicks.HasValue == true)
        {
            duration = TimeSpan.FromTicks(_currentItem.RunTimeTicks.Value);
        }

        if (duration == TimeSpan.Zero)
        {
            return; // No duration available from either source
        }

        TimeSpan remaining = duration - session.Position;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        // Adjust for playback rate (e.g., at 2x speed, remaining time is halved)
        double playbackRate = session.PlaybackRate;
        if (playbackRate > 0 && playbackRate != 1.0)
        {
            remaining = TimeSpan.FromTicks((long)(remaining.Ticks / playbackRate));
        }

        DateTime endTime = DateTime.Now + remaining;
        _transportControls.EndsAtText = $"Ends at {endTime:t}";
    }

    private async Task<int> DetectBitrateAsync()
    {
        const int BitrateTestSize = 1024 * 1024; // 1MB
        const int DownloadChunkSize = 64 * 1024; // 64KB
        const double SafetyRatio = 0.8; // Only allow 80% of the detected bitrate to avoid buffering.

        long startTime = Stopwatch.GetTimestamp();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        int totalBytesRead = 0;
        try
        {
            Stream? stream = await _jellyfinApiClient.Playback.BitrateTest.GetAsync(
                request =>
                {
                    request.QueryParameters.Size = BitrateTestSize;
                },
                cts.Token);
            byte[] buffer = new byte[DownloadChunkSize];
            while (true)
            {
                int bytesRead = await stream!.ReadAsync(buffer, cts.Token);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }
        }
        catch (OperationCanceledException)
        {
            // This is expected. We only want to test for a finite amount of time.
        }

        double responseTimeSeconds = ((double)(Stopwatch.GetTimestamp() - startTime)) / Stopwatch.Frequency;
        double bytesPerSecond = totalBytesRead / responseTimeSeconds;
        int bitrate = (int)Math.Round(bytesPerSecond * 8 * SafetyRatio);
        Debug.WriteLine($"Downloaded {totalBytesRead} bytes in {responseTimeSeconds:F2}s. Using max bitrate of {bitrate}");
        return bitrate;
    }

    private async Task ReportProgressAsync()
    {
        if (_playbackProgressInfo is null)
        {
            return;
        }

        await _jellyfinApiClient.Sessions.Playing.Progress.PostAsync(_playbackProgressInfo);
    }
}