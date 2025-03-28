using FinBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FinBox.Views;

internal sealed partial class ItemDetails : Page
{
    public ItemDetails()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<ItemDetailsViewModel>();
    }

    internal ItemDetailsViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.HandleParameters((Parameters)e.Parameter);

    internal sealed record Parameters(Guid ItemId);
}
