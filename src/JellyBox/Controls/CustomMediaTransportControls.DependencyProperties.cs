using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace JellyBox.Controls;

internal sealed partial class CustomMediaTransportControls
{
    #region State Dependency Properties

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(CustomMediaTransportControls),
        new PropertyMetadata(false, OnIsPlayingChanged));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly DependencyProperty IsFavoriteProperty = DependencyProperty.Register(
        nameof(IsFavorite), typeof(bool), typeof(CustomMediaTransportControls),
        new PropertyMetadata(false, OnIsFavoriteChanged));

    public bool IsFavorite
    {
        get => (bool)GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
        nameof(Volume), typeof(double), typeof(CustomMediaTransportControls),
        new PropertyMetadata(1.0, OnVolumeChanged));

    public double Volume
    {
        get => (double)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
        nameof(IsMuted), typeof(bool), typeof(CustomMediaTransportControls),
        new PropertyMetadata(false, OnIsMutedChanged));

    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly DependencyProperty IsBufferingProperty = DependencyProperty.Register(
        nameof(IsBuffering), typeof(bool), typeof(CustomMediaTransportControls),
        new PropertyMetadata(false));

    public bool IsBuffering
    {
        get => (bool)GetValue(IsBufferingProperty);
        set => SetValue(IsBufferingProperty, value);
    }

    public static readonly DependencyProperty EndsAtTextProperty = DependencyProperty.Register(
        nameof(EndsAtText), typeof(string), typeof(CustomMediaTransportControls),
        new PropertyMetadata("Ends at --:--", OnEndsAtTextChanged));

    public string EndsAtText
    {
        get => (string)GetValue(EndsAtTextProperty);
        set => SetValue(EndsAtTextProperty, value);
    }

    public static readonly DependencyProperty AudioTracksProperty = DependencyProperty.Register(
        nameof(AudioTracks), typeof(IReadOnlyList<TrackInfo>), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null, OnAudioTracksChanged));

    public IReadOnlyList<TrackInfo>? AudioTracks
    {
        get => (IReadOnlyList<TrackInfo>?)GetValue(AudioTracksProperty);
        set => SetValue(AudioTracksProperty, value);
    }

    public static readonly DependencyProperty SelectedAudioIndexProperty = DependencyProperty.Register(
        nameof(SelectedAudioIndex), typeof(int), typeof(CustomMediaTransportControls),
        new PropertyMetadata(-1, OnSelectedAudioIndexChanged));

    public int SelectedAudioIndex
    {
        get => (int)GetValue(SelectedAudioIndexProperty);
        set => SetValue(SelectedAudioIndexProperty, value);
    }

    public static readonly DependencyProperty SubtitleTracksProperty = DependencyProperty.Register(
        nameof(SubtitleTracks), typeof(IReadOnlyList<TrackInfo>), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null, OnSubtitleTracksChanged));

    public IReadOnlyList<TrackInfo>? SubtitleTracks
    {
        get => (IReadOnlyList<TrackInfo>?)GetValue(SubtitleTracksProperty);
        set => SetValue(SubtitleTracksProperty, value);
    }

    public static readonly DependencyProperty SelectedSubtitleIndexProperty = DependencyProperty.Register(
        nameof(SelectedSubtitleIndex), typeof(int), typeof(CustomMediaTransportControls),
        new PropertyMetadata(-1, OnSelectedSubtitleIndexChanged));

    public int SelectedSubtitleIndex
    {
        get => (int)GetValue(SelectedSubtitleIndexProperty);
        set => SetValue(SelectedSubtitleIndexProperty, value);
    }

    public static readonly DependencyProperty PlaybackSpeedProperty = DependencyProperty.Register(
        nameof(PlaybackSpeed), typeof(double), typeof(CustomMediaTransportControls),
        new PropertyMetadata(1.0, OnPlaybackSpeedChanged));

    public double PlaybackSpeed
    {
        get => (double)GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    public static readonly DependencyProperty StretchModeProperty = DependencyProperty.Register(
        nameof(StretchMode), typeof(Stretch), typeof(CustomMediaTransportControls),
        new PropertyMetadata(Stretch.Uniform, OnStretchModeChanged));

    public Stretch StretchMode
    {
        get => (Stretch)GetValue(StretchModeProperty);
        set => SetValue(StretchModeProperty, value);
    }

    #endregion

    #region Command Dependency Properties

    public static readonly DependencyProperty PlayPauseCommandProperty = DependencyProperty.Register(
        nameof(PlayPauseCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? PlayPauseCommand
    {
        get => (ICommand?)GetValue(PlayPauseCommandProperty);
        set => SetValue(PlayPauseCommandProperty, value);
    }

    public static readonly DependencyProperty RewindCommandProperty = DependencyProperty.Register(
        nameof(RewindCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? RewindCommand
    {
        get => (ICommand?)GetValue(RewindCommandProperty);
        set => SetValue(RewindCommandProperty, value);
    }

    public static readonly DependencyProperty FastForwardCommandProperty = DependencyProperty.Register(
        nameof(FastForwardCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? FastForwardCommand
    {
        get => (ICommand?)GetValue(FastForwardCommandProperty);
        set => SetValue(FastForwardCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleFavoriteCommandProperty = DependencyProperty.Register(
        nameof(ToggleFavoriteCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ToggleFavoriteCommand
    {
        get => (ICommand?)GetValue(ToggleFavoriteCommandProperty);
        set => SetValue(ToggleFavoriteCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleMuteCommandProperty = DependencyProperty.Register(
        nameof(ToggleMuteCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ToggleMuteCommand
    {
        get => (ICommand?)GetValue(ToggleMuteCommandProperty);
        set => SetValue(ToggleMuteCommandProperty, value);
    }

    public static readonly DependencyProperty ChangeVolumeCommandProperty = DependencyProperty.Register(
        nameof(ChangeVolumeCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ChangeVolumeCommand
    {
        get => (ICommand?)GetValue(ChangeVolumeCommandProperty);
        set => SetValue(ChangeVolumeCommandProperty, value);
    }

    public static readonly DependencyProperty SelectAudioTrackCommandProperty = DependencyProperty.Register(
        nameof(SelectAudioTrackCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? SelectAudioTrackCommand
    {
        get => (ICommand?)GetValue(SelectAudioTrackCommandProperty);
        set => SetValue(SelectAudioTrackCommandProperty, value);
    }

    public static readonly DependencyProperty SelectSubtitleTrackCommandProperty = DependencyProperty.Register(
        nameof(SelectSubtitleTrackCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? SelectSubtitleTrackCommand
    {
        get => (ICommand?)GetValue(SelectSubtitleTrackCommandProperty);
        set => SetValue(SelectSubtitleTrackCommandProperty, value);
    }

    public static readonly DependencyProperty ChangePlaybackSpeedCommandProperty = DependencyProperty.Register(
        nameof(ChangePlaybackSpeedCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ChangePlaybackSpeedCommand
    {
        get => (ICommand?)GetValue(ChangePlaybackSpeedCommandProperty);
        set => SetValue(ChangePlaybackSpeedCommandProperty, value);
    }

    public static readonly DependencyProperty ChangeStretchModeCommandProperty = DependencyProperty.Register(
        nameof(ChangeStretchModeCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ChangeStretchModeCommand
    {
        get => (ICommand?)GetValue(ChangeStretchModeCommandProperty);
        set => SetValue(ChangeStretchModeCommandProperty, value);
    }

    public static readonly DependencyProperty ShowPlaybackInfoCommandProperty = DependencyProperty.Register(
        nameof(ShowPlaybackInfoCommand), typeof(ICommand), typeof(CustomMediaTransportControls),
        new PropertyMetadata(null));

    public ICommand? ShowPlaybackInfoCommand
    {
        get => (ICommand?)GetValue(ShowPlaybackInfoCommandProperty);
        set => SetValue(ShowPlaybackInfoCommandProperty, value);
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdatePlayPauseIcon();

    private static void OnIsFavoriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateFavoriteIcon();

    private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateVolumeState();

    private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateVolumeIcon();

    private static void OnEndsAtTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateEndsAtText();

    private static void OnAudioTracksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).RebuildAudioTracksFlyout();

    private static void OnSelectedAudioIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateAudioTracksCheckedState();

    private static void OnSubtitleTracksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).RebuildSubtitlesFlyout();

    private static void OnSelectedSubtitleIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateSubtitlesCheckedState();

    private static void OnPlaybackSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdatePlaybackSpeedCheckedState();

    private static void OnStretchModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CustomMediaTransportControls)d).UpdateStretchModeCheckedState();

    #endregion
}
