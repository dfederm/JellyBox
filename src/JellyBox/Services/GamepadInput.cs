using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Services;

/// <summary>
/// Shared Xbox / gamepad virtual-key helpers for consistent controller input across the app.
/// </summary>
internal static class GamepadInput
{
    public static bool IsTextInputFocused()
        => FocusManager.GetFocusedElement() is TextBox or PasswordBox;

    public static bool IsAcceptKey(VirtualKey key)
        => key is VirtualKey.Enter or VirtualKey.GamepadA or VirtualKey.Space;

    public static bool IsBackKey(VirtualKey key)
        => key is VirtualKey.Back or VirtualKey.Escape or VirtualKey.GamepadB or VirtualKey.GoBack;

    public static bool IsMenuToggleKey(VirtualKey key)
        => key is VirtualKey.GamepadMenu or VirtualKey.GamepadView or VirtualKey.M;

    public static bool IsNavigateLeftKey(VirtualKey key)
        => key is VirtualKey.Left
            or VirtualKey.GamepadDPadLeft
            or VirtualKey.GamepadLeftThumbstickLeft
            or VirtualKey.NavigationLeft;

    public static bool IsNavigateRightKey(VirtualKey key)
        => key is VirtualKey.Right
            or VirtualKey.GamepadDPadRight
            or VirtualKey.GamepadLeftThumbstickRight
            or VirtualKey.NavigationRight;

    public static bool IsVolumeUpKey(VirtualKey key)
        => key is VirtualKey.Up
            or VirtualKey.GamepadDPadUp
            or VirtualKey.GamepadLeftThumbstickUp;

    public static bool IsVolumeDownKey(VirtualKey key)
        => key is VirtualKey.Down
            or VirtualKey.GamepadDPadDown
            or VirtualKey.GamepadLeftThumbstickDown;

    public static bool IsSeekBackwardKey(VirtualKey key)
        => key is VirtualKey.Left
            or VirtualKey.GamepadDPadLeft
            or VirtualKey.GamepadLeftThumbstickLeft;

    public static bool IsSeekForwardKey(VirtualKey key)
        => key is VirtualKey.Right
            or VirtualKey.GamepadDPadRight
            or VirtualKey.GamepadLeftThumbstickRight;
}