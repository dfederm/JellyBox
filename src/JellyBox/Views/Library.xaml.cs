using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Library : Page
{
    public Library()
    {
        InitializeComponent();
        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<LibraryViewModel>();
    }

    internal LibraryViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.HandleParameters((Parameters)e.Parameter);

    internal sealed record Parameters(Guid CollectionItemId, BaseItemKind ItemKind, string Title);
}
