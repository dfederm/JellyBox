using JellyBox.Models;
using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox;

internal sealed partial class MainPage : Page
{
    private FrameworkElement? _lastFocusedElement;

    public MainPage()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<MainPageViewModel>();
        ViewModel.IsMenuOpenChanged += OnIsMenuOpenChanged;

        // Cache the page state so the ContentFrame's BackStack can be preserved
        NavigationCacheMode = NavigationCacheMode.Required;

        SlideInAnimation.Completed += SlideInCompleted;

        Loaded += (sender, e) =>
        {
            ContentFrame.Navigated += ContentFrameNavigated;
            ViewModel.UpdateSelectedMenuItem();
        };

        Unloaded += (sender, e) =>
        {
            ContentFrame.Navigated -= ContentFrameNavigated;
        };
    }

    internal MainPageViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel.HandleParameters(e.Parameter as Parameters, ContentFrame);

        base.OnNavigatedTo(e);
    }

    private void OnIsMenuOpenChanged(bool isOpen)
    {
        if (isOpen)
        {
            OpenNavigationOverlay();
        }
        else
        {
            CloseNavigationOverlay();
        }
    }

    private void OpenNavigationOverlay()
    {
        // Store current focus to restore later
        _lastFocusedElement = FocusManager.GetFocusedElement() as FrameworkElement;

        NavigationOverlay.Visibility = Visibility.Visible;
        SlideInAnimation.Begin();
    }

    private void CloseNavigationOverlay()
    {
        SlideOutAnimation.Begin();
    }

    private void SlideInCompleted(object? sender, object e)
    {
        // Focus the navigation panel after animation completes
        if (ViewModel.IsMenuOpen)
        {
            _ = Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => NavigationPanel.Focus(FocusState.Keyboard));
        }
    }

    private void SlideOutCompleted(object sender, object e)
    {
        NavigationOverlay.Visibility = Visibility.Collapsed;

        // Restore focus to previously focused element (if it's still in the visual tree)
        if (_lastFocusedElement is Control control && control.IsLoaded)
        {
            control.Focus(FocusState.Keyboard);
        }

        _lastFocusedElement = null;
    }

    private void ContentFrameNavigated(object sender, NavigationEventArgs e)
    {
        _ = Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
            () =>
            {
                ViewModel.CloseNavigationCommand.Execute(null);
                ViewModel.UpdateSelectedMenuItem();
            });
    }

    private void CloseNavigation(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.CloseNavigationCommand.Execute(null);
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestion suggestion)
        {
            ViewModel.Search.SelectSuggestion(suggestion);
            return;
        }

        ViewModel.Search.SubmitQuery(args.QueryText);
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestion suggestion)
        {
            ViewModel.Search.SelectSuggestion(suggestion);
        }
    }

    internal sealed record Parameters(Action DeferredNavigationAction);
}
