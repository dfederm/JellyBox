using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Behaviors;

/// <summary>
/// Automatically focuses the first item in a list once items are loaded and containers are realized.
/// </summary>
internal sealed class FocusFirstItemBehavior : Behavior<ListViewBase>
{
    private bool _hasFocused;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
    }

    private async void OnLayoutUpdated(object? sender, object e)
    {
        if (_hasFocused || AssociatedObject.Items.Count == 0)
        {
            return;
        }

        DependencyObject? firstItem = AssociatedObject.ContainerFromIndex(0);
        if (firstItem is null)
        {
            return;
        }

        _hasFocused = true;
        await FocusManager.TryFocusAsync(firstItem, FocusState.Programmatic);
    }
}
