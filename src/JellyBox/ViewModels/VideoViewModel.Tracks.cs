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
        // We include ALL subtitles in the list, but only assign UwpTrackIndex to supported formats.
        // Unsupported formats will trigger a playback restart to force server-side transcoding.
        List<TrackInfo> subtitleTracks = [];
        int defaultSubtitleIndex = selectedSubtitleIndex ?? mediaSourceInfo.DefaultSubtitleStreamIndex ?? -1;
        int uwpSubtitleIndex = 0;

        // First pass: embedded subtitles
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && !s.IsExternal.GetValueOrDefault()))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            bool isSupported = IsSubtitleFormatSupported(stream);

            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                isSupported ? uwpSubtitleIndex : null,
                displayName));

            if (isSupported)
            {
                uwpSubtitleIndex++;
            }
        }

        // Second pass: external subtitles
        foreach (MediaStream stream in mediaSourceInfo.MediaStreams
            .Where(s => s.Type == MediaStream_Type.Subtitle && s.IsExternal.GetValueOrDefault()))
        {
            string displayName = stream.DisplayTitle ?? stream.Language ?? $"Track {stream.Index}";
            bool isSupported = IsSubtitleFormatSupported(stream);

            subtitleTracks.Add(new TrackInfo(
                stream.Index!.Value,
                isSupported ? uwpSubtitleIndex : null,
                displayName));

            if (isSupported)
            {
                uwpSubtitleIndex++;
            }
        }

        _transportControls.SubtitleTracks = subtitleTracks;
        _transportControls.SelectedSubtitleIndex = defaultSubtitleIndex;
    }

    private void OnTimedMetadataTracksChanged(MediaPlaybackItem sender, IVectorChangedEventArgs args)
    {
        // Capture the sender reference for use in the dispatcher callback
        MediaPlaybackItem playbackItem = sender;

        _ = CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
            () =>
            {
                try
                {
                    // Verify the playback item is still current
                    if (playbackItem != _currentPlaybackItem)
                    {
                        return;
                    }

                    // Present the selected subtitle track if one is selected
                    int selectedIndex = _playbackProgressInfo?.SubtitleStreamIndex ?? -1;
                    int? uwpIndex = GetSubtitleUwpIndex(selectedIndex);
                    if (uwpIndex.HasValue && uwpIndex.Value < playbackItem.TimedMetadataTracks.Count)
                    {
                        playbackItem.TimedMetadataTracks.SetPresentationMode((uint)uwpIndex.Value, TimedMetadataTrackPresentationMode.PlatformPresented);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnTimedMetadataTracksChanged: {ex}");
                }
            });
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
            // Set each track's presentation mode in a single pass
            for (uint i = 0; i < _currentPlaybackItem!.TimedMetadataTracks.Count; i++)
            {
                var mode = i == track.UwpTrackIndex
                    ? TimedMetadataTrackPresentationMode.PlatformPresented
                    : TimedMetadataTrackPresentationMode.Disabled;
                _currentPlaybackItem.TimedMetadataTracks.SetPresentationMode(i, mode);
            }

            Debug.WriteLine(track.UwpTrackIndex.HasValue
                ? $"Seamless subtitle switch to Jellyfin index {track.JellyfinIndex} (UWP index {track.UwpTrackIndex})"
                : "Subtitles disabled");
            return;
        }

        // Fall back to restarting playback (e.g., for transcoding scenarios or unsupported formats)
        Debug.WriteLine($"Restarting playback for subtitle track {track.JellyfinIndex}");
        RestartPlaybackWithCurrentPosition();
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
