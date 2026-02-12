using Jellyfin.Sdk.Generated.Models;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display.Core;
using Windows.Media.Audio;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Render;

namespace JellyBox.Services;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class DeviceProfileManager
#pragma warning restore CA1812
{
    // MFVideoFormat_AV1 GUID (FOURCC 'AV01'). CodecSubtypes.VideoFormatAv1 requires a newer API contract.
    private const string VideoFormatAv1 = "{31305641-0000-0010-8000-00AA00389B71}";

    // Supported embedded subtitle formats for UWP MediaPlayer
    public static readonly HashSet<string> SupportedEmbeddedSubtitleFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "subrip",
        "srt",
        "vtt",
        "webvtt",
        "mov_text",  // MP4 timed text
        "ass",       // Basic support (styling stripped). TODO: Can we render it manually?
        "ssa",       // Basic support (styling stripped). TODO: Can we render it manually?
    };

    public static readonly HashSet<string> SupportedExternalSubtitleFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        // UWP TimedTextSource supports SRT and VTT natively.
        "subrip",
        "srt",
        "vtt",
        "webvtt",

        // "pgssub" // PGS subtitles are not supported natively. TODO: Can we render it manually?
    };

    public DeviceProfile Profile { get; private set; } = null!; // TODO

    // This logic is adapted from the web client's browserDeviceProfile.js
    public async Task InitializeAsync()
    {
        CodecQuery codecQuery = new();

        HashSet<string> videoCodecGuids = new(StringComparer.OrdinalIgnoreCase);

        // For some reason querying video codecs without a specific subtype on Xbox results in an Access Violation.
        // So just query for each specific codec we care about.
        string[] subtypes = [
            CodecSubtypes.VideoFormatHevc.ToString(),
            CodecSubtypes.VideoFormatH264.ToString(),
            CodecSubtypes.VideoFormatMpeg2.ToString(),
            CodecSubtypes.VideoFormatWvc1.ToString(),
            CodecSubtypes.VideoFormatVP80.ToString(),
            CodecSubtypes.VideoFormatVP90.ToString(),
            VideoFormatAv1,
        ];
        foreach (string subtype in subtypes)
        {
            foreach (CodecInfo codecInfo in await codecQuery.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, subtype))
            {
                foreach (string subType in codecInfo.Subtypes)
                {
                    videoCodecGuids.Add(subType);
                }
            }
        }

        HashSet<string> audioCodecGuids = new(StringComparer.OrdinalIgnoreCase);
        foreach (CodecInfo codecInfo in await codecQuery.FindAllAsync(CodecKind.Audio, CodecCategory.Decoder, string.Empty))
        {
            foreach (string subType in codecInfo.Subtypes)
            {
                audioCodecGuids.Add(subType);
            }
        }

        const int maxBitrate = 120_000_000;
        uint audioChannelCount = await GetAudioChannelCountAsync();

        List<string> webmAudioCodecs = ["vorbis"];

        DeviceProfile profile = new()
        {
            MaxStreamingBitrate = maxBitrate,
            MaxStaticBitrate = 100_000_000,
            MusicStreamingTranscodingBitrate = Math.Min(maxBitrate, 384_000),
            DirectPlayProfiles = [],
            TranscodingProfiles = [],
            ContainerProfiles = [],
            CodecProfiles = [],
            SubtitleProfiles = [],
        };

        List<string> videoAudioCodecs = [];
        List<string> hlsInTsVideoAudioCodecs = [];
        List<string> hlsInFmp4VideoAudioCodecs = [];

        // Detect max video width from HDMI display modes (4K on Xbox One S and later).
        // Fall back to 1920 if HDMI info is unavailable (e.g. running on desktop).
        int maxVideoWidth = 1920;
        IReadOnlyList<HdmiDisplayMode>? hdmiModes = HdmiDisplayInformation.GetForCurrentView()?.GetSupportedDisplayModes();
        if (hdmiModes is not null)
        {
            foreach (HdmiDisplayMode mode in hdmiModes)
            {
                if (mode.ResolutionWidthInRawPixels > (uint)maxVideoWidth)
                {
                    maxVideoWidth = (int)mode.ResolutionWidthInRawPixels;
                }
            }
        }

        // Transcoding codec is the first in hlsVideoAudioCodecs.
        // Prefer AAC, MP3 to other codecs when audio transcoding.
        bool canPlayAac = audioCodecGuids.Contains(CodecSubtypes.AudioFormatAac);
        if (canPlayAac)
        {
            videoAudioCodecs.Add("aac");
            hlsInTsVideoAudioCodecs.Add("aac");
            hlsInFmp4VideoAudioCodecs.Add("aac");
        }

        bool canPlayMp3 = audioCodecGuids.Contains(CodecSubtypes.AudioFormatMP3);
        if (canPlayMp3)
        {
            videoAudioCodecs.Add("mp3");
            hlsInTsVideoAudioCodecs.Add("mp3");
            hlsInFmp4VideoAudioCodecs.Add("mp3");
        }

        // For AC3/EAC3 remuxing.
        // Do not use AC3 for audio transcoding unless AAC and MP3 are not supported.
        bool canPlayAc3 = audioCodecGuids.Contains(CodecSubtypes.AudioFormatDolbyAC3);
        bool canPlayEac3 = audioCodecGuids.Contains(CodecSubtypes.AudioFormatDolbyDDPlus);
        if (canPlayAc3)
        {
            videoAudioCodecs.Add("ac3");
            hlsInTsVideoAudioCodecs.Add("ac3");
            hlsInFmp4VideoAudioCodecs.Add("ac3");
        }

        if (canPlayEac3)
        {
            videoAudioCodecs.Add("eac3");
            hlsInTsVideoAudioCodecs.Add("eac3");
            hlsInFmp4VideoAudioCodecs.Add("eac3");
        }

        bool canPlayMp2 = audioCodecGuids.Contains(CodecSubtypes.AudioFormatMPeg);
        if (canPlayMp2)
        {
            videoAudioCodecs.Add("mp2");
            hlsInTsVideoAudioCodecs.Add("mp2");
            hlsInFmp4VideoAudioCodecs.Add("mp2");
        }

        // TODO: Check user setting: enableDts
        bool supportsDts = audioCodecGuids.Contains(CodecSubtypes.AudioFormatDts);
        if (supportsDts)
        {
            videoAudioCodecs.Add("dca");
            videoAudioCodecs.Add("dts");
        }

        // TODO: Check user setting: enableTrueHd
        // TrueHD is not natively supported on Xbox UWP, but declaring it allows passthrough to capable receivers.
        videoAudioCodecs.Add("truehd");

        bool canPlayOpus = audioCodecGuids.Contains(CodecSubtypes.AudioFormatOpus);
        if (canPlayOpus)
        {
            videoAudioCodecs.Add("opus");
            webmAudioCodecs.Add("opus");
            hlsInFmp4VideoAudioCodecs.Add("opus");
        }

        bool canPlayFlac = audioCodecGuids.Contains(CodecSubtypes.AudioFormatFlac);
        if (canPlayFlac)
        {
            videoAudioCodecs.Add("flac");
            hlsInFmp4VideoAudioCodecs.Add("flac");
        }

        bool canPlayAlac = audioCodecGuids.Contains(CodecSubtypes.AudioFormatAlac);
        if (canPlayAlac)
        {
            videoAudioCodecs.Add("alac");
            hlsInFmp4VideoAudioCodecs.Add("alac");
        }

        List<string> mp4VideoCodecs = [];
        List<string> webmVideoCodecs = [];
        List<string> hlsInTsVideoCodecs = [];
        List<string> hlsInFmp4VideoCodecs = [];

        // av1 main level 5.3
        bool canPlayAv1 = videoCodecGuids.Contains(VideoFormatAv1);
        if (canPlayAv1)
        {
            hlsInFmp4VideoCodecs.Add("av1");
        }

        bool canPlayHevc = videoCodecGuids.Contains(CodecSubtypes.VideoFormatHevc);
        if (canPlayHevc)
        {
            hlsInFmp4VideoCodecs.Add("hevc");
        }

        bool canPlayH264 = videoCodecGuids.Contains(CodecSubtypes.VideoFormatH264);
        if (canPlayH264)
        {
            mp4VideoCodecs.Add("h264");
            hlsInTsVideoCodecs.Add("h264");
            hlsInFmp4VideoCodecs.Add("h264");
        }

        if (canPlayHevc)
        {
            mp4VideoCodecs.Add("hevc");
        }

        bool supportsMpeg2Video = videoCodecGuids.Contains(CodecSubtypes.VideoFormatMpeg2);
        if (supportsMpeg2Video)
        {
            mp4VideoCodecs.Add("mpeg2video");
        }

        bool supportsVc1 = videoCodecGuids.Contains(CodecSubtypes.VideoFormatWvc1);
        if (supportsVc1)
        {
            mp4VideoCodecs.Add("vc1");
        }

        bool canPlayVp8 = videoCodecGuids.Contains(CodecSubtypes.VideoFormatVP80);
        if (canPlayVp8)
        {
            webmVideoCodecs.Add("vp8");
        }

        bool canPlayVp9 = videoCodecGuids.Contains(CodecSubtypes.VideoFormatVP90);
        if (canPlayVp9)
        {
            mp4VideoCodecs.Add("vp9");
            hlsInFmp4VideoCodecs.Add("vp9");
            webmVideoCodecs.Add("vp9");
        }

        if (canPlayAv1)
        {
            mp4VideoCodecs.Add("av1");
            webmVideoCodecs.Add("av1");
        }

        if (canPlayVp8)
        {
            videoAudioCodecs.Add("vorbis");
        }

        if (webmVideoCodecs.Count > 0)
        {
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "webm",
                    Type = DirectPlayProfile_Type.Video,
                    VideoCodec = string.Join(',', webmVideoCodecs),
                    AudioCodec = string.Join(',', webmAudioCodecs),
                });
        }

        if (mp4VideoCodecs.Count > 0)
        {
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "mp4,m4v",
                    Type = DirectPlayProfile_Type.Video,
                    VideoCodec = string.Join(',', mp4VideoCodecs),
                    AudioCodec = string.Join(',', videoAudioCodecs),
                });
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "mkv",
                    Type = DirectPlayProfile_Type.Video,
                    VideoCodec = string.Join(',', mp4VideoCodecs),
                    AudioCodec = string.Join(',', videoAudioCodecs),
                });
        }

        // m2ts container
        {
            List<string> videoCodecs = ["h264"];

            if (supportsVc1)
            {
                videoCodecs.Add("vc1");
            }

            if (supportsMpeg2Video)
            {
                videoCodecs.Add("mpeg2video");
            }

            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "m2ts",
                    Type = DirectPlayProfile_Type.Video,
                    VideoCodec = string.Join(',', videoCodecs),
                    AudioCodec = string.Join(',', videoAudioCodecs),
                });
        }

        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "wmv",
                Type = DirectPlayProfile_Type.Video,
            });

        // ts container
        {
            List<string> videoCodecs = ["h264"];

            if (supportsVc1)
            {
                videoCodecs.Add("vc1");
            }

            if (supportsMpeg2Video)
            {
                videoCodecs.Add("mpeg2video");
            }

            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "ts,mpegts",
                    Type = DirectPlayProfile_Type.Video,
                    VideoCodec = string.Join(',', videoCodecs),
                    AudioCodec = string.Join(',', videoAudioCodecs),
                });
        }

        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "asf",
                Type = DirectPlayProfile_Type.Video
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "avi",
                Type = DirectPlayProfile_Type.Video,
                AudioCodec = string.Join(',', videoAudioCodecs),
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "mpg",
                Type = DirectPlayProfile_Type.Video,
                AudioCodec = string.Join(',', videoAudioCodecs),
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "mpeg",
                Type = DirectPlayProfile_Type.Video,
                AudioCodec = string.Join(',', videoAudioCodecs),
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "mov",
                Type = DirectPlayProfile_Type.Video,
                VideoCodec = "h264",
                AudioCodec = string.Join(',', videoAudioCodecs),
            });

        if (canPlayOpus)
        {
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "webm",
                    Type = DirectPlayProfile_Type.Audio,
                    AudioCodec = "opus",
                });
        }

        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "mp2",
                Type = DirectPlayProfile_Type.Audio,
            });

        if (canPlayAac)
        {
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "aac",
                    Type = DirectPlayProfile_Type.Audio,
                });
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "m4a",
                    Type = DirectPlayProfile_Type.Audio,
                    AudioCodec = "aac",
                });
            profile.DirectPlayProfiles.Add(
                new DirectPlayProfile
                {
                    Container = "m4b",
                    Type = DirectPlayProfile_Type.Audio,
                    AudioCodec = "aac",
                });
        }

        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "flac",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "alac",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "m4a",
                Type = DirectPlayProfile_Type.Audio,
                AudioCodec = "alac",
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "m4b",
                Type = DirectPlayProfile_Type.Audio,
                AudioCodec = "alac",
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "webma",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "webm",
                Type = DirectPlayProfile_Type.Audio,
                AudioCodec = "webma",
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "wma",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "wav",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "ogg",
                Type = DirectPlayProfile_Type.Audio,
            });
        profile.DirectPlayProfiles.Add(
            new DirectPlayProfile
            {
                Container = "oga",
                Type = DirectPlayProfile_Type.Audio,
            });

        profile.TranscodingProfiles.Add(
            new TranscodingProfile
            {
                Container = "mp4",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "aac",
                Context = TranscodingProfile_Context.Streaming,
                Protocol = TranscodingProfile_Protocol.Hls,
                MaxAudioChannels = audioChannelCount.ToString(),
                MinSegments = 1,
                BreakOnNonKeyFrames = false,
                EnableAudioVbrEncoding = false // TODO: ??? !appSettings.disableVbrAudio()
            });

        // For streaming, prioritize opus transcoding after mp3/aac. It is too problematic with random failures
        // But for static (offline sync), it will be just fine.
        // Prioritize aac higher because the encoder can accept more channels than mp3
        if (canPlayAac)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "aac",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "aac",
                Context = TranscodingProfile_Context.Streaming,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        if (canPlayMp3)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "mp3",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "mp3",
                Context = TranscodingProfile_Context.Streaming,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        if (canPlayOpus)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "opus",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "opus",
                Context = TranscodingProfile_Context.Streaming,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        profile.TranscodingProfiles.Add(new TranscodingProfile
        {
            Container = "wav",
            Type = TranscodingProfile_Type.Audio,
            AudioCodec = "wav",
            Context = TranscodingProfile_Context.Streaming,
            Protocol = TranscodingProfile_Protocol.Http,
            MaxAudioChannels = audioChannelCount.ToString(),
        });

        if (canPlayOpus)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "opus",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "opus",
                Context = TranscodingProfile_Context.Static,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        if (canPlayMp3)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "mp3",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "mp3",
                Context = TranscodingProfile_Context.Static,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        if (canPlayAac)
        {
            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "aac",
                Type = TranscodingProfile_Type.Audio,
                AudioCodec = "aac",
                Context = TranscodingProfile_Context.Static,
                Protocol = TranscodingProfile_Protocol.Http,
                MaxAudioChannels = audioChannelCount.ToString(),
            });
        }

        profile.TranscodingProfiles.Add(new TranscodingProfile
        {
            Container = "wav",
            Type = TranscodingProfile_Type.Audio,
            AudioCodec = "wav",
            Context = TranscodingProfile_Context.Static,
            Protocol = TranscodingProfile_Protocol.Http,
            MaxAudioChannels = audioChannelCount.ToString(),
        });

        if (hlsInFmp4VideoCodecs.Count > 0 && hlsInFmp4VideoAudioCodecs.Count > 0)
        {
            // HACK: Since there is no filter for TS/MP4 in the API, specify HLS support in general and rely on retry after DirectPlay error
            // FIXME: Need support for {Container = "mp4", Protocol: "hls"} or {Container = "hls", SubContainer = "mp4"}
            profile.DirectPlayProfiles.Add(new DirectPlayProfile
            {
                Container = "hls",
                Type = DirectPlayProfile_Type.Video,
                VideoCodec = string.Join(',', hlsInFmp4VideoCodecs),
                AudioCodec = string.Join(',', hlsInFmp4VideoAudioCodecs)
            });

            profile.TranscodingProfiles.Add(new TranscodingProfile
            {
                Container = "mp4",
                Type = TranscodingProfile_Type.Video,
                AudioCodec = string.Join(',', hlsInFmp4VideoAudioCodecs),
                VideoCodec = string.Join(',', hlsInFmp4VideoCodecs),
                Context = TranscodingProfile_Context.Streaming,
                Protocol = TranscodingProfile_Protocol.Hls,
                MaxAudioChannels = audioChannelCount.ToString(),
                MinSegments = 1,
                BreakOnNonKeyFrames = false,
            });
        }

        // UWP does not support secondary audio tracks
        List<ProfileCondition> aacCodecProfileConditions =
        [
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.Equals,
                Property = ProfileCondition_Property.IsSecondaryAudio,
                Value = "false",
                IsRequired = false
            },
        ];

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.VideoAudio,
                Codec = "aac",
                Conditions = aacCodecProfileConditions
            });

        // TODO: Add a user setting for allowed audio channels, then add LessThanEqual AudioChannels
        // conditions to both globalAudioCodecProfileConditions and globalVideoAudioCodecProfileConditions.

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.VideoAudio,
                Conditions =
                [
                    new ProfileCondition
                    {
                        Condition = ProfileCondition_Condition.Equals,
                        Property = ProfileCondition_Property.IsSecondaryAudio,
                        Value = "false",
                        IsRequired = false
                    },
                ],
            });

        int maxH264Level = 51;
        string h264Profiles = "high|main|baseline|constrained baseline";

        int maxHevcLevel = 153; // Level 5.1 (4K@60fps)
        string hevcProfiles = "main|main 10";

        int maxAv1Level = 15; // Level 5.3 (covers 4K)
        string av1Profiles = "main"; // AV1 Main profile covers 4:2:0 8 & 10 bits

        string h264VideoRangeTypes = "SDR";
        string hevcVideoRangeTypes = "SDR";
        string vp9VideoRangeTypes = "SDR";
        string av1VideoRangeTypes = "SDR";

        bool supportsHdr10 = hdmiModes?.Any(mode => mode.IsSmpte2084Supported) ?? false;
        if (supportsHdr10)
        {
            hevcVideoRangeTypes += "|HDR10";
            vp9VideoRangeTypes += "|HDR10";
            av1VideoRangeTypes += "|HDR10";
        }

        // HdmiDisplayMode has no IsHlgSupported property and HdmiDisplayHdrOption has no HLG variant.
        // HLG content plays through the HDR10/ST2084 display pipeline with automatic conversion.
        bool supportsHlg = supportsHdr10;
        if (supportsHlg)
        {
            hevcVideoRangeTypes += "|HLG";
            vp9VideoRangeTypes += "|HLG";
            av1VideoRangeTypes += "|HLG";
        }

        bool supportsDolbyVision = hdmiModes?.Any(mode => mode.IsDolbyVisionLowLatencySupported) ?? false;
        if (supportsDolbyVision)
        {
            hevcVideoRangeTypes += "|DOVI";
            hevcVideoRangeTypes += "|DOVIWithHDR10|DOVIWithHLG|DOVIWithSDR";

            // Profile 10 4k@24fps
            av1VideoRangeTypes += "|DOVI|DOVIWithHDR10|DOVIWithHLG|DOVIWithSDR";
        }

        string maxVideoWidthStr = maxVideoWidth.ToString();

        List<ProfileCondition> h264CodecProfileConditions =
        [
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.NotEquals,
                Property = ProfileCondition_Property.IsAnamorphic,
                Value = "true",
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoProfile,
                Value = h264Profiles,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoRangeType,
                Value = h264VideoRangeTypes,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.VideoLevel,
                Value = maxH264Level.ToString(),
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.Width,
                Value = maxVideoWidthStr,
                IsRequired = false
            },
        ];

        List<ProfileCondition> hevcCodecProfileConditions =
        [
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.NotEquals,
                Property = ProfileCondition_Property.IsAnamorphic,
                Value = "true",
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoProfile,
                Value = hevcProfiles,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoRangeType,
                Value = hevcVideoRangeTypes,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.VideoLevel,
                Value = maxHevcLevel.ToString(),
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.Width,
                Value = maxVideoWidthStr,
                IsRequired = false
            },
        ];

        List<ProfileCondition> vp9CodecProfileConditions =
        [
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoRangeType,
                Value = vp9VideoRangeTypes,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.Width,
                Value = maxVideoWidthStr,
                IsRequired = false
            },
        ];

        List<ProfileCondition> av1CodecProfileConditions =
        [
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.NotEquals,
                Property = ProfileCondition_Property.IsAnamorphic,
                Value = "true",
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoProfile,
                Value = av1Profiles,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.EqualsAny,
                Property = ProfileCondition_Property.VideoRangeType,
                Value = av1VideoRangeTypes,
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.VideoLevel,
                Value = maxAv1Level.ToString(),
                IsRequired = false
            },
            new ProfileCondition
            {
                Condition = ProfileCondition_Condition.LessThanEqual,
                Property = ProfileCondition_Property.Width,
                Value = maxVideoWidthStr,
                IsRequired = false
            },
        ];

        // UWP supports hardware deinterlacing, so no interlaced restriction needed.

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.Video,
                Codec = "h264",
                Conditions = h264CodecProfileConditions,
            });

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.Video,
                Codec = "hevc",
                Conditions = hevcCodecProfileConditions,
            });

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.Video,
                Codec = "vp9",
                Conditions = vp9CodecProfileConditions,
            });

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.Video,
                Codec = "av1",
                Conditions = av1CodecProfileConditions,
            });

        profile.CodecProfiles.Add(
            new CodecProfile
            {
                Type = CodecProfile_Type.Video,
                Conditions =
                [
                    new ProfileCondition
                    {
                        Condition = ProfileCondition_Condition.LessThanEqual,
                        Property = ProfileCondition_Property.VideoBitrate,
                        Value = maxBitrate.ToString(),
                    },
                    new ProfileCondition
                    {
                        Condition = ProfileCondition_Condition.LessThanEqual,
                        Property = ProfileCondition_Property.Width,
                        Value = maxVideoWidthStr,
                        IsRequired = false,
                    },
                ],
            });

        // Subtitle profiles
        foreach (string subtitleFormat in SupportedEmbeddedSubtitleFormats)
        {
            profile.SubtitleProfiles.Add(
                new SubtitleProfile
                {
                    Format = subtitleFormat,
                    Method = SubtitleProfile_Method.Embed,
                });
        }

        foreach (string subtitleFormat in SupportedExternalSubtitleFormats)
        {
            profile.SubtitleProfiles.Add(
                new SubtitleProfile
                {
                    Format = subtitleFormat,
                    Method = SubtitleProfile_Method.External,
                });
        }

        Profile = profile;
    }

    private static async Task<uint> GetAudioChannelCountAsync()
    {
        string defaultAudioRenderDeviceId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
        DeviceInformation defaultAudioDevice = await DeviceInformation.CreateFromIdAsync(defaultAudioRenderDeviceId);

        AudioGraphSettings settings = new(AudioRenderCategory.Media)
        {
            PrimaryRenderDevice = defaultAudioDevice
        };
        CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

        if (result.Status != AudioGraphCreationStatus.Success)
        {
            // Audio graph creation failed. Just default to 2.
            return 2;
        }

        using AudioGraph graph = result.Graph;
        return graph.EncodingProperties.ChannelCount;
    }
}
