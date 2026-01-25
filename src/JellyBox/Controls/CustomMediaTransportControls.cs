using Windows.UI.Xaml.Controls;

namespace JellyBox.Controls;

/// <summary>
/// Custom media transport controls with Jellyfin-specific features.
/// </summary>
/// <remarks>
/// This class is split across multiple partial class files:
/// - CustomMediaTransportControls.cs (this file) - Constructor and template setup
/// - CustomMediaTransportControls.DependencyProperties.cs - State and command dependency properties
/// - CustomMediaTransportControls.IconUpdates.cs - Icon update methods and volume slider sync
/// - CustomMediaTransportControls.Flyouts.cs - Audio, subtitle, and settings flyout management
/// </remarks>
internal sealed partial class CustomMediaTransportControls : MediaTransportControls
{
    #region Template Part Names
    private const string PlayPauseIconName = "CustomPlayPauseIcon";
    private const string VolumeSliderName = "CustomVolumeSlider";
    private const string VolumeIconName = "CustomVolumeIcon";
    private const string FavoriteIconName = "FavoriteIcon";
    private const string AudioTracksButtonName = "CustomAudioTracksButton";
    private const string SubtitlesButtonName = "CustomSubtitlesButton";
    private const string SettingsButtonName = "SettingsButton";
    private const string EndsAtTextBlockName = "EndsAtTextBlock";
    #endregion

    #region Template Part References
    private FontIcon? _playPauseIcon;
    private Slider? _volumeSlider;
    private FontIcon? _volumeIcon;
    private FontIcon? _favoriteIcon;
    private Button? _audioTracksButton;
    private Button? _subtitlesButton;
    private Button? _settingsButton;
    private TextBlock? _endsAtTextBlock;
    #endregion

    public CustomMediaTransportControls()
    {
        DefaultStyleKey = typeof(CustomMediaTransportControls);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Get references to template parts
        _playPauseIcon = GetTemplateChild(PlayPauseIconName) as FontIcon;
        _volumeSlider = GetTemplateChild(VolumeSliderName) as Slider;
        _volumeIcon = GetTemplateChild(VolumeIconName) as FontIcon;
        _favoriteIcon = GetTemplateChild(FavoriteIconName) as FontIcon;
        _audioTracksButton = GetTemplateChild(AudioTracksButtonName) as Button;
        _subtitlesButton = GetTemplateChild(SubtitlesButtonName) as Button;
        _settingsButton = GetTemplateChild(SettingsButtonName) as Button;
        _endsAtTextBlock = GetTemplateChild(EndsAtTextBlockName) as TextBlock;

        // Wire up volume slider (two-way sync)
        if (_volumeSlider != null)
        {
            _volumeSlider.ValueChanged -= OnVolumeSliderChanged;
            _volumeSlider.ValueChanged += OnVolumeSliderChanged;
            _volumeSlider.Value = Volume * 100;
        }

        // Set up flyouts
        RebuildAudioTracksFlyout();
        RebuildSubtitlesFlyout();
        SetupSettingsFlyout();

        // Initialize icon states
        UpdatePlayPauseIcon();
        UpdateVolumeIcon();
        UpdateFavoriteIcon();
        UpdateEndsAtText();
    }
}
