using System.Diagnostics;
using Jellyfin.Sdk.Generated.Models;
using Windows.Media.Playback;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
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

    private async Task ReportProgressAsync()
    {
        if (_playbackProgressInfo is null)
        {
            return;
        }

        await _jellyfinApiClient.Sessions.Playing.Progress.PostAsync(_playbackProgressInfo);
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
}
