using JellyBox.Services;
using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox;

internal sealed partial class MainPage : Page
{
    private readonly NavigationManager _navigationManager;

    public MainPage()
    {
        InitializeComponent();

        _navigationManager = AppServices.Instance.ServiceProvider.GetRequiredService<NavigationManager>();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<MainPageViewModel>();

        // Cache the page state so the ContentFrame's BackStack can be preserved
        NavigationCacheMode = NavigationCacheMode.Required;

        KeyDown += OnKeyDown;

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
        _navigationManager.RegisterContentFrame(ContentFrame);

        ViewModel.HandleParameters(e.Parameter as Parameters, ContentFrame);

        base.OnNavigatedTo(e);
    }

    private void ContentFrameNavigated(object sender, NavigationEventArgs e)
    {
        _ = Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            () =>
            {
                ViewModel.IsMenuOpen = false;
                ViewModel.UpdateSelectedMenuItem();
            });
    }

    /// <summary>
    /// Default keyboard focus movement for any unhandled keyboarding
    /// </summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        FocusNavigationDirection direction = FocusNavigationDirection.None;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
            case Windows.System.VirtualKey.GamepadDPadLeft:
            case Windows.System.VirtualKey.GamepadLeftThumbstickLeft:
            case Windows.System.VirtualKey.NavigationLeft:
            {
                direction = FocusNavigationDirection.Left;
                break;
            }
            case Windows.System.VirtualKey.Right:
            case Windows.System.VirtualKey.GamepadDPadRight:
            case Windows.System.VirtualKey.GamepadLeftThumbstickRight:
            case Windows.System.VirtualKey.NavigationRight:
            {
                direction = FocusNavigationDirection.Right;
                break;
            }
            case Windows.System.VirtualKey.Up:
            case Windows.System.VirtualKey.GamepadDPadUp:
            case Windows.System.VirtualKey.GamepadLeftThumbstickUp:
            case Windows.System.VirtualKey.NavigationUp:
            {
                direction = FocusNavigationDirection.Up;
                break;
            }
            case Windows.System.VirtualKey.Down:
            case Windows.System.VirtualKey.GamepadDPadDown:
            case Windows.System.VirtualKey.GamepadLeftThumbstickDown:
            case Windows.System.VirtualKey.NavigationDown:
            {
                direction = FocusNavigationDirection.Down;
                break;
            }
        }

        if (direction != FocusNavigationDirection.None)
        {
            if (FocusManager.TryMoveFocus(direction))
            {
                e.Handled = true;
            }
        }
    }

    internal sealed record Parameters(Action DeferredNavigationAction);
}
