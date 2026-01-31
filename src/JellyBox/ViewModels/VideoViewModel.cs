using CommunityToolkit.Mvvm.ComponentModel;
using JellyBox.Controls;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
}
