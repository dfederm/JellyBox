using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Controls;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class VideoViewModel : ObservableObject
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly ILogger<VideoViewModel> _logger;
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly IRequestAdapter _requestAdapter;
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
    private int? _cachedMaxStreamingBitrate;

    public VideoViewModel(
        ILogger<VideoViewModel> logger,
        JellyfinApiClient jellyfinApiClient,
        IRequestAdapter requestAdapter,
        JellyfinImageResolver imageResolver,
        JellyfinSdkSettings sdkClientSettings,
        DeviceProfileManager deviceProfileManager)
    {
        _logger = logger;
        _jellyfinApiClient = jellyfinApiClient;
        _requestAdapter = requestAdapter;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting playback of \"{ItemName}\" ({ItemId}).")]
    private partial void LogPlaybackStarting(string? itemName, Guid itemId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped playback of \"{ItemName}\" at {PositionTicks} ticks.")]
    private partial void LogPlaybackStopped(string? itemName, long positionTicks);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No playable media source returned for item {ItemId}.")]
    private partial void LogNoMediaSource(Guid itemId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Playback error for \"{ItemName}\".")]
    private partial void LogPlaybackError(Exception exception, string? itemName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to present subtitle track.")]
    private partial void LogSubtitlePresentationError(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Subtitle UWP index out of range for Jellyfin index {JellyfinIndex} (UWP index {UwpIndex}, track count {TrackCount}).")]
    private partial void LogSubtitleIndexOutOfRange(int jellyfinIndex, int? uwpIndex, int trackCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seamless audio track switch to Jellyfin index {JellyfinIndex} (UWP index {UwpIndex}).")]
    private partial void LogSeamlessAudioSwitch(int jellyfinIndex, int? uwpIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Restarting playback for audio track {JellyfinIndex}.")]
    private partial void LogRestartForAudioTrack(int jellyfinIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seamless subtitle switch to Jellyfin index {JellyfinIndex} (UWP index {UwpIndex}).")]
    private partial void LogSeamlessSubtitleSwitch(int jellyfinIndex, int? uwpIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Restarting playback for subtitle track {JellyfinIndex}.")]
    private partial void LogRestartForSubtitleTrack(int jellyfinIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to report playback progress.")]
    private partial void LogProgressReportError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to show playback info dialog.")]
    private partial void LogPlaybackInfoDialogError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to restart playback.")]
    private partial void LogRestartPlaybackError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to toggle favorite state.")]
    private partial void LogToggleFavoriteError(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping unsupported external subtitle format {Codec} (index {Index}).")]
    private partial void LogSkippingUnsupportedSubtitle(string? codec, int? index);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Subtitle track {Index} has no delivery URL.")]
    private partial void LogSubtitleNoDeliveryUrl(int? index);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added third-party external subtitle (index {Index}): {SubtitleUrl}.")]
    private partial void LogAddedThirdPartySubtitle(int? index, string subtitleUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added Jellyfin-hosted external subtitle from stream (index {Index}).")]
    private partial void LogAddedJellyfinSubtitle(int? index);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download subtitle stream (index {Index}).")]
    private partial void LogSubtitleDownloadFailed(Exception exception, int? index);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Measured max streaming bitrate {Bitrate} bps ({TotalBytes} bytes downloaded in {ResponseTimeSeconds}s).")]
    private partial void LogMeasuredBitrate(long totalBytes, double responseTimeSeconds, int bitrate);
}
