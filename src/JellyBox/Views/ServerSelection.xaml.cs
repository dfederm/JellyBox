using JellyBox.Services;
using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Views;

internal sealed partial class ServerSelection : Page
{
    public ServerSelection()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<ServerSelectionViewModel>();
        KeyDown += OnKeyDown;
    }

    public ServerSelectionViewModel ViewModel { get; }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (GamepadInput.IsAcceptKey(e.Key)
            && ViewModel.ConnectCommand.CanExecute(null))
        {
            ViewModel.ConnectCommand.Execute(null);
            e.Handled = true;
        }
    }
}
