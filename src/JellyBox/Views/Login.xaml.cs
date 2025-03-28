using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;

namespace JellyBox.Views;

internal sealed partial class Login : Page
{
    public Login()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<LoginViewModel>();
    }

    public LoginViewModel ViewModel { get; }
}