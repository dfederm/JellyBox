using Microsoft.Extensions.Logging;
using Windows.Media.Playback;
using Windows.System.Display;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
    private DisplayRequest? _displayRequest;
    private bool _isDisplayRequestActive;

    private void UpdateDisplayRequest()
    {
        try
        {
            SetDisplayRequestActive(_playerElement?.MediaPlayer?.PlaybackSession.PlaybackState is
                MediaPlaybackState.Opening or MediaPlaybackState.Buffering or MediaPlaybackState.Playing);
        }
        catch (Exception ex)
        {
            LogPlaybackError(ex, _currentItem?.Name);
            SetDisplayRequestActive(false);
        }
    }

    private void SetDisplayRequestActive(bool isActive)
    {
        if (isActive == _isDisplayRequestActive)
        {
            return;
        }

        try
        {
            if (isActive)
            {
                _displayRequest ??= new DisplayRequest();
                _displayRequest.RequestActive();
            }
            else
            {
                _displayRequest!.RequestRelease();
            }

            _isDisplayRequestActive = isActive;
            LogDisplayRequestChanged(isActive);
        }
        catch (Exception ex)
        {
            LogDisplayRequestError(ex, isActive);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Playback display request active: {IsActive}.")]
    private partial void LogDisplayRequestChanged(bool isActive);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to change playback display request active state to {IsActive}.")]
    private partial void LogDisplayRequestError(Exception exception, bool isActive);

    [LoggerMessage(Level = LogLevel.Error, Message = "Media playback failed with {Error}: {ErrorMessage}.")]
    private partial void LogMediaPlaybackFailed(Exception exception, MediaPlayerError error, string errorMessage);
}