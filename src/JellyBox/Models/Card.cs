using System.Globalization;
using System.Windows.Input;
using JellyBox.Services;

namespace JellyBox.Models;

internal enum CardShape
{
    Portrait,
    Backdrop,
    Square,
    Banner,
}

internal sealed record Card : INavigable
{
    public required string Name { get; init; }

    public required int ImageWidth { get; init; }

    public required int ImageHeight { get; init; }

    public required JellyfinImage Image { get; init; }

    public required ICommand NavigateCommand { get; init; }

    public bool IsFavorite { get; init; }

    public bool IsPlayed { get; init; }

    public double PlayedPercentage { get; init; }

    public int UnplayedItemCount { get; init; }

    public bool HasProgress => PlayedPercentage > 0 && !IsPlayed;

    public bool ShowPlayedIndicator => IsPlayed && UnplayedItemCount == 0;

    public bool ShowUnplayedCount => UnplayedItemCount > 0;

    public string UnplayedCountText => UnplayedItemCount >= 100 ? "99+" : UnplayedItemCount.ToString(CultureInfo.InvariantCulture);

    public double ProgressWidth => ImageWidth * PlayedPercentage / 100.0;
}
