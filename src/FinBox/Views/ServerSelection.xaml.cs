using FinBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml.Controls;

namespace FinBox.Views;

internal sealed partial class ServerSelection : Page
{
    public ServerSelection()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<ServerSelectionViewModel>();
    }

    public ServerSelectionViewModel ViewModel { get; }
}
