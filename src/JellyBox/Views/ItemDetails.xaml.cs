using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class ItemDetails : Page
{
    private bool _hasFocusedInitial;
    private FocusNavigationDirection? _lastNavigationDirection;

    public ItemDetails()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<ItemDetailsViewModel>();

        // Populate flyouts when they open
        VersionFlyout.Opening += (s, e) => PopulateVersionFlyout();
        AudioFlyout.Opening += (s, e) => PopulateAudioFlyout();
        SubtitleFlyout.Opening += (s, e) => PopulateSubtitleFlyout();

        // Single handler for all vertical zone transitions
        ContentGrid.LosingFocus += ContentGrid_LosingFocus;
        ContentInfoPanel.LayoutUpdated += ContentInfoPanel_LayoutUpdated;
        ContentInfoPanel.GotFocus += ContentInfoPanel_GotFocus;
    }

    internal ItemDetailsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.HandleParameters((Parameters)e.Parameter);

    internal sealed record Parameters(Guid ItemId);

    #region Initial Focus

    private async void ContentInfoPanel_LayoutUpdated(object? sender, object e)
    {
        if (_hasFocusedInitial)
        {
            return;
        }

        Button? target = FindFirstVisible(PlayButton, TrailerButton, PlayedButton, FavoriteButton, VersionButton, AudioButton, SubtitleButton);
        if (target is null)
        {
            return;
        }

        _hasFocusedInitial = true;
        ContentInfoPanel.LayoutUpdated -= ContentInfoPanel_LayoutUpdated;
        await FocusManager.TryFocusAsync(target, FocusState.Programmatic);
    }

    private void ContentInfoPanel_GotFocus(object sender, RoutedEventArgs e)
    {
        // Scroll to top only when navigating up to action buttons (not when restoring focus)
        if (IsActionButton(e.OriginalSource) && _lastNavigationDirection == FocusNavigationDirection.Up)
        {
            ContentScrollViewer.ChangeView(null, 0, null, disableAnimation: false);
        }

        _lastNavigationDirection = null;
    }

    #endregion

    #region Vertical Zone Navigation

    /// <summary>
    /// Single handler for all vertical (Up/Down) navigation between zones.
    /// Zones: Action Buttons → Stream Selection → Sections.
    /// Within-section navigation is handled by SectionNavigationBehavior.
    /// </summary>
    private void ContentGrid_LosingFocus(UIElement sender, LosingFocusEventArgs e)
    {
        // Trap focus at left/right edges of action buttons so left opens nav menu
        if (e.Direction is FocusNavigationDirection.Left or FocusNavigationDirection.Right
            && IsActionButton(e.OldFocusedElement)
            && !IsActionButton(e.NewFocusedElement))
        {
            e.TryCancel();

            // XY focus consumes the key even when cancelled, so MainPage.OnKeyDown never fires.
            // Open the nav menu directly for left-edge presses.
            if (e.Direction == FocusNavigationDirection.Left)
            {
                ViewModel.RequestOpenMenu();
            }

            return;
        }

        if (e.Direction is not (FocusNavigationDirection.Up or FocusNavigationDirection.Down))
        {
            return;
        }

        _lastNavigationDirection = e.Direction;

        bool oldInSections = e.OldFocusedElement.IsDescendantOf(SectionsControl);
        bool newInSections = e.NewFocusedElement.IsDescendantOf(SectionsControl);

        // Within-section navigation already handled by SectionNavigationBehavior
        if (oldInSections && newInSections)
        {
            return;
        }

        if (e.Direction == FocusNavigationDirection.Down && !oldInSections)
        {
            HandleDownFromContent(e);
        }
        else if (e.Direction == FocusNavigationDirection.Up && oldInSections && !newInSections)
        {
            HandleUpFromSections(e);
        }
    }

    private void HandleDownFromContent(LosingFocusEventArgs e)
    {
        // From action buttons → first stream selection button, or first section
        if (IsActionButton(e.OldFocusedElement))
        {
            Button? streamButton = FindFirstVisible(VersionButton, AudioButton, SubtitleButton);
            if (streamButton is not null)
            {
                e.TrySetNewFocusedElement(streamButton);
                return;
            }

            // No stream selection visible - go to first section
            if (!TryFocusFirstSectionItem(e))
            {
                e.TryCancel();
            }
        }
        // From last visible stream button → first section
        else if (IsLastVisibleStreamButton(e.OldFocusedElement))
        {
            if (!TryFocusFirstSectionItem(e))
            {
                e.TryCancel();
            }
        }
    }

    private void HandleUpFromSections(LosingFocusEventArgs e)
    {
        // Collect all visible content buttons (bottom-most row first, then action buttons)
        List<Button> candidates = [];
        AddIfVisible(candidates, SubtitleButton, AudioButton, VersionButton);

        if (candidates.Count > 0)
        {
            // Stream buttons are stacked vertically — pick the bottom-most (first in list)
            e.TrySetNewFocusedElement(candidates[0]);
            return;
        }

        // Fall back to action buttons (horizontal row — pick closest)
        AddIfVisible(candidates, PlayButton, TrailerButton, PlayedButton, FavoriteButton);

        if (candidates.Count == 0)
        {
            return;
        }

        if (e.OldFocusedElement is FrameworkElement oldElement)
        {
            Button closest = FindClosestHorizontally(oldElement, candidates);
            e.TrySetNewFocusedElement(closest);
        }
        else
        {
            e.TrySetNewFocusedElement(candidates[0]);
        }
    }

    private Button FindClosestHorizontally(FrameworkElement source, List<Button> candidates)
    {
        double sourceCenterX = GetElementCenterX(source);
        Button closest = candidates[0];
        double closestDistance = double.MaxValue;

        foreach (Button button in candidates)
        {
            double distance = Math.Abs(GetElementCenterX(button) - sourceCenterX);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = button;
            }
        }

        return closest;
    }

    private double GetElementCenterX(FrameworkElement element)
    {
        try
        {
            var transform = element.TransformToVisual(ContentGrid);
            var position = transform.TransformPoint(new Point(0, 0));
            return position.X + (element.ActualWidth / 2);
        }
        catch
        {
            return 0;
        }
    }

    private static void AddIfVisible(List<Button> list, params Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            if (button.Visibility is Visibility.Visible)
            {
                list.Add(button);
            }
        }
    }

    #endregion

    #region Helpers

    private bool IsActionButton(object? element)
        => ReferenceEquals(element, PlayButton)
            || ReferenceEquals(element, TrailerButton)
            || ReferenceEquals(element, PlayedButton)
            || ReferenceEquals(element, FavoriteButton);

    private bool IsLastVisibleStreamButton(object? element)
    {
        // Check from bottom to top - the first visible one is the "last" (bottom-most)
        if (SubtitleButton.Visibility is Visibility.Visible)
        {
            return ReferenceEquals(element, SubtitleButton);
        }

        if (AudioButton.Visibility is Visibility.Visible)
        {
            return ReferenceEquals(element, AudioButton);
        }

        if (VersionButton.Visibility is Visibility.Visible)
        {
            return ReferenceEquals(element, VersionButton);
        }

        return false;
    }

    private bool TryFocusFirstSectionItem(LosingFocusEventArgs e)
    {
        ListView? firstListView = SectionsControl.FindFirstDescendant<ListView>(
            lv => lv.Visibility is Visibility.Visible && lv.Items.Count > 0);

        if (firstListView is null)
        {
            return false;
        }

        DependencyObject? firstItem = firstListView.ContainerFromIndex(0);
        if (firstItem is null)
        {
            return false;
        }

        return e.TrySetNewFocusedElement(firstItem);
    }

    private static Button? FindFirstVisible(params Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            if (button.Visibility == Visibility.Visible)
            {
                return button;
            }
        }

        return null;
    }

    #endregion

    #region Flyout Population

    private void PopulateVersionFlyout()
        => PopulateFlyout(
            VersionFlyout,
            ViewModel.SourceContainers,
            s => s.Value?.Name ?? "Unknown",
            s => ViewModel.SelectedSourceContainer = s);

    private void PopulateAudioFlyout()
        => PopulateFlyout(
            AudioFlyout,
            ViewModel.AudioStreams,
            s => s.DisplayText,
            s => ViewModel.SelectedAudioStream = s);

    private void PopulateSubtitleFlyout()
        => PopulateFlyout(
            SubtitleFlyout,
            ViewModel.SubtitleStreams,
            s => s.DisplayText,
            s => ViewModel.SelectedSubtitleStream = s);

    private static void PopulateFlyout<T>(MenuFlyout flyout, IEnumerable<T>? items, Func<T, string> textSelector, Action<T> onSelect)
    {
        flyout.Items.Clear();
        if (items is null)
        {
            return;
        }

        foreach (T item in items)
        {
            var menuItem = new MenuFlyoutItem { Text = textSelector(item) };
            menuItem.Click += (s, e) => onSelect(item);
            flyout.Items.Add(menuItem);
        }
    }

    #endregion
}
