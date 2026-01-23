using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
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

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.PlayVideo((Parameters)e.Parameter, PlayerElement);

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) => ViewModel.StopVideo();

    internal sealed record Parameters(BaseItemDto Item, string? MediaSourceId, int? AudioStreamIndex, int? SubtitleStreamIndex);
}
