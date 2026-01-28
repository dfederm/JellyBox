using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Controls;
using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk.Generated.Models;
using Windows.ApplicationModel.Core;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
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

    private async void RestartPlaybackWithCurrentPosition()
    {
        if (_playerElement?.MediaPlayer is null || _currentMediaSource is null)
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
}
