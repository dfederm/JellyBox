using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;
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

        KeyDown += OnKeyDown;

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

        // Restore focus to previously focused element
        if (_lastFocusedElement is Control control)
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

    /// <summary>
    /// Keyboard and gamepad input handling.
    /// Only handles commands and nav-menu-specific logic.
    /// Directional focus movement is handled by XYFocusKeyboardNavigation in XAML.
    /// </summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // Back gesture - close navigation if open
            case VirtualKey.Back:
            case VirtualKey.GamepadB:
            {
                if (ViewModel.IsMenuOpen)
                {
                    ViewModel.CloseNavigationCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }

            // Toggle navigation menu
            case VirtualKey.GamepadMenu:
            case VirtualKey.GamepadView:
            case VirtualKey.M:
            {
                ViewModel.ToggleNavigationCommand.Execute(null);
                e.Handled = true;
                break;
            }

            // Close navigation menu
            case VirtualKey.Escape:
            {
                if (ViewModel.IsMenuOpen)
                {
                    ViewModel.CloseNavigationCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }

            // Right closes nav menu when open
            case VirtualKey.Right:
            case VirtualKey.GamepadDPadRight:
            case VirtualKey.GamepadLeftThumbstickRight:
            case VirtualKey.NavigationRight:
            {
                if (ViewModel.IsMenuOpen)
                {
                    ViewModel.CloseNavigationCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }

            // Left at edge opens nav menu
            case VirtualKey.Left:
            case VirtualKey.GamepadDPadLeft:
            case VirtualKey.GamepadLeftThumbstickLeft:
            case VirtualKey.NavigationLeft:
            {
                if (!ViewModel.IsMenuOpen && !FocusManager.TryMoveFocus(FocusNavigationDirection.Left))
                {
                    ViewModel.OpenNavigationCommand.Execute(null);
                    e.Handled = true;
                }

                break;
            }
        }
    }

    internal sealed record Parameters(Action DeferredNavigationAction);
}
