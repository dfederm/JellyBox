using Windows.UI.Xaml.Controls.Primitives;

namespace JellyBox.Controls;

internal sealed partial class CustomMediaTransportControls
{
    private bool _isUpdatingVolumeSlider;

    private void UpdatePlayPauseIcon() => _playPauseIcon?.Glyph = IsPlaying ? Glyphs.Pause : Glyphs.Play;

    private void UpdateFavoriteIcon() => _favoriteIcon?.Glyph = IsFavorite ? Glyphs.HeartFilled : Glyphs.HeartOutline;

    private void UpdateEndsAtText() => _endsAtTextBlock?.Text = EndsAtText;

    private void UpdateVolumeState()
    {
        UpdateVolumeIcon();
        UpdateVolumeSlider();
    }

    private void UpdateVolumeIcon()
    {
        if (_volumeIcon is null)
        {
            return;
        }

        if (IsMuted || Volume == 0)
        {
            _volumeIcon.Glyph = Glyphs.VolumeMute;
        }
        else if (Volume < 0.33)
        {
            _volumeIcon.Glyph = Glyphs.VolumeLow;
        }
        else if (Volume < 0.66)
        {
            _volumeIcon.Glyph = Glyphs.VolumeMedium;
        }
        else
        {
            _volumeIcon.Glyph = Glyphs.VolumeHigh;
        }
    }

    private void UpdateVolumeSlider()
    {
        if (_volumeSlider is null)
        {
            return;
        }

        _isUpdatingVolumeSlider = true;
        _volumeSlider.Value = Volume * 100;
        _isUpdatingVolumeSlider = false;
    }

    private void OnVolumeSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingVolumeSlider)
        {
            return;
        }

        double newVolume = e.NewValue / 100.0;
        ChangeVolumeCommand?.Execute(newVolume);
    }
}
