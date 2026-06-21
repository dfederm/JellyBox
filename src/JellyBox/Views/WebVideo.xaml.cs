using JellyBox.Services;
using JellyBox.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class WebVideo : Page
{
    public WebVideo()
    {
        InitializeComponent();

        ViewModel = new WebVideoViewModel();
    }

    internal WebVideoViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel.HandleParameters((Parameters)e.Parameter);
        Window.Current.CoreWindow.KeyDown += OnCoreWindowKeyDown;
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        Window.Current.CoreWindow.KeyDown -= OnCoreWindowKeyDown;

        // Dispose of the webview to stop the video
        WebView2.Close();
    }

    private void OnCoreWindowKeyDown(CoreWindow sender, KeyEventArgs args)
    {
        if (GamepadInput.IsBackKey(args.VirtualKey))
        {
            Frame.GoBack();
            args.Handled = true;
        }
    }

    internal sealed record Parameters(Uri VideoUri);
}