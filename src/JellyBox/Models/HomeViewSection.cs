using System.Windows.Input;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.Models;

internal sealed class HomeViewSection
{
    public required string Name { get; set; }

    public required IReadOnlyList<BaseItemDto>? Items { get; set; }

    public required ICommand NavigateToItemCommand { get; set; }
}