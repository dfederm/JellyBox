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
            _cachedMaxStreamingBitrate = null;
            BaseItemDto item = _currentItem;
            SetDisplayRequestActive(false);
            _playerElement = playerElement;

            LogPlaybackStarting(item.Name, item.Id ?? Guid.Empty);

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

            // Determine start position from parameters
            TimeSpan startPosition = TimeSpan.FromTicks(parameters.StartPositionTicks);

            // Calculate initial "Ends at" from metadata, accounting for resume position
            if (item.RunTimeTicks.HasValue)
            {
                TimeSpan remaining = TimeSpan.FromTicks(item.RunTimeTicks.Value) - startPosition;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

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
                PositionTicks = startPosition.Ticks,
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
                    () =>
                    {
                        if (mp == _playerElement?.MediaPlayer)
                        {
                            UpdateDisplayRequest();
                            UpdatePositionTicks(mp);
                        }
                    });

                await ReportStoppedAsync();
            };

            _playerElement.MediaPlayer.PlaybackSession.PlaybackStateChanged += async (session, obj) =>
            {
                try
                {
                    bool reportProgress = false;
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () =>
                        {
                            if (session != _playerElement?.MediaPlayer?.PlaybackSession)
                            {
                                return;
                            }

                            UpdateDisplayRequest();
                            MediaPlaybackState state = session.PlaybackState;
                            if (state == MediaPlaybackState.None)
                            {
                                return;
                            }

                            if (state == MediaPlaybackState.Playing)
                            {
                                ShowBackdropImage = false;
                                _playbackProgressInfo.IsPaused = false;
                                _transportControls.IsPlaying = true;
                                UpdateEndsAtText();
                            }
                            else if (state == MediaPlaybackState.Paused)
                            {
                                _playbackProgressInfo.IsPaused = true;
                                _transportControls.IsPlaying = false;
                            }

                            _transportControls.IsBuffering = state == MediaPlaybackState.Buffering;
                            _playbackProgressInfo.CanSeek = session.CanSeek;
                            _playbackProgressInfo.PositionTicks = session.Position.Ticks;
                            reportProgress = true;
                        });

                    if (reportProgress)
                    {
                        await ReportProgressAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogProgressReportError(ex);
                }
            };

            _playerElement.MediaPlayer.MediaFailed += OnMediaFailed;

            MediaPlayer? player = await StartPlaybackAsync(parameters.MediaSourceId, startPosition);
            if (player is null || player != _playerElement?.MediaPlayer)
            {
                return;
            }

            await ReportStartedAsync();

            if (player == _playerElement?.MediaPlayer)
            {
                _progressTimer.Start();
            }
        }
        catch (Exception ex)
        {
            LogPlaybackError(ex, _currentItem?.Name);
        }
    }

    public async void StopVideo()
    {
        MediaPlayerElement? playerElement = _playerElement;
        _playerElement = null;
        SetDisplayRequestActive(false);

        try
        {
            _progressTimer.Stop();

            MediaPlayer? player = playerElement?.MediaPlayer;
            UpdatePositionTicks(player);

            LogPlaybackStopped(_currentItem?.Name, _playbackProgressInfo?.PositionTicks ?? 0);

            if (player is not null)
            {
                player.MediaFailed -= OnMediaFailed;
                player.Pause();

                MediaPlaybackItem mediaPlaybackItem = (MediaPlaybackItem)player.Source;

                // Detach components from each other
                playerElement!.SetMediaPlayer(null);
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
            LogPlaybackError(ex, _currentItem?.Name);
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
            LogRestartPlaybackError(ex);
        }
    }

    /// <summary>
    /// Starts or restarts playback with the specified media source and position.
    /// </summary>
    /// <param name="mediaSourceId">The media source ID to play.</param>
    /// <param name="startPosition">The position to start playback from.</param>
    private async Task<MediaPlayer?> StartPlaybackAsync(string? mediaSourceId, TimeSpan startPosition)
    {
        MediaPlayer? player = _playerElement?.MediaPlayer;
        if (_currentItem is null || _playbackProgressInfo is null || player is null)
        {
            return null;
        }

        // Get playback info with current track selections
        (MediaSourceInfo? mediaSourceInfo, string? playSessionId) = await GetPlaybackInfoAsync(
            _currentItem.Id!.Value,
            mediaSourceId,
            _playbackProgressInfo.AudioStreamIndex,
            _playbackProgressInfo.SubtitleStreamIndex,
            startPosition.Ticks);

        if (mediaSourceInfo is null)
        {
            LogNoMediaSource(_currentItem.Id ?? Guid.Empty);
            return null;
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
            return null;
        }

        // Set display mode based on video stream properties (may change on restart if transcoding)
        MediaStream videoStream = mediaSourceInfo.MediaStreams!.First(stream => stream.Type == MediaStream_Type.Video);
        await DisplayModeManager.SetBestDisplayModeAsync(
            (uint)videoStream.Width!.Value,
            (uint)videoStream.Height!.Value,
            (double)videoStream.RealFrameRate!.Value,
            videoStream.VideoRangeType!.Value);

        // Start playback
        if (player != _playerElement?.MediaPlayer)
        {
            return null;
        }

        _playerElement!.Source = playbackItem;
        player.Play();
        return player;
    }

    private async void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        try
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    if (sender == _playerElement?.MediaPlayer)
                    {
                        UpdateDisplayRequest();
                        LogMediaPlaybackFailed(args.ExtendedErrorCode, args.Error, args.ErrorMessage);
                    }
                });
        }
        catch (Exception ex)
        {
            LogPlaybackError(ex, _currentItem?.Name);
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
            LogToggleFavoriteError(ex);
        }
    }
}
