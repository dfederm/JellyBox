using FinBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;

namespace FinBox.Views;

internal sealed partial class Login : Page
{
    public Login()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<LoginViewModel>();
    }

    public LoginViewModel ViewModel { get; }
}