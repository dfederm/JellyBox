using FinBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FinBox.Views;

internal sealed partial class Video : Page
{
    public Video()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<VideoViewModel>();
    }

    internal VideoViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e) => await ViewModel.PlayVideoAsync((Parameters)e.Parameter, PlayerElement);

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e) => await ViewModel.StopVideoAsync();

    internal sealed record Parameters(BaseItemDto Item, string? MediaSourceId, int? AudioStreamIndex, int? SubtitleStreamIndex);
}
