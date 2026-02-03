using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Behaviors;

/// <summary>
/// Manages focus behavior for horizontal lists:
/// - Adjusts scroll position to keep focused items within the TV-safe zone
/// - Prevents focus from escaping at the right edge
/// </summary>
internal sealed class HorizontalScrollOnFocusBehavior : Behavior<ListViewBase>
{
    private ScrollViewer? _scrollViewer;

    /// <summary>
    /// The minimum distance from the viewport edge that items should maintain.
    /// </summary>
    public double SafeZoneMargin { get; set; } = 48;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.GotFocus += OnGotFocus;
        AssociatedObject.LosingFocus += OnLosingFocus;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.GotFocus -= OnGotFocus;
        AssociatedObject.LosingFocus -= OnLosingFocus;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer = AssociatedObject.FindDescendant<ScrollViewer>();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        // Find the focused item container
        FrameworkElement? container =
            (e.OriginalSource as DependencyObject)?.FindAncestor<ListViewItem>() as FrameworkElement
            ?? (e.OriginalSource as DependencyObject)?.FindAncestor<GridViewItem>();

        if (container is not null)
        {
            AdjustScrollPosition(container);
        }
    }

    private void OnLosingFocus(UIElement sender, LosingFocusEventArgs e)
    {
        // Only trap right navigation at the right edge
        if (e.Direction != FocusNavigationDirection.Right
            || e.OldFocusedElement is not FrameworkElement oldElement)
        {
            return;
        }

        // Check if we're at the right edge
        int focusedIndex = AssociatedObject.IndexFromContainer(oldElement);
        if (focusedIndex < 0 || focusedIndex < AssociatedObject.Items.Count - 1)
        {
            return;
        }

        // Cancel if focus is trying to leave this list
        if (e.NewFocusedElement is DependencyObject newElement
            && newElement.FindAncestor<ListViewBase>() != AssociatedObject)
        {
            e.TryCancel();
        }
    }

    private void AdjustScrollPosition(FrameworkElement container)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        var transform = container.TransformToVisual(_scrollViewer);
        var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        double itemLeft = position.X;
        double itemRight = position.X + container.ActualWidth;
        double viewportWidth = _scrollViewer.ViewportWidth;

        if (itemLeft < SafeZoneMargin)
        {
            double newOffset = _scrollViewer.HorizontalOffset - (SafeZoneMargin - itemLeft);
            _scrollViewer.ChangeView(Math.Max(0, newOffset), null, null, false);
        }
        else if (itemRight > viewportWidth - SafeZoneMargin)
        {
            double newOffset = _scrollViewer.HorizontalOffset + (itemRight - (viewportWidth - SafeZoneMargin));
            _scrollViewer.ChangeView(Math.Min(_scrollViewer.ScrollableWidth, newOffset), null, null, false);
        }
    }
}
