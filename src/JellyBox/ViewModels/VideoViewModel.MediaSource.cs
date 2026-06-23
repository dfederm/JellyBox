using System.Diagnostics;
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
    /// Fetches playback info from the Jellyfin API.
    /// </summary>
    private async Task<(MediaSourceInfo? MediaSource, string? PlaySessionId)> GetPlaybackInfoAsync(
        Guid itemId,
        string? mediaSourceId,
        int? audioStreamIndex,
        int? subtitleStreamIndex,
        long? startTimeTicks)
    {
        await _deviceProfileManager.EnsureInitializedAsync();
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

        // Only load the selected external subtitle to avoid extra network I/O and parsing.
        // Switching to a different external track restarts playback (see PopulateTrackLists).
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
            .FirstOrDefault(s => s.Type == MediaStream_Type.Subtitle && s.Index == selectedSubtitleIndex);

        if (subtitleTrack is null
            || GetSubtitleDeliveryMethod(subtitleTrack) != MediaStream_DeliveryMethod.External)
        {
            return;
        }

        if (!IsSubtitleFormatSupported(subtitleTrack))
        {
            Debug.WriteLine($"Skipping unsupported external subtitle format: {subtitleTrack.Codec} (index {subtitleTrack.Index})");
            return;
        }

        string language = subtitleTrack.Language ?? string.Empty;

        if (subtitleTrack.IsExternalUrl.GetValueOrDefault())
        {
            string? subtitleUrl = subtitleTrack.DeliveryUrl;
            if (string.IsNullOrEmpty(subtitleUrl))
            {
                Debug.WriteLine($"Subtitle track {subtitleTrack.Index} has no delivery URL.");
                return;
            }

            TimedTextSource timedTextSource = TimedTextSource.CreateFromUri(new Uri(subtitleUrl), language);
            mediaSource.ExternalTimedTextSources.Add(timedTextSource);
            Debug.WriteLine($"Added third-party external subtitle (index {subtitleTrack.Index}): {subtitleUrl}");
            return;
        }

        try
        {
            RequestInformation request = _jellyfinApiClient
                .Videos[_currentItem!.Id!.Value]
                [mediaSourceInfo.Id!]
                .Subtitles[subtitleTrack.Index!.Value]
                .StreamWithRouteFormat(GetSubtitleRouteFormat(subtitleTrack))
                .ToGetRequestInformation();

            using Stream responseStream = await _requestAdapter.SendPrimitiveAsync<Stream>(request)
                ?? throw new InvalidOperationException("Subtitle stream response was null.");

            using MemoryStream memoryStream = new();
            await responseStream.CopyToAsync(memoryStream);
            byte[] bytes = memoryStream.ToArray();
#pragma warning disable CA2000 // MediaSource owns the stream for the lifetime of playback.
            InMemoryRandomAccessStream stream = new();
#pragma warning restore CA2000
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            TimedTextSource timedTextSource = TimedTextSource.CreateFromStream(stream, language);
            mediaSource.ExternalTimedTextSources.Add(timedTextSource);
            Debug.WriteLine($"Added Jellyfin-hosted external subtitle from stream (index {subtitleTrack.Index})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to download subtitle stream (index {subtitleTrack.Index}): {ex.Message}");
        }
    }

    private static string GetSubtitleRouteFormat(MediaStream stream)
    {
        string codec = stream.Codec ?? string.Empty;
        if (codec.Equals("subrip", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("srt", StringComparison.OrdinalIgnoreCase))
        {
            return "srt";
        }

        if (codec.Equals("vtt", StringComparison.OrdinalIgnoreCase)
            || codec.Equals("webvtt", StringComparison.OrdinalIgnoreCase))
        {
            return "vtt";
        }

        return "srt";
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