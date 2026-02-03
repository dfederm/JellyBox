using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Home : Page
{
    private bool _hasFocusedFirstItem;

    public Home()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<HomeViewModel>();
        SectionsControl.LayoutUpdated += SectionsControl_LayoutUpdated;
        SectionsControl.LosingFocus += SectionsControl_LosingFocus;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Initialize();

    public HomeViewModel ViewModel { get; }

    private async void SectionsControl_LayoutUpdated(object? sender, object e)
    {
        if (_hasFocusedFirstItem || ViewModel.Sections is not { Count: > 0 })
        {
            return;
        }

        ListView? firstListView = SectionsControl.FindDescendant<ListView>();
        if (firstListView is null || firstListView.Items.Count == 0)
        {
            return;
        }

        DependencyObject? firstItem = firstListView.ContainerFromIndex(0);
        if (firstItem is null)
        {
            return;
        }

        _hasFocusedFirstItem = true;
        await FocusManager.TryFocusAsync(firstItem, FocusState.Programmatic);
    }

    private void SectionsControl_GotFocus(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement focusedElement)
        {
            BringIntoViewIfNeeded(focusedElement);
        }
    }

    private void SectionsControl_LosingFocus(UIElement sender, LosingFocusEventArgs e)
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
        List<ListView> listViews = SectionsControl.FindAllDescendants<ListView>();
        listViews.RemoveAll(lv => lv.Visibility != Visibility.Visible || lv.Items.Count == 0);

        int currentIndex = listViews.IndexOf(fromListView);
        if (currentIndex < 0)
        {
            return;
        }

        // Get target ListView
        bool isDown = e.Direction == FocusNavigationDirection.Down;
        int targetIndex = isDown ? currentIndex + 1 : currentIndex - 1;
        if (targetIndex < 0 || targetIndex >= listViews.Count)
        {
            // At edge - cancel navigation to stay on current item
            e.TryCancel();
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
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (targetListView.ContainerFromIndex(0) is { } container)
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
        if (e.TrySetNewFocusedElement(closestItem))
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => BringIntoViewIfNeeded(closestItem));
        }
    }

    private double GetElementCenterX(FrameworkElement element)
    {
        try
        {
            var transform = element.TransformToVisual(this);
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
        var transform = element.TransformToVisual(ContentScrollViewer);
        var position = transform.TransformPoint(new Point(0, 0));

        double viewportTop = ContentScrollViewer.VerticalOffset;
        double viewportBottom = viewportTop + ContentScrollViewer.ViewportHeight;

        double elementTop = position.Y + ContentScrollViewer.VerticalOffset;
        double elementBottom = elementTop + element.ActualHeight;

        if (elementTop < viewportTop)
        {
            ContentScrollViewer.ChangeView(null, elementTop - 20, null, false);
        }
        else if (elementBottom > viewportBottom)
        {
            ContentScrollViewer.ChangeView(null, elementBottom - ContentScrollViewer.ViewportHeight + 20, null, false);
        }
    }
}
