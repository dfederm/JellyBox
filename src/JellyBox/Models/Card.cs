using JellyBox.Services;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Models;

internal enum CardShape
{
    Portrait,
    Backdrop,
    Square,
    Banner,
}

internal sealed record Card
{
    public required BaseItemDto Item { get; init; }

    public required string Name { get; init; }

    public required int ImageWidth { get; init; }

    public required int ImageHeight { get; init; }

    public required JellyfinImage Image { get; init; }
}
