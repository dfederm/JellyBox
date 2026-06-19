using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage.Streams;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
    /// <summary>
    /// Fetches playback info, preferring direct play when subtitles can be rendered client-side.
    /// </summary>
    private async Task<(MediaSourceInfo? MediaSource, string? PlaySessionId)> GetPlaybackInfoForPlaybackAsync(
        Guid itemId,
        string? mediaSourceId,
        int? audioStreamIndex,
        int? subtitleStreamIndex,
        long? startTimeTicks)
    {
        if (subtitleStreamIndex is >= 0)
        {
            (MediaSourceInfo? mediaSource, string? playSessionId) = await GetPlaybackInfoAsync(
                itemId,
                mediaSourceId,
                audioStreamIndex,
                subtitleStreamIndex: null,
                startTimeTicks);

            if (mediaSource is not null
                && CanRenderSubtitleClientSide(mediaSource, subtitleStreamIndex.Value)
                && (mediaSource.SupportsDirectPlay.GetValueOrDefault() || mediaSource.SupportsDirectStream.GetValueOrDefault()))
            {
                Debug.WriteLine(
                    $"Using client-side subtitles (Jellyfin index {subtitleStreamIndex.Value}); omitting subtitle index from playback request to avoid remux/transcode.");
                return (mediaSource, playSessionId);
            }
        }

        return await GetPlaybackInfoAsync(
            itemId,
            mediaSourceId,
            audioStreamIndex,
            subtitleStreamIndex,
            startTimeTicks);
    }

    /// <summary>
    /// Fetches playback info from the Jellyfin API.
    /// </summary>
    private async Task<(MediaSourceInfo? MediaSource, string? PlaySessionId)> GetPlaybackInfoAsync(
        Guid itemId,
        string? mediaSourceId,
        int? audioStreamIndex,
        int? subtitleStreamIndex,
        long? startTimeTicks)
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
            StartTimeTicks = startTimeTicks,
        };

        PlaybackInfoResponse? response = await _jellyfinApiClient.Items[itemId].PlaybackInfo.PostAsync(playbackInfo);

        if (response?.MediaSources is null || response.MediaSources.Count == 0)
        {
            return (null, null);
        }

        return (response.MediaSources[0], response.PlaySessionId);
    }

    private static bool CanRenderSubtitleClientSide(MediaSourceInfo mediaSource, int subtitleStreamIndex)
    {
        MediaStream? stream = mediaSource.MediaStreams?
            .FirstOrDefault(s => s.Type == MediaStream_Type.Subtitle && s.Index == subtitleStreamIndex);

        return stream is not null && IsSubtitleFormatSupported(stream);
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

        // Only load the selected external subtitle. Loading every track upfront causes extra network I/O and parsing.
        await AddSelectedExternalSubtitleAsync(mediaSource, mediaSourceInfo);

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
    /// Adds the selected external subtitle in a supported format.
    /// UWP TimedTextSource only supports SRT (subrip) and VTT formats.
    /// </summary>
    private async Task AddSelectedExternalSubtitleAsync(MediaSource mediaSource, MediaSourceInfo mediaSourceInfo)
    {
        if (mediaSourceInfo.MediaStreams is null)
        {
            return;
        }

        int? selectedSubtitleIndex = _playbackProgressInfo?.SubtitleStreamIndex;
        if (selectedSubtitleIndex is not >= 0)
        {
            return;
        }

        MediaStream? subtitleTrack = mediaSourceInfo.MediaStreams
            .FirstOrDefault(s => s.Type == MediaStream_Type.Subtitle
                && s.IsExternal.GetValueOrDefault()
                && s.Index == selectedSubtitleIndex);

        if (subtitleTrack is null)
        {
            return;
        }

        if (!IsSubtitleFormatSupported(subtitleTrack))
        {
            Debug.WriteLine($"Skipping unsupported external subtitle format: {subtitleTrack.Codec} (index {subtitleTrack.Index})");
            return;
        }

        string? subtitleUrl = subtitleTrack.DeliveryUrl;
        if (string.IsNullOrEmpty(subtitleUrl))
        {
            Debug.WriteLine($"Subtitle track {subtitleTrack.Index} has no delivery URL.");
            return;
        }

        Uri subtitleUri = BuildAuthenticatedSubtitleUri(subtitleUrl, subtitleTrack.IsExternalUrl.GetValueOrDefault());

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("Jellyfin");
            using HttpResponseMessage response = await client.GetAsync(subtitleUri);
            response.EnsureSuccessStatusCode();

            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
#pragma warning disable CA2000 // MediaSource owns the stream for the lifetime of playback.
            InMemoryRandomAccessStream stream = new();
#pragma warning restore CA2000
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            TimedTextSource timedTextSource = TimedTextSource.CreateFromStream(stream, subtitleTrack.Language ?? string.Empty);
            mediaSource.ExternalTimedTextSources.Add(timedTextSource);
            Debug.WriteLine($"Added external subtitle from stream (index {subtitleTrack.Index}): {subtitleUri}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to download subtitle stream, falling back to URI: {ex.Message}");
            TimedTextSource timedTextSource = TimedTextSource.CreateFromUri(subtitleUri);
            mediaSource.ExternalTimedTextSources.Add(timedTextSource);
        }
    }

    private Uri BuildAuthenticatedSubtitleUri(string subtitleUrl, bool isExternalUrl)
    {
        string url = isExternalUrl ? subtitleUrl : _sdkClientSettings.ServerUrl + subtitleUrl;
        Uri uri = new(url);

        if (string.IsNullOrEmpty(_sdkClientSettings.AccessToken)
            || uri.Query.Contains("api_key=", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        string separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri.AbsoluteUri}{separator}api_key={_sdkClientSettings.AccessToken}");
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
        if (_cachedMaxStreamingBitrate.HasValue)
        {
            return _cachedMaxStreamingBitrate.Value;
        }

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
        _cachedMaxStreamingBitrate = bitrate;
        return bitrate;
    }
}