using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace JellyBox.Controls;

/// <summary>
/// Represents a selectable audio or subtitle track.
/// </summary>
/// <param name="JellyfinIndex">The Jellyfin stream index.</param>
/// <param name="UwpTrackIndex">The UWP MediaPlayer track index, or null if not directly playable.</param>
/// <param name="DisplayName">The display name for the track.</param>
internal sealed record TrackInfo(int JellyfinIndex, int? UwpTrackIndex, string DisplayName);

internal sealed partial class CustomMediaTransportControls
{
    #region Flyout References

    private MenuFlyout? _audioTracksFlyout;
    private MenuFlyout? _subtitlesFlyout;
    private MenuFlyoutSubItem? _playbackSpeedSubItem;
    private MenuFlyoutSubItem? _aspectRatioSubItem;

    #endregion

    #region Audio Tracks Flyout

    private void RebuildAudioTracksFlyout()
    {
        if (_audioTracksButton is null)
        {
            return;
        }

        if (AudioTracks is null || AudioTracks.Count == 0)
        {
            _audioTracksButton.Visibility = Visibility.Collapsed;
            return;
        }

        _audioTracksButton.Visibility = Visibility.Visible;

        _audioTracksFlyout = new() { Placement = FlyoutPlacementMode.Top };
        foreach (TrackInfo track in AudioTracks)
        {
            ToggleMenuFlyoutItem item = CreateFlyoutMenuItem(track, SelectedAudioIndex);
            item.Click += OnAudioTrackItemClicked;
            _audioTracksFlyout.Items.Add(item);
        }

        _audioTracksButton.Flyout = _audioTracksFlyout;
    }

    private void UpdateAudioTracksCheckedState()
    {
        if (_audioTracksFlyout is null)
        {
            return;
        }

        foreach (MenuFlyoutItemBase menuItem in _audioTracksFlyout.Items)
        {
            if (menuItem is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is TrackInfo track)
            {
                toggleItem.IsChecked = track.JellyfinIndex == SelectedAudioIndex;
            }
        }
    }

    private void OnAudioTrackItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem item && item.Tag is TrackInfo track)
        {
            SelectAudioTrackCommand?.Execute(track);
        }
    }

    #endregion

    #region Subtitles Flyout

    private void RebuildSubtitlesFlyout()
    {
        if (_subtitlesButton is null)
        {
            return;
        }

        if (SubtitleTracks is null || SubtitleTracks.Count == 0)
        {
            _subtitlesButton.Visibility = Visibility.Collapsed;
            return;
        }

        _subtitlesButton.Visibility = Visibility.Visible;

        _subtitlesFlyout = new() { Placement = FlyoutPlacementMode.Top };

        // Add "Off" option at the top
        TrackInfo offTrack = new(-1, UwpTrackIndex: null, "Off");
        ToggleMenuFlyoutItem offItem = CreateFlyoutMenuItem(offTrack, SelectedSubtitleIndex);

        offItem.Click += OnSubtitleTrackItemClicked;
        _subtitlesFlyout.Items.Add(offItem);

        _subtitlesFlyout.Items.Add(new MenuFlyoutSeparator());

        foreach (TrackInfo track in SubtitleTracks)
        {
            ToggleMenuFlyoutItem item = CreateFlyoutMenuItem(track, SelectedSubtitleIndex);
            item.Click += OnSubtitleTrackItemClicked;
            _subtitlesFlyout.Items.Add(item);
        }

        _subtitlesButton.Flyout = _subtitlesFlyout;
    }

    private void UpdateSubtitlesCheckedState()
    {
        if (_subtitlesFlyout is null)
        {
            return;
        }

        foreach (MenuFlyoutItemBase menuItem in _subtitlesFlyout.Items)
        {
            if (menuItem is ToggleMenuFlyoutItem toggleItem && toggleItem.Tag is TrackInfo track)
            {
                toggleItem.IsChecked = track.JellyfinIndex == SelectedSubtitleIndex;
            }
        }
    }

    private void OnSubtitleTrackItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem item && item.Tag is TrackInfo track)
        {
            SelectSubtitleTrackCommand?.Execute(track);
        }
    }

    #endregion

    #region Settings Flyout

    private void SetupSettingsFlyout()
    {
        if (_settingsButton is null)
        {
            return;
        }

        MenuFlyout flyout = new();

        // Playback Speed submenu
        _playbackSpeedSubItem = new() { Text = "Playback Speed" };
        double[] speeds = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
        foreach (double speed in speeds)
        {
            ToggleMenuFlyoutItem speedItem = new()
            {
                Text = speed == 1.0 ? "Normal" : $"{speed}x",
                Tag = speed,
                IsChecked = speed == PlaybackSpeed
            };
            speedItem.Click += OnPlaybackSpeedItemClicked;
            _playbackSpeedSubItem.Items.Add(speedItem);
        }
        flyout.Items.Add(_playbackSpeedSubItem);

        // Aspect Ratio submenu
        _aspectRatioSubItem = new() { Text = "Aspect Ratio" };
        (string Name, Stretch Stretch)[] aspects =
        [
            ("Fit", Stretch.Uniform),
            ("Fill", Stretch.UniformToFill),
            ("Stretch", Stretch.Fill),
            ("None", Stretch.None)
        ];
        foreach (var (name, stretch) in aspects)
        {
            ToggleMenuFlyoutItem aspectItem = new()
            {
                Text = name,
                Tag = stretch,
                IsChecked = stretch == Stretch.Uniform
            };
            aspectItem.Click += OnAspectRatioItemClicked;
            _aspectRatioSubItem.Items.Add(aspectItem);
        }
        flyout.Items.Add(_aspectRatioSubItem);

        // Separator before playback info
        flyout.Items.Add(new MenuFlyoutSeparator());

        // Playback Info
        MenuFlyoutItem infoItem = new() { Text = "Playback Info" };
        infoItem.Click += OnPlaybackInfoClicked;
        flyout.Items.Add(infoItem);

        _settingsButton.Flyout = flyout;
    }

    private void UpdatePlaybackSpeedCheckedState()
    {
        if (_playbackSpeedSubItem is null)
        {
            return;
        }

        foreach (MenuFlyoutItemBase subMenuItem in _playbackSpeedSubItem.Items)
        {
            if (subMenuItem is ToggleMenuFlyoutItem toggleItem)
            {
                toggleItem.IsChecked = toggleItem.Tag is double itemSpeed && itemSpeed == PlaybackSpeed;
            }
        }
    }

    private void OnPlaybackSpeedItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem clickedItem && clickedItem.Tag is double speed)
        {
            ChangePlaybackSpeedCommand?.Execute(speed);
        }
    }

    private void UpdateStretchModeCheckedState()
    {
        if (_aspectRatioSubItem is null)
        {
            return;
        }

        foreach (MenuFlyoutItemBase subMenuItem in _aspectRatioSubItem.Items)
        {
            if (subMenuItem is ToggleMenuFlyoutItem toggleItem)
            {
                toggleItem.IsChecked = toggleItem.Tag is Stretch itemStretch && itemStretch == StretchMode;
            }
        }
    }

    private void OnAspectRatioItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem clickedItem && clickedItem.Tag is Stretch stretch)
        {
            ChangeStretchModeCommand?.Execute(stretch);
        }
    }

    private void OnPlaybackInfoClicked(object sender, RoutedEventArgs e)
    {
        ShowPlaybackInfoCommand?.Execute(null);
    }

    #endregion

    private static ToggleMenuFlyoutItem CreateFlyoutMenuItem(TrackInfo track, int selectedIndex)
        => new()
        {
            Text = track.DisplayName,
            Tag = track,
            IsChecked = track.JellyfinIndex == selectedIndex
        };
}
