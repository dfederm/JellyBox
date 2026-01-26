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
}
