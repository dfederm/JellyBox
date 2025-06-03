using Jellyfin.Sdk.Generated.Models;
using Windows.Graphics.Display.Core;

namespace JellyBox.Services;

/// <summary>
/// Manages the display mode of the application.
/// </summary>
internal static class DisplayModeManager
{
    public static async Task SetBestDisplayModeAsync(uint videoWidth, uint videoHeight, double videoFrameRate, MediaStream_VideoRangeType videoRangeType)
    {
        HdmiDisplayInformation hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiDisplayInformation is null)
        {
            return;
        }

        IReadOnlyList<HdmiDisplayMode> supportedDisplayModes = hdmiDisplayInformation.GetSupportedDisplayModes();
        HdmiDisplayMode currentHdmiDisplayMode = hdmiDisplayInformation.GetCurrentDisplayMode();
        HdmiDisplayHdrOption hdmiDisplayHdrOption = GetHdmiDisplayHdrOption(supportedDisplayModes, videoRangeType);
        HdmiDisplayMode? bestDisplayMode = GetBestDisplayMode(supportedDisplayModes, currentHdmiDisplayMode, hdmiDisplayHdrOption, videoWidth, videoHeight, videoFrameRate);
        if (bestDisplayMode is not null)
        {
            await hdmiDisplayInformation.RequestSetCurrentDisplayModeAsync(bestDisplayMode, hdmiDisplayHdrOption);
        }
    }

    public static async Task SetDefaultDisplayModeAsync()
    {
        HdmiDisplayInformation hdmiDisplayInformation = HdmiDisplayInformation.GetForCurrentView();
        if (hdmiDisplayInformation is null)
        {
            return;
        }

        await hdmiDisplayInformation.SetDefaultDisplayModeAsync();
    }

    private static HdmiDisplayHdrOption GetHdmiDisplayHdrOption(IReadOnlyList<HdmiDisplayMode> supportedDisplayModes, MediaStream_VideoRangeType videoRangeType)
    {
        bool displaySupportsDoVi = supportedDisplayModes.Any(mode => mode.IsDolbyVisionLowLatencySupported);
        bool displaySupportsHdr = supportedDisplayModes.Any(mode => mode.IsSmpte2084Supported);

        HdmiDisplayHdrOption hdrOtherwiseSdr = displaySupportsHdr ? HdmiDisplayHdrOption.Eotf2084 : HdmiDisplayHdrOption.None;
        HdmiDisplayHdrOption doViOtherwiseHdrOtherwiseSdr = displaySupportsDoVi ? HdmiDisplayHdrOption.DolbyVisionLowLatency : hdrOtherwiseSdr;

        switch (videoRangeType)
        {
            // Xbox only supports DOVI profile 5
            case MediaStream_VideoRangeType.DOVI:
                return doViOtherwiseHdrOtherwiseSdr;
            case MediaStream_VideoRangeType.DOVIWithHDR10:
            case MediaStream_VideoRangeType.DOVIWithHLG:
            case MediaStream_VideoRangeType.HDR10:
            case MediaStream_VideoRangeType.HDR10Plus:
            case MediaStream_VideoRangeType.HLG:
                return hdrOtherwiseSdr;
            case MediaStream_VideoRangeType.DOVIWithSDR:
            case MediaStream_VideoRangeType.SDR:
            case MediaStream_VideoRangeType.Unknown:
            default:
                return HdmiDisplayHdrOption.None;
        }
    }

    private static bool MatchesRefreshRate(HdmiDisplayMode mode, double refreshRate) => Math.Abs(refreshRate - mode.RefreshRate) <= 0.5;

    private static bool MatchesResolution(HdmiDisplayMode mode, uint width, uint height) => mode.ResolutionWidthInRawPixels == width || mode.ResolutionHeightInRawPixels == height;

    private static bool HdrMatches(HdmiDisplayHdrOption hdmiDisplayHdrOption, HdmiDisplayMode hdmiDisplayMode)
        => hdmiDisplayHdrOption == HdmiDisplayHdrOption.None
            || (hdmiDisplayHdrOption == HdmiDisplayHdrOption.DolbyVisionLowLatency && hdmiDisplayMode.IsDolbyVisionLowLatencySupported)
            || (hdmiDisplayHdrOption == HdmiDisplayHdrOption.Eotf2084 && hdmiDisplayMode.IsSmpte2084Supported);

    private static HdmiDisplayMode? GetBestDisplayMode(
        IReadOnlyList<HdmiDisplayMode> supportedDisplayModes,
        HdmiDisplayMode currentHdmiDisplayMode,
        HdmiDisplayHdrOption hdmiDisplayHdrOption,
        uint videoWidth,
        uint videoHeight,
        double videoFrameRate)
    {
        HdmiDisplayMode[] hdmiDisplayModes = supportedDisplayModes
            .Where(mode => HdrMatches(hdmiDisplayHdrOption, mode))
            .ToArray();

        bool filteredToVideoResolution = false;
        HdmiDisplayMode[] matchingResolution = hdmiDisplayModes
            .Where(mode => MatchesResolution(mode, videoWidth, videoHeight))
            .ToArray();
        if (matchingResolution.Length != 0)
        {
            hdmiDisplayModes = matchingResolution;
            filteredToVideoResolution = true;
        }

        if (!filteredToVideoResolution)
        {
            hdmiDisplayModes = hdmiDisplayModes
                .Where(mode => MatchesResolution(mode, currentHdmiDisplayMode.ResolutionWidthInRawPixels, currentHdmiDisplayMode.ResolutionHeightInRawPixels))
                .ToArray();
        }

        HdmiDisplayMode? matchingRefreshRate = hdmiDisplayModes
            .Where(mode => MatchesRefreshRate(mode, videoFrameRate))
            .FirstOrDefault();
        if (matchingRefreshRate is not null)
        {
            return matchingRefreshRate;
        }

        // fall back to current resolution/refreshRate as a mode that supports the hdmiDisplayHdrOption is required.
        return hdmiDisplayModes
            .Where(mode => MatchesResolution(mode, currentHdmiDisplayMode.ResolutionWidthInRawPixels, currentHdmiDisplayMode.ResolutionHeightInRawPixels))
            .Where(mode => MatchesRefreshRate(mode, currentHdmiDisplayMode.RefreshRate))
            .FirstOrDefault();
    }
}
