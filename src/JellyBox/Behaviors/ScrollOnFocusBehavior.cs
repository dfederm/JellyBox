using Microsoft.Xaml.Interactivity;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace JellyBox.Behaviors;

/// <summary>
/// Adjusts scroll position to keep focused items within the TV-safe zone.
/// Supports both horizontal carousels and full grid layouts.
/// </summary>
internal sealed class ScrollOnFocusBehavior : Behavior<ListViewBase>
{
    private ScrollViewer? _scrollViewer;

    /// <summary>
    /// The minimum distance from the viewport edge that items should maintain.
    /// </summary>
    public double SafeZoneMargin { get; set; } = 48;

    /// <summary>
    /// Additional margin at the top of the viewport (e.g., for a title overlay).
    /// Only used when <see cref="EnableVerticalScroll"/> is true.
    /// </summary>
    public double TopOffset { get; set; }

    /// <summary>
    /// Whether to adjust vertical scroll position. Default is false (horizontal only).
    /// </summary>
    public bool EnableVerticalScroll { get; set; }

    /// <summary>
    /// Whether to trap focus at the right edge. Default is true (for carousels).
    /// Set to false for grids that should allow wrap navigation.
    /// </summary>
    public bool TrapFocusAtRightEdge { get; set; } = true;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.GotFocus += OnGotFocus;

        if (TrapFocusAtRightEdge)
        {
            AssociatedObject.LosingFocus += OnLosingFocus;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.GotFocus -= OnGotFocus;

        if (TrapFocusAtRightEdge)
        {
            AssociatedObject.LosingFocus -= OnLosingFocus;
        }
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

        // Find the item container that belongs to this specific ListViewBase
        // Both ListViewItem and GridViewItem inherit from SelectorItem
        var container = (e.OriginalSource as DependencyObject)?.FindAncestor<SelectorItem>();

        // Verify this container belongs to our AssociatedObject, not a nested list
        if (container is not null && AssociatedObject.IndexFromContainer(container) >= 0)
        {
            AdjustScrollPosition(container);
        }
    }

    private void OnLosingFocus(UIElement sender, LosingFocusEventArgs e)
    {
        // Only trap right navigation at the right edge
        if (e.Direction != FocusNavigationDirection.Right
            || e.OldFocusedElement is not DependencyObject oldElement)
        {
            return;
        }

        // Find the container for the element losing focus
        var container = oldElement.FindAncestor<SelectorItem>();
        if (container is null)
        {
            return;
        }

        // Check if we're at the right edge
        int focusedIndex = AssociatedObject.IndexFromContainer(container);
        if (focusedIndex != AssociatedObject.Items.Count - 1)
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
        var position = transform.TransformPoint(new Point(0, 0));

        double? newHorizontalOffset = null;
        double? newVerticalOffset = null;

        // Horizontal adjustment
        double itemLeft = position.X;
        double itemRight = position.X + container.ActualWidth;
        double viewportWidth = _scrollViewer.ViewportWidth;

        if (itemLeft < SafeZoneMargin)
        {
            newHorizontalOffset = Math.Max(0, _scrollViewer.HorizontalOffset - (SafeZoneMargin - itemLeft));
        }
        else if (itemRight > viewportWidth - SafeZoneMargin)
        {
            newHorizontalOffset = Math.Min(
                _scrollViewer.ScrollableWidth,
                _scrollViewer.HorizontalOffset + (itemRight - (viewportWidth - SafeZoneMargin)));
        }

        // Vertical adjustment (only if enabled)
        if (EnableVerticalScroll)
        {
            double topMargin = SafeZoneMargin + TopOffset;
            double itemTop = position.Y;
            double itemBottom = position.Y + container.ActualHeight;
            double viewportHeight = _scrollViewer.ViewportHeight;

            if (itemTop < topMargin)
            {
                newVerticalOffset = Math.Max(0, _scrollViewer.VerticalOffset - (topMargin - itemTop));
            }
            else if (itemBottom > viewportHeight - SafeZoneMargin)
            {
                newVerticalOffset = Math.Min(
                    _scrollViewer.ScrollableHeight,
                    _scrollViewer.VerticalOffset + (itemBottom - (viewportHeight - SafeZoneMargin)));
            }
        }

        // Apply scroll with smooth animation
        if (newHorizontalOffset.HasValue || newVerticalOffset.HasValue)
        {
            _scrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, null, disableAnimation: false);
        }
    }
}
