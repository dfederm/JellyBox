using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Home : Page
{
    public Home()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<HomeViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Initialize();

    public HomeViewModel ViewModel { get; }
}
