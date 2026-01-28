using CommunityToolkit.Mvvm.Input;
using Windows.Media.Playback;
using Windows.UI.Xaml.Media;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
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

        if (_playerElement.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
        {
            _playerElement.MediaPlayer.Pause();
        }
        else
        {
            _playerElement.MediaPlayer.Play();
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

    [RelayCommand]
    private void ChangeStretchMode(Stretch stretch)
    {
        _playerElement?.Stretch = stretch;
        _transportControls?.StretchMode = stretch;
    }

    [RelayCommand]
    private void ChangePlaybackSpeed(double speed)
    {
        _playerElement?.MediaPlayer?.PlaybackSession?.PlaybackRate = speed;
        _transportControls?.PlaybackSpeed = speed;

        UpdateEndsAtText();
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
}
