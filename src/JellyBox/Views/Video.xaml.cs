using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Video : Page
{
    public Video()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<VideoViewModel>();
    }

    internal VideoViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel.PlayVideo((Parameters)e.Parameter, PlayerElement, TransportControls);

        // Register for gamepad/keyboard input
        Window.Current.CoreWindow.KeyDown += OnCoreWindowKeyDown;
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        Window.Current.CoreWindow.KeyDown -= OnCoreWindowKeyDown;
        ViewModel.StopVideo();
    }

    private void OnCoreWindowKeyDown(CoreWindow sender, KeyEventArgs args)
    {
        // Don't intercept input when a control has focus (e.g., seek bar, flyout items)
        object? focused = FocusManager.GetFocusedElement();
        bool hasControlFocus = focused is Control and not Page;

        // Handle Xbox controller and keyboard shortcuts
        switch (args.VirtualKey)
        {
            // Controller: LB, Keyboard: J - Rewind 10 seconds
            case VirtualKey.GamepadLeftShoulder:
            case VirtualKey.J:
                ViewModel.Rewind();
                args.Handled = true;
                break;

            // Controller: RB, Keyboard: L - Fast forward 30 seconds
            case VirtualKey.GamepadRightShoulder:
            case VirtualKey.L:
                ViewModel.FastForward();
                args.Handled = true;
                break;

            // Controller: Y, Keyboard: F - Toggle favorite
            case VirtualKey.GamepadY:
            case VirtualKey.F:
                if (!hasControlFocus)
                {
                    _ = ViewModel.ToggleFavoriteAsync();
                    args.Handled = true;
                }
                break;

            // Controller: Menu, Keyboard: Space/K - Play/Pause
            case VirtualKey.GamepadMenu:
                ViewModel.TogglePlayPause();
                args.Handled = true;
                break;

            case VirtualKey.Space:
            case VirtualKey.K:
                if (!hasControlFocus)
                {
                    ViewModel.TogglePlayPause();
                    args.Handled = true;
                }
                break;

            // Keyboard: M - Toggle mute
            case VirtualKey.M:
                if (!hasControlFocus)
                {
                    ViewModel.ToggleMute();
                    args.Handled = true;
                }
                break;

            // Keyboard: Up/Down arrow - Volume control (when not on a control)
            case VirtualKey.Up:
                if (!hasControlFocus)
                {
                    ViewModel.AdjustVolume(0.1);
                    args.Handled = true;
                }
                break;

            case VirtualKey.Down:
                if (!hasControlFocus)
                {
                    ViewModel.AdjustVolume(-0.1);
                    args.Handled = true;
                }
                break;

            // Keyboard: Left/Right arrow - Seek (when not on a control)
            case VirtualKey.Left:
                if (!hasControlFocus)
                {
                    ViewModel.Rewind();
                    args.Handled = true;
                }
                break;

            case VirtualKey.Right:
                if (!hasControlFocus)
                {
                    ViewModel.FastForward();
                    args.Handled = true;
                }
                break;

            // Keyboard: Escape - Go back
            case VirtualKey.Escape:
            case VirtualKey.GamepadB:
                Frame.GoBack();
                args.Handled = true;
                break;
        }
    }

    internal sealed record Parameters(BaseItemDto Item, string? MediaSourceId, int? AudioStreamIndex, int? SubtitleStreamIndex);
}
