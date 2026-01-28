using System.Diagnostics;
using JellyBox.Services;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
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
}
