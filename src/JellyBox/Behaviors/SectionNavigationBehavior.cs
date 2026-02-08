using Microsoft.Xaml.Interactivity;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Behaviors;

/// <summary>
/// Handles vertical navigation between horizontal list rows within a sections container.
/// Maintains horizontal position when moving between rows and handles edge trapping.
/// </summary>
internal sealed class SectionNavigationBehavior : Behavior<ItemsControl>
{
    private ScrollViewer? _scrollViewer;

    /// <summary>
    /// The ScrollViewer to use for bringing items into view.
    /// If not set, attempts to find an ancestor ScrollViewer.
    /// </summary>
    public ScrollViewer? ScrollViewer { get; set; }

    /// <summary>
    /// Whether to trap focus at the top edge (cancel up navigation from first row).
    /// </summary>
    public bool TrapAtTop { get; set; } = true;

    /// <summary>
    /// Whether to trap focus at the bottom edge (cancel down navigation from last row).
    /// </summary>
    public bool TrapAtBottom { get; set; } = true;

    /// <summary>
    /// Margin from viewport edge for scroll adjustments.
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
        _scrollViewer = ScrollViewer ?? AssociatedObject.FindAncestor<ScrollViewer>();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement focusedElement)
        {
            BringIntoViewIfNeeded(focusedElement);
        }
    }

    private void OnLosingFocus(UIElement sender, LosingFocusEventArgs e)
    {
        // Only handle vertical navigation
        if (e.Direction is not (FocusNavigationDirection.Down or FocusNavigationDirection.Up))
        {
            return;
        }

        if (e.OldFocusedElement is not FrameworkElement oldElement)
        {
            return;
        }

        ListView? fromListView = oldElement.FindAncestor<ListView>();
        if (fromListView is null)
        {
            return;
        }

        // Find all visible ListViews with items in visual order
        List<ListView> listViews = AssociatedObject.FindAllDescendants<ListView>();
        listViews.RemoveAll(lv => lv.Visibility != Visibility.Visible || lv.Items.Count == 0);

        int currentIndex = listViews.IndexOf(fromListView);
        if (currentIndex < 0)
        {
            return;
        }

        // Get target ListView
        bool isDown = e.Direction == FocusNavigationDirection.Down;
        int targetIndex = isDown ? currentIndex + 1 : currentIndex - 1;

        if (targetIndex < 0)
        {
            if (TrapAtTop)
            {
                e.TryCancel();
            }

            return;
        }

        if (targetIndex >= listViews.Count)
        {
            if (TrapAtBottom)
            {
                e.TryCancel();
            }

            return;
        }

        ListView targetListView = listViews[targetIndex];

        // Find the item in the target ListView closest to the current horizontal position
        double fromCenterX = GetElementCenterX(oldElement);
        FrameworkElement? closestItem = null;
        double closestDistance = double.MaxValue;

        for (int i = 0; i < targetListView.Items.Count; i++)
        {
            if (targetListView.ContainerFromIndex(i) is FrameworkElement container)
            {
                double distance = Math.Abs(GetElementCenterX(container) - fromCenterX);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestItem = container;
                }
            }
        }

        // If containers aren't realized yet, scroll to first item and try again after layout
        if (closestItem is null)
        {
            e.TryCancel();
            targetListView.ScrollIntoView(targetListView.Items[0]);
            _ = AssociatedObject.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (targetListView.ContainerFromIndex(0) is FrameworkElement container)
                {
                    await FocusManager.TryFocusAsync(container, FocusState.Keyboard);
                    if (FocusManager.GetFocusedElement() is FrameworkElement fe)
                    {
                        BringIntoViewIfNeeded(fe);
                    }
                }
            });
            return;
        }

        // Redirect focus to the closest item
        e.TrySetNewFocusedElement(closestItem);
    }

    private double GetElementCenterX(FrameworkElement element)
    {
        try
        {
            var transform = element.TransformToVisual(AssociatedObject);
            var position = transform.TransformPoint(new Point(0, 0));
            return position.X + (element.ActualWidth / 2);
        }
        catch
        {
            return 0;
        }
    }

    private void BringIntoViewIfNeeded(FrameworkElement element)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        try
        {
            // Use the parent ListView's section (title + list) for scroll calculations
            // so the section title is also visible when scrolling down to a row
            ListView? listView = element.FindAncestor<ListView>();
            FrameworkElement scrollTarget = listView?.FindAncestor<StackPanel>() ?? element;

            var transform = scrollTarget.TransformToVisual(_scrollViewer);
            var position = transform.TransformPoint(new Point(0, 0));

            double viewportHeight = _scrollViewer.ViewportHeight;
            double elementTop = position.Y - scrollTarget.Margin.Top;
            double elementBottom = position.Y + scrollTarget.ActualHeight + scrollTarget.Margin.Bottom;

            if (elementTop < SafeZoneMargin)
            {
                double newOffset = _scrollViewer.VerticalOffset + elementTop - SafeZoneMargin;
                _scrollViewer.ChangeView(null, Math.Max(0, newOffset), null, disableAnimation: false);
            }
            else if (elementBottom > viewportHeight - SafeZoneMargin)
            {
                double newOffset = _scrollViewer.VerticalOffset + (elementBottom - (viewportHeight - SafeZoneMargin));
                _scrollViewer.ChangeView(null, newOffset, null, disableAnimation: false);
            }
        }
        catch
        {
            // Element may have been removed from visual tree
        }
    }
}
