using JellyBox.Services;
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

        VirtualKey key = args.VirtualKey;

        // Handle Xbox controller and keyboard shortcuts
        switch (key)
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

            // Controller: A, Keyboard: Space/K - Play/Pause (when transport controls are not focused)
            case VirtualKey.GamepadA:
                if (!hasControlFocus)
                {
                    ViewModel.TogglePlayPause();
                    args.Handled = true;
                }

                break;

            // Controller: Menu - Play/Pause globally
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

            // Controller: X, Keyboard: M - Toggle mute
            case VirtualKey.GamepadX:
            case VirtualKey.M:
                if (!hasControlFocus)
                {
                    ViewModel.ToggleMute();
                    args.Handled = true;
                }
                break;

            default:
                if (!hasControlFocus)
                {
                    if (GamepadInput.IsVolumeUpKey(key))
                    {
                        ViewModel.AdjustVolume(0.1);
                        args.Handled = true;
                    }
                    else if (GamepadInput.IsVolumeDownKey(key))
                    {
                        ViewModel.AdjustVolume(-0.1);
                        args.Handled = true;
                    }
                    else if (GamepadInput.IsSeekBackwardKey(key))
                    {
                        ViewModel.Rewind();
                        args.Handled = true;
                    }
                    else if (GamepadInput.IsSeekForwardKey(key))
                    {
                        ViewModel.FastForward();
                        args.Handled = true;
                    }
                }

                break;
        }

        if (!args.Handled && GamepadInput.IsBackKey(key))
        {
            Frame.GoBack();
            args.Handled = true;
        }
    }

    internal sealed record Parameters(BaseItemDto Item, string? MediaSourceId, int? AudioStreamIndex, int? SubtitleStreamIndex, long StartPositionTicks);
}
