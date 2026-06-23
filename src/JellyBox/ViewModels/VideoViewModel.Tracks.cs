using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Controls;
using JellyBox.Services;
using Jellyfin.Sdk.Generated.Models;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
    private void PopulateTrackLists(MediaSourceInfo mediaSourceInfo, int? selectedAudioIndex, int? selectedSubtitleIndex)
    {
        if (mediaSourceInfo.MediaStreams is null || _transportControls is null)
        {
            return;
        }

        // Audio tracks
        List<TrackInfo> audioTracks = [];
        int defaultAudioIndex = selectedAudioIndex ?? mediaSourceInfo.DefaultAudioStreamIndex ?? -1;
        int uwpAudioIndex = 0; // UWP index matches iteration order
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams.Where(s => s.Type == MediaStream_Type.Audio))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            audioTracks.Add(new TrackInfo(
                stream.Index!.Value,
                uwpAudioIndex,
                displayName));
            uwpAudioIndex++;
        }
        _transportControls.AudioTracks = audioTracks;
        _transportControls.SelectedAudioIndex = defaultAudioIndex;

        // Subtitle tracks
        // UWP's TimedMetadataTracks collection orders embedded tracks first, then external tracks
        // in the order they were added to ExternalTimedTextSources.
        // See: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/media-playback-with-mediasource
        // UwpTrackIndex follows DeliveryMethod: Embed and the selected External track are presented
        // client-side; other External tracks restart playback to load; Encode/Hls/Drop rely on the server.
        List<TrackInfo> subtitleTracks = [];
        int defaultSubtitleIndex = selectedSubtitleIndex ?? mediaSourceInfo.DefaultSubtitleStreamIndex ?? -1;
        int uwpSubtitleIndex = 0;

        // First pass: embedded subtitles
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && !s.IsExternal.GetValueOrDefault()))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            bool hasUwpTrack = HasClientSideUwpSubtitleTrack(stream, defaultSubtitleIndex);

            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                hasUwpTrack ? uwpSubtitleIndex : null,
                displayName));

            if (hasUwpTrack)
            {
                uwpSubtitleIndex++;
            }
        }

        // Second pass: external subtitles
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && s.IsExternal.GetValueOrDefault()))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            bool hasUwpTrack = HasClientSideUwpSubtitleTrack(stream, defaultSubtitleIndex);

            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                hasUwpTrack ? uwpSubtitleIndex : null,
                displayName));

            if (hasUwpTrack)
            {
                uwpSubtitleIndex++;
            }
        }

        _transportControls.SubtitleTracks = subtitleTracks;
        _transportControls.SelectedSubtitleIndex = defaultSubtitleIndex;
    }

    private void OnTimedMetadataTracksChanged(MediaPlaybackItem sender, IVectorChangedEventArgs args)
    {
        if (args.CollectionChange != CollectionChange.ItemInserted)
        {
            return;
        }

        // Capture the sender reference for use in the dispatcher callback
        MediaPlaybackItem playbackItem = sender;

        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            CoreDispatcherPriority.Low,
            () =>
            {
                try
                {
                    // Verify the playback item is still current
                    if (playbackItem != _currentPlaybackItem)
                    {
                        return;
                    }

                    PresentSelectedSubtitleTrack(playbackItem);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnTimedMetadataTracksChanged: {ex}");
                }
            });
    }

    private void PresentSelectedSubtitleTrack(MediaPlaybackItem playbackItem)
    {
        int selectedIndex = _playbackProgressInfo?.SubtitleStreamIndex ?? -1;
        if (selectedIndex < 0)
        {
            for (uint i = 0; i < playbackItem.TimedMetadataTracks.Count; i++)
            {
                playbackItem.TimedMetadataTracks.SetPresentationMode(i, TimedMetadataTrackPresentationMode.Disabled);
            }

            return;
        }

        int? uwpIndex = GetSubtitleUwpIndex(selectedIndex);
        if (!uwpIndex.HasValue || uwpIndex.Value >= playbackItem.TimedMetadataTracks.Count)
        {
            Debug.WriteLine(
                $"Subtitle UWP index out of range for Jellyfin index {selectedIndex} (uwp={uwpIndex}, count={playbackItem.TimedMetadataTracks.Count}).");
            return;
        }

        for (uint i = 0; i < playbackItem.TimedMetadataTracks.Count; i++)
        {
            var mode = i == uwpIndex.Value
                ? TimedMetadataTrackPresentationMode.PlatformPresented
                : TimedMetadataTrackPresentationMode.Disabled;
            playbackItem.TimedMetadataTracks.SetPresentationMode(i, mode);
        }
    }

    /// <summary>
    /// Looks up the precomputed UWP track index for a given Jellyfin subtitle stream index.
    /// </summary>
    private int? GetSubtitleUwpIndex(int jellyfinIndex)
        => _transportControls?.SubtitleTracks?.FirstOrDefault(t => t.JellyfinIndex == jellyfinIndex)?.UwpTrackIndex;

    [RelayCommand]
    private void SelectAudioTrack(TrackInfo track)
    {
        _playbackProgressInfo?.AudioStreamIndex = track.JellyfinIndex;
        _transportControls?.SelectedAudioIndex = track.JellyfinIndex;

        // Try seamless switch if we're in direct play
        if (_isDirectPlay
            && _currentPlaybackItem is not null
            && track.UwpTrackIndex.HasValue
            && track.UwpTrackIndex.Value < _currentPlaybackItem.AudioTracks.Count)
        {
            _currentPlaybackItem.AudioTracks.SelectedIndex = track.UwpTrackIndex.Value;
            Debug.WriteLine($"Seamless audio track switch to Jellyfin index {track.JellyfinIndex} (UWP index {track.UwpTrackIndex})");
            return;
        }

        // Fall back to restarting playback (e.g., for transcoding scenarios)
        Debug.WriteLine($"Restarting playback for audio track {track.JellyfinIndex}");
        RestartPlaybackWithCurrentPosition();
    }

    [RelayCommand]
    private void SelectSubtitleTrack(TrackInfo track)
    {
        // JellyfinIndex of -1 means "off"
        _playbackProgressInfo?.SubtitleStreamIndex = track.JellyfinIndex;
        _transportControls?.SelectedSubtitleIndex = track.JellyfinIndex;

        bool isDisabling = track.JellyfinIndex == -1;
        bool canSeamlessSwitch = _isDirectPlay
            && _currentPlaybackItem is not null
            && (isDisabling || (track.UwpTrackIndex.HasValue && track.UwpTrackIndex.Value < _currentPlaybackItem.TimedMetadataTracks.Count));

        if (canSeamlessSwitch)
        {
            PresentSelectedSubtitleTrack(_currentPlaybackItem!);
            Debug.WriteLine(track.UwpTrackIndex.HasValue
                ? $"Seamless subtitle switch to Jellyfin index {track.JellyfinIndex} (UWP index {track.UwpTrackIndex})"
                : "Subtitles disabled");
            return;
        }

        // Fall back to restarting playback (e.g., switching unloaded external tracks, transcoding, unsupported formats)
        Debug.WriteLine($"Restarting playback for subtitle track {track.JellyfinIndex}");
        RestartPlaybackWithCurrentPosition();
    }

    /// <summary>
    /// Returns whether the stream has a UWP TimedMetadataTrack that can be toggled without restarting playback.
    /// </summary>
    private static bool HasClientSideUwpSubtitleTrack(MediaStream stream, int selectedSubtitleIndex)
    {
        if (!IsSubtitleFormatSupported(stream))
        {
            return false;
        }

        return GetSubtitleDeliveryMethod(stream) switch
        {
            MediaStream_DeliveryMethod.Embed => true,
            MediaStream_DeliveryMethod.External => stream.Index == selectedSubtitleIndex,
            _ => false,
        };
    }

    private static MediaStream_DeliveryMethod GetSubtitleDeliveryMethod(MediaStream stream)
    {
        if (stream.DeliveryMethod.HasValue)
        {
            return stream.DeliveryMethod.Value;
        }

        return stream.IsExternal.GetValueOrDefault()
            ? MediaStream_DeliveryMethod.External
            : MediaStream_DeliveryMethod.Embed;
    }

    /// <summary>
    /// Determines if a subtitle format is supported by UWP MediaPlayer.
    /// </summary>
    /// <param name="stream">The subtitle media stream to check.</param>
    /// <returns>True if the subtitle format is supported for playback.</returns>
    private static bool IsSubtitleFormatSupported(MediaStream stream)
    {
        if (stream.Codec is null)
        {
            return false;
        }

        return stream.IsExternal.GetValueOrDefault()
            ? DeviceProfileManager.SupportedExternalSubtitleFormats.Contains(stream.Codec)
            : DeviceProfileManager.SupportedEmbeddedSubtitleFormats.Contains(stream.Codec);
    }
}
