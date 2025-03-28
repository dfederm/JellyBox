using FinBox.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FinBox.Views;

internal sealed partial class WebVideo : Page
{
    public WebVideo()
    {
        InitializeComponent();

        ViewModel = new WebVideoViewModel();
    }

    internal WebVideoViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.HandleParameters((Parameters)e.Parameter);

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        // Dispose of the webview to stop the video
        WebView2.Close();
    }

    internal sealed record Parameters(Uri VideoUri);
}
