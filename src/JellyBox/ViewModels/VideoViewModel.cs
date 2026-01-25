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
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly DeviceProfileManager _deviceProfileManager;
    private readonly DispatcherTimer _progressTimer;
    private MediaPlayerElement? _playerElement;
    private CustomMediaTransportControls? _transportControls;
    private PlaybackProgressInfo? _playbackProgressInfo;
    private BaseItemDto? _currentItem;
    private MediaSourceInfo? _currentMediaSource;
    private double _volumeBeforeMute = 1.0;

    public VideoViewModel(
        JellyfinApiClient jellyfinApiClient,
        JellyfinSdkSettings sdkClientSettings,
        DeviceProfileManager deviceProfileManager)
    {
        _jellyfinApiClient = jellyfinApiClient;
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

            DeviceProfile deviceProfile = _deviceProfileManager.Profile;

            BackdropImageUri = _jellyfinApiClient.GetItemBackdropImageUrl(item, 1920);
            ShowBackdropImage = true;

            // Note: This mutates the shared device profile. That's probably OK as long as all accesses do this.
            // TODO: Look into making a copy instead.
            deviceProfile.MaxStreamingBitrate = await DetectBitrateAsync();

            PlaybackInfoDto playbackInfo = new()
            {
                DeviceProfile = deviceProfile,
                MediaSourceId = parameters.MediaSourceId,
                AudioStreamIndex = parameters.AudioStreamIndex,
                SubtitleStreamIndex = parameters.SubtitleStreamIndex,
            };

            // TODO: Does this create a play session? If so, update progress properly.
            PlaybackInfoResponse? playbackInfoResponse = await _jellyfinApiClient.Items[item.Id!.Value].PlaybackInfo.PostAsync(playbackInfo);

            // TODO: Always the first? What if 0 or > 1?
            MediaSourceInfo mediaSourceInfo = playbackInfoResponse!.MediaSources![0];
            _currentMediaSource = mediaSourceInfo;

            _playbackProgressInfo = new PlaybackProgressInfo
            {
                ItemId = item.Id.Value,
                MediaSourceId = mediaSourceInfo.Id,
                PlaySessionId = playbackInfoResponse.PlaySessionId,
                AudioStreamIndex = playbackInfo.AudioStreamIndex,
                SubtitleStreamIndex = playbackInfo.SubtitleStreamIndex,
            };

            // Populate audio and subtitle track lists
            PopulateTrackLists(mediaSourceInfo, playbackInfo.AudioStreamIndex, playbackInfo.SubtitleStreamIndex);

            bool isAdaptive;
            Uri? mediaUri;

            if (mediaSourceInfo.SupportsDirectPlay.GetValueOrDefault() || mediaSourceInfo.SupportsDirectStream.GetValueOrDefault())
            {
                RequestInformation request = _jellyfinApiClient.Videos[item.Id.Value].StreamWithContainer(mediaSourceInfo.Container).ToGetRequestInformation(
                    parameters =>
                    {
                        parameters.QueryParameters.Static = true;
                        parameters.QueryParameters.MediaSourceId = mediaSourceInfo.Id;

                        // TODO Copied from AppServices. Get this in a better way, shared by the Jellyfin SDK settings initialization.
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
                mediaUri = _jellyfinApiClient.BuildUri(request);

                // TODO: The Jellyfin SDK doesn't appear to provide a way to add this query param.
                mediaUri = new Uri($"{mediaUri.AbsoluteUri}&api_key={_sdkClientSettings.AccessToken}");
                isAdaptive = false;
            }
            else if (mediaSourceInfo.SupportsTranscoding.GetValueOrDefault())
            {
                if (!Uri.TryCreate(_sdkClientSettings.ServerUrl + mediaSourceInfo.TranscodingUrl, UriKind.Absolute, out mediaUri))
                {
                    // TODO: Error handling
                    return;
                }

                isAdaptive = mediaSourceInfo.TranscodingSubProtocol == MediaSourceInfo_TranscodingSubProtocol.Hls;
            }
            else
            {
                // TODO: Default handling
                return;
            }

#pragma warning disable CA2000 // Dispose objects before losing scope. The media source is disposed in StopVideoAsync.
            MediaSource mediaSource;
            if (isAdaptive)
            {
                AdaptiveMediaSourceCreationResult result = await AdaptiveMediaSource.CreateFromUriAsync(mediaUri);
                if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
                {
                    AdaptiveMediaSource ams = result.MediaSource;
                    ams.InitialBitrate = ams.AvailableBitrates.Max();

                    mediaSource = MediaSource.CreateFromAdaptiveMediaSource(ams);
                }
                else
                {
                    // Fall back to creating from the Uri directly
                    mediaSource = MediaSource.CreateFromUri(mediaUri);
                }
            }
            else
            {
                mediaSource = MediaSource.CreateFromUri(mediaUri);
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

            if (mediaSourceInfo.DefaultSubtitleStreamIndex.HasValue
                && mediaSourceInfo.DefaultSubtitleStreamIndex.Value != -1)
            {
                MediaStream subtitleTrack = mediaSourceInfo.MediaStreams![mediaSourceInfo.DefaultSubtitleStreamIndex.Value];
                if (subtitleTrack.IsExternal.GetValueOrDefault())
                {
                    // TODO: Check the subtitle format (Codec property), as some mayneed to be handled differently.
                    string? subtitleUrl = subtitleTrack.DeliveryUrl;
                    if (!subtitleTrack.IsExternalUrl.GetValueOrDefault())
                    {
                        subtitleUrl = _sdkClientSettings.ServerUrl + subtitleUrl;
                    }

                    if (Uri.TryCreate(subtitleUrl, UriKind.Absolute, out Uri? subtitleUri))
                    {
                        TimedTextSource timedTextSource = TimedTextSource.CreateFromUri(subtitleUri);
                        mediaSource.ExternalTimedTextSources.Add(timedTextSource);
                    }
                    else
                    {
                        // TODO: Error handling
                    }
                }
            }

            MediaPlaybackItem playbackItem = new(mediaSource);

            // Present the first track, which is the subtitles
            playbackItem.TimedMetadataTracksChanged += (sender, args) =>
            {
                playbackItem.TimedMetadataTracks.SetPresentationMode(0, TimedMetadataTrackPresentationMode.PlatformPresented);
            };

#pragma warning disable CA2000 // Dispose objects before losing scope. Disposed in StopVideoAsync.
            _playerElement.SetMediaPlayer(new MediaPlayer());
#pragma warning restore CA2000 // Dispose objects before losing scope
            _playerElement.MediaPlayer.Source = playbackItem;

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

            MediaStream videoStream = mediaSourceInfo.MediaStreams!.First(stream => stream.Type == MediaStream_Type.Video);
            await DisplayModeManager.SetBestDisplayModeAsync(
                (uint)videoStream.Width!.Value,
                (uint)videoStream.Height!.Value,
                (double)videoStream.RealFrameRate!.Value,
                videoStream.VideoRangeType!.Value);

            _playerElement.MediaPlayer.Play();

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

        // Audio tracks
        List<TrackInfo> audioTracks = [];
        int defaultAudioIndex = selectedAudioIndex ?? mediaSourceInfo.DefaultAudioStreamIndex ?? -1;
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams.Where(s => s.Type == MediaStream_Type.Audio))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            audioTracks.Add(new TrackInfo(stream.Index!.Value, displayName));
        }
        _transportControls.AudioTracks = audioTracks;
        _transportControls.SelectedAudioIndex = defaultAudioIndex;

        // Subtitle tracks
        List<TrackInfo> subtitleTracks = [];
        int defaultSubtitleIndex = selectedSubtitleIndex ?? mediaSourceInfo.DefaultSubtitleStreamIndex ?? -1;
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams.Where(s => s.Type == MediaStream_Type.Subtitle))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            subtitleTracks.Add(new TrackInfo(stream.Index!.Value, displayName));
        }
        _transportControls.SubtitleTracks = subtitleTracks;
        _transportControls.SelectedSubtitleIndex = defaultSubtitleIndex;
    }

    [RelayCommand]
    private void SelectAudioTrack(int trackIndex)
    {
        _playbackProgressInfo?.AudioStreamIndex = trackIndex;
        _transportControls?.SelectedAudioIndex = trackIndex;

        // Restart playback with the new audio track
        RestartPlaybackWithCurrentPosition();
    }

    [RelayCommand]
    private void SelectSubtitleTrack(int trackIndex)
    {
        // -1 means "off", otherwise use the track index
        // Note: We store -1 directly rather than null so the API explicitly disables subtitles
        _playbackProgressInfo?.SubtitleStreamIndex = trackIndex;
        _transportControls?.SelectedSubtitleIndex = trackIndex;

        // Restart playback with the new subtitle track
        RestartPlaybackWithCurrentPosition();
    }

    private async void RestartPlaybackWithCurrentPosition()
    {
        if (_playerElement?.MediaPlayer is null || _currentItem is null || _currentMediaSource is null)
        {
            return;
        }

        try
        {
            // Save current position
            TimeSpan currentPosition = _playerElement.MediaPlayer.PlaybackSession.Position;

            // Get new playback info with updated track selections
            DeviceProfile deviceProfile = _deviceProfileManager.Profile;
            deviceProfile.MaxStreamingBitrate = await DetectBitrateAsync();

            PlaybackInfoDto playbackInfo = new()
            {
                DeviceProfile = deviceProfile,
                MediaSourceId = _currentMediaSource.Id,
                AudioStreamIndex = _playbackProgressInfo?.AudioStreamIndex,
                SubtitleStreamIndex = _playbackProgressInfo?.SubtitleStreamIndex,
            };

            PlaybackInfoResponse? playbackInfoResponse = await _jellyfinApiClient.Items[_currentItem.Id!.Value].PlaybackInfo.PostAsync(playbackInfo);
            if (playbackInfoResponse?.MediaSources is null || playbackInfoResponse.MediaSources.Count == 0)
            {
                return;
            }

            MediaSourceInfo mediaSourceInfo = playbackInfoResponse.MediaSources[0];
            _currentMediaSource = mediaSourceInfo;

            // Update play session
            _playbackProgressInfo?.PlaySessionId = playbackInfoResponse.PlaySessionId;

            // Build new URI
            Uri? mediaUri = BuildMediaUri(mediaSourceInfo);
            if (mediaUri is null)
            {
                return;
            }

            bool isAdaptive = !mediaSourceInfo.SupportsDirectPlay.GetValueOrDefault()
                           && !mediaSourceInfo.SupportsDirectStream.GetValueOrDefault()
                           && mediaSourceInfo.TranscodingSubProtocol == MediaSourceInfo_TranscodingSubProtocol.Hls;

            // Create new media source
#pragma warning disable CA2000 // Dispose objects before losing scope. The media source is assigned to the player and disposed on track change or stop.
            MediaSource mediaSource;
            if (isAdaptive)
            {
                AdaptiveMediaSourceCreationResult result = await AdaptiveMediaSource.CreateFromUriAsync(mediaUri);
                if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
                {
                    AdaptiveMediaSource ams = result.MediaSource;
                    ams.InitialBitrate = ams.AvailableBitrates.Max();
                    mediaSource = MediaSource.CreateFromAdaptiveMediaSource(ams);
                }
                else
                {
                    mediaSource = MediaSource.CreateFromUri(mediaUri);
                }
            }
            else
            {
                mediaSource = MediaSource.CreateFromUri(mediaUri);
            }
#pragma warning restore CA2000 // Dispose objects before losing scope

            // Dispose old media source
            if (_playerElement.Source is MediaPlaybackItem oldItem)
            {
                oldItem.Source?.Dispose();
            }
            else if (_playerElement.Source is MediaSource oldSource)
            {
                oldSource.Dispose();
            }

            // Start playback at the saved position
            MediaPlaybackItem playbackItem = new(mediaSource);
            _playerElement.Source = playbackItem;
            _playerElement.MediaPlayer.Play();

            // Seek to saved position after a short delay to let playback start
            await Task.Delay(500);
            if (_playerElement.MediaPlayer.PlaybackSession.CanSeek)
            {
                _playerElement.MediaPlayer.PlaybackSession.Position = currentPosition;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restarting playback: {ex}");
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