using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using FinBox.Services;
using FinBox.Views;
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

namespace FinBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class VideoViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly DeviceProfileManager _deviceProfileManager;
    private readonly DispatcherTimer _progressTimer;
    private MediaPlayerElement? _playerElement;
    private PlaybackProgressInfo? _playbackProgressInfo;

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

    public async Task PlayVideoAsync(Video.Parameters parameters, MediaPlayerElement playerElement)
    {
        var item = parameters.Item;
        _playerElement = playerElement;

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

        _playbackProgressInfo = new PlaybackProgressInfo
        {
            ItemId = item.Id.Value,
            MediaSourceId = mediaSourceInfo.Id,
            PlaySessionId = playbackInfoResponse.PlaySessionId,
            AudioStreamIndex = playbackInfo.AudioStreamIndex,
            SubtitleStreamIndex = playbackInfo.SubtitleStreamIndex,
        };

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

        if (mediaSourceInfo.DefaultSubtitleStreamIndex.HasValue)
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

            if (session.PlaybackState == MediaPlaybackState.Playing)
            {
                _playbackProgressInfo.IsPaused = false;
            }
            else if (session.PlaybackState == MediaPlaybackState.Paused)
            {
                _playbackProgressInfo.IsPaused = true;
            }

            // TODO: Only update if something actually changed?
            await ReportProgressAsync();
        };

        _playerElement.MediaPlayer.Play();

        await ReportStartedAsync();

        _progressTimer.Start();
    }

    public async Task StopVideoAsync()
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

        await ReportStoppedAsync();
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

    private async void TimerTick()
    {
        UpdatePositionTicks();

        // Only report progress when playing.
        if (_playerElement is not null
            && _playerElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            await ReportProgressAsync();
        }
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