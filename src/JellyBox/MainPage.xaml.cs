using System.ComponentModel;
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
    private bool _ignoreNextQuerySubmitted;
    private bool _suppressSearchTextSync;

    public MainPage()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<MainPageViewModel>();
        ViewModel.IsMenuOpenChanged += OnIsMenuOpenChanged;
        ViewModel.Search.PropertyChanged += OnSearchPropertyChanged;

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
            ViewModel.Search.PropertyChanged -= OnSearchPropertyChanged;
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

    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSearchTextSync
            || e.PropertyName is not nameof(ShellSearchViewModel.Query)
            || SearchBox.Text == ViewModel.Search.Query)
        {
            return;
        }

        SearchBox.Text = ViewModel.Search.Query;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.Search.Query = sender.Text ?? string.Empty;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_ignoreNextQuerySubmitted)
        {
            _ignoreNextQuerySubmitted = false;
            return;
        }

        if (args.ChosenSuggestion is SearchSuggestion suggestion)
        {
            OpenSearchSuggestion(suggestion);
            return;
        }

        ViewModel.Search.SubmitQuery(args.QueryText);
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestion suggestion)
        {
            OpenSearchSuggestion(suggestion);
        }
    }

    private void OpenSearchSuggestion(SearchSuggestion suggestion)
    {
        _ignoreNextQuerySubmitted = true;

        _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            _suppressSearchTextSync = true;
            try
            {
                ViewModel.Search.SelectSuggestion(suggestion);
                SearchBox.Text = suggestion.DisplayText;
            }
            finally
            {
                _suppressSearchTextSync = false;
            }
        });
    }

    internal sealed record Parameters(Action DeferredNavigationAction);
}
