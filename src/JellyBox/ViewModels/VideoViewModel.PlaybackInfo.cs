using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Jellyfin.Sdk.Generated.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812
internal sealed partial class VideoViewModel
#pragma warning restore CA1812
{
    [RelayCommand]
    private async Task ShowPlaybackInfoAsync()
    {
        try
        {
            if (_currentMediaSource is null || _currentItem is null)
            {
                return;
            }

            // Build playback info text
            StringBuilder info = new();

            info.AppendLine($"Title: {_currentItem.Name}");
            info.AppendLine();

            // Media source info
            info.AppendLine("Media Source:");
            if (!string.IsNullOrEmpty(_currentMediaSource.Container))
            {
                info.AppendLine($"  Container: {_currentMediaSource.Container.ToUpperInvariant()}");
            }
            if (_currentMediaSource.Size.HasValue)
            {
                info.AppendLine($"  Size: {FormatFileSize(_currentMediaSource.Size.Value)}");
            }
            if (_currentMediaSource.Bitrate.HasValue)
            {
                info.AppendLine($"  Bitrate: {FormatBitrate(_currentMediaSource.Bitrate.Value)}");
            }
            info.AppendLine();

            // Video stream info
            MediaStream? videoStream = _currentMediaSource.MediaStreams?
                .FirstOrDefault(s => s.Type == MediaStream_Type.Video);
            if (videoStream != null)
            {
                info.AppendLine("Video:");
                string videoCodec = videoStream.Codec?.ToUpperInvariant() ?? "Unknown";
                if (!string.IsNullOrEmpty(videoStream.Profile))
                {
                    videoCodec += $" {videoStream.Profile}";
                }
                info.AppendLine($"  Codec: {videoCodec}");
                info.AppendLine($"  Resolution: {videoStream.Width}x{videoStream.Height}");
                if (videoStream.RealFrameRate.HasValue)
                {
                    info.AppendLine($"  Frame Rate: {videoStream.RealFrameRate:F2} fps");
                }
                if (videoStream.BitRate.HasValue)
                {
                    info.AppendLine($"  Bitrate: {FormatBitrate(videoStream.BitRate.Value)}");
                }
                if (videoStream.VideoRangeType.HasValue)
                {
                    string rangeDisplay = videoStream.VideoDoViTitle ?? videoStream.VideoRangeType.Value.ToString();
                    info.AppendLine($"  Range: {rangeDisplay}");
                }
                if (!string.IsNullOrEmpty(videoStream.PixelFormat))
                {
                    info.AppendLine($"  Pixel Format: {videoStream.PixelFormat}");
                }
                info.AppendLine();
            }

            // Audio stream info
            int audioIndex = _playbackProgressInfo?.AudioStreamIndex ?? _currentMediaSource.DefaultAudioStreamIndex ?? -1;
            MediaStream? audioStream = _currentMediaSource.MediaStreams?
                .FirstOrDefault(s => s.Type == MediaStream_Type.Audio && s.Index == audioIndex);
            if (audioStream != null)
            {
                info.AppendLine("Audio:");
                string audioCodec = audioStream.Codec?.ToUpperInvariant() ?? "Unknown";
                if (!string.IsNullOrEmpty(audioStream.Profile))
                {
                    audioCodec += $" {audioStream.Profile}";
                }
                info.AppendLine($"  Codec: {audioCodec}");
                if (audioStream.Channels.HasValue)
                {
                    info.AppendLine($"  Channels: {audioStream.Channels}");
                }
                if (audioStream.BitRate.HasValue)
                {
                    info.AppendLine($"  Bitrate: {FormatBitrate(audioStream.BitRate.Value)}");
                }
                if (audioStream.SampleRate.HasValue)
                {
                    info.AppendLine($"  Sample Rate: {audioStream.SampleRate} Hz");
                }
                if (audioStream.BitDepth.HasValue)
                {
                    info.AppendLine($"  Bit Depth: {audioStream.BitDepth}");
                }
                if (!string.IsNullOrEmpty(audioStream.Language))
                {
                    info.AppendLine($"  Language: {audioStream.Language}");
                }
                info.AppendLine();
            }

            // Subtitle stream info
            int subtitleIndex = _playbackProgressInfo?.SubtitleStreamIndex ?? _currentMediaSource.DefaultSubtitleStreamIndex ?? -1;
            if (subtitleIndex >= 0)
            {
                MediaStream? subtitleStream = _currentMediaSource.MediaStreams?
                    .FirstOrDefault(s => s.Type == MediaStream_Type.Subtitle && s.Index == subtitleIndex);
                if (subtitleStream != null)
                {
                    info.AppendLine("Subtitles:");
                    info.AppendLine($"  Format: {subtitleStream.Codec?.ToUpperInvariant()}");
                    if (!string.IsNullOrEmpty(subtitleStream.Language))
                    {
                        info.AppendLine($"  Language: {subtitleStream.Language}");
                    }
                    info.AppendLine($"  Delivery: {(subtitleStream.IsExternal == true ? "External" : "Embedded")}");
                    info.AppendLine();
                }
            }

            // Playback method
            info.AppendLine("Playback:");
            string playMethod;
            if (_currentMediaSource.SupportsDirectPlay.GetValueOrDefault())
            {
                playMethod = "Direct Play";
            }
            else if (_currentMediaSource.SupportsDirectStream.GetValueOrDefault())
            {
                playMethod = "Direct Stream";
            }
            else
            {
                playMethod = "Transcoding";
            }
            info.AppendLine($"  Method: {playMethod}");

            // Transcoding info
            if (playMethod == "Transcoding")
            {
                if (!string.IsNullOrEmpty(_currentMediaSource.TranscodingContainer))
                {
                    info.AppendLine($"  Container: {_currentMediaSource.TranscodingContainer.ToUpperInvariant()}");
                }
                if (!string.IsNullOrEmpty(_currentMediaSource.TranscodingUrl))
                {
                    // Parse transcode reasons from URL if available
                    if (_currentMediaSource.TranscodingUrl.Contains("TranscodeReasons=", StringComparison.Ordinal))
                    {
                        int start = _currentMediaSource.TranscodingUrl.IndexOf("TranscodeReasons=", StringComparison.Ordinal) + 17;
                        int end = _currentMediaSource.TranscodingUrl.IndexOf('&', start);
                        if (end == -1)
                        {
                            end = _currentMediaSource.TranscodingUrl.Length;
                        }

                        string reasons = Uri.UnescapeDataString(_currentMediaSource.TranscodingUrl[start..end]);
                        if (!string.IsNullOrEmpty(reasons))
                        {
                            info.AppendLine($"  Reasons: {reasons.Replace(",", ", ", StringComparison.Ordinal)}");
                        }
                    }
                }
            }

            // Player info
            if (_playerElement?.MediaPlayer != null)
            {
                info.AppendLine();
                info.AppendLine("Player:");
                info.AppendLine($"  State: {_playerElement.MediaPlayer.PlaybackSession.PlaybackState}");
                info.AppendLine($"  Playback Rate: {_playerElement.MediaPlayer.PlaybackSession.PlaybackRate}x");
                info.AppendLine($"  Volume: {_playerElement.MediaPlayer.Volume * 100:F0}%");
            }

            ContentDialog dialog = new()
            {
                Title = "Playback Info",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = info.ToString(),
                        FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    MaxHeight = 400
                },
                CloseButtonText = "Close",
                XamlRoot = _playerElement?.XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing playback info: {ex}");
        }
    }

    private static string FormatBitrate(long bitrate)
    {
        if (bitrate >= 1_000_000)
        {
            return $"{bitrate / 1_000_000.0:F1} Mbps";
        }
        return $"{bitrate / 1000} kbps";
    }

    private static string FormatFileSize(long bytes)
    {
        const long GB = 1024L * 1024 * 1024;
        const long MB = 1024L * 1024;

        if (bytes >= GB)
        {
            return $"{bytes / (double)GB:F2} GB";
        }
        return $"{bytes / (double)MB:F1} MB";
    }
}
