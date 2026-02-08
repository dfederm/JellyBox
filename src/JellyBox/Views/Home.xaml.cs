using JellyBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Home : Page
{
    private bool _hasFocusedFirstItem;

    public Home()
    {
        InitializeComponent();

        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<HomeViewModel>();
        SectionsControl.LayoutUpdated += SectionsControl_LayoutUpdated;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ViewModel.Initialize();

    public HomeViewModel ViewModel { get; }

    private async void SectionsControl_LayoutUpdated(object? sender, object e)
    {
        if (_hasFocusedFirstItem || ViewModel.Sections is not { Count: > 0 })
        {
            return;
        }

        ListView? firstListView = SectionsControl.FindDescendant<ListView>();
        if (firstListView is null || firstListView.Items.Count == 0)
        {
            return;
        }

        DependencyObject? firstItem = firstListView.ContainerFromIndex(0);
        if (firstItem is null)
        {
            return;
        }

        _hasFocusedFirstItem = true;
        await FocusManager.TryFocusAsync(firstItem, FocusState.Programmatic);
    }
}
