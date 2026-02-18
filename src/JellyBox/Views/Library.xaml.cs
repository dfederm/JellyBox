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

    private void SortListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SortOption sortOption)
        {
            ViewModel.SelectSortOptionCommand.Execute(sortOption);
        }
    }

    internal sealed record Parameters(Guid CollectionItemId, BaseItemKind ItemKind, string Title);
}
