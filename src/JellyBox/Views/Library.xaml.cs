using System.ComponentModel;
using JellyBox.ViewModels;
using Jellyfin.Sdk.Generated.Models;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace JellyBox.Views;

internal sealed partial class Library : Page
{
    private bool _filterChromeAttached;

    public Library()
    {
        InitializeComponent();
        ViewModel = AppServices.Instance.ServiceProvider.GetRequiredService<LibraryViewModel>();
        Loaded += OnLoaded;
    }

    internal LibraryViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel.HandleParameters((Parameters)e.Parameter);
        UpdateFilterChrome();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_filterChromeAttached)
        {
            return;
        }

        _filterChromeAttached = true;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateFilterChrome();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.HasActiveFilters))
        {
            UpdateFilterChrome();
        }
    }

    private void UpdateFilterChrome()
    {
        if (!IsLoaded)
        {
            return;
        }

        string resourceKey = ViewModel.HasActiveFilters ? "AccentColor" : "BorderSubtle";
        if (Application.Current.Resources[resourceKey] is Brush brush)
        {
            FilterButton.BorderBrush = brush;
        }

        FilterButton.XYFocusRight = ViewModel.HasActiveFilters ? ClearFiltersButton : FilterButton;
        ClearFiltersButton.XYFocusRight = ClearFiltersButton;
    }

    private void SortListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SortOption sortOption)
        {
            ViewModel.SelectSortOptionCommand.Execute(sortOption);
        }
    }

    internal sealed record Parameters(Guid CollectionItemId, BaseItemKind ItemKind, string Title);
}