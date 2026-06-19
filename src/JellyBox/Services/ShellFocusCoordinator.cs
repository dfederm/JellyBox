using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Services;

/// <summary>
/// Coordinates focus between the shell search bar and content pages.
/// Search is only focused when the user explicitly navigates up to the top edge.
/// </summary>
#pragma warning disable CA1812 // Instantiated via dependency injection.
internal sealed class ShellFocusCoordinator
#pragma warning restore CA1812
{
    private AutoSuggestBox? _searchBox;

    public void RegisterSearchBox(AutoSuggestBox searchBox) => _searchBox = searchBox;

    public void OnContentNavigated()
    {
        if (_searchBox is null)
        {
            return;
        }

        // FocusState.Unfocused is invalid for Control.Focus and throws on Xbox.
        _searchBox.IsTabStop = false;
    }

    public bool TryFocusSearch()
    {
        if (_searchBox is null)
        {
            return false;
        }

        _searchBox.IsTabStop = true;
        return _searchBox.Focus(FocusState.Programmatic);
    }

    public void OnSearchDismissed()
    {
        if (_searchBox is not null)
        {
            _searchBox.IsTabStop = false;
        }
    }
}