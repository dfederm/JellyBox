using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Windows.UI.Xaml.Controls;

namespace JellyBox.ViewModels;

internal sealed partial class MainPageViewModel : ObservableObject
{
    private readonly AppSettings _appSettings;
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    [ObservableProperty]
    public partial bool IsMenuOpen { get; set; }

    partial void OnIsMenuOpenChanged(bool value) => IsMenuOpenChanged?.Invoke(value);

    public event Action<bool>? IsMenuOpenChanged;

    [ObservableProperty]
    public partial ObservableCollection<NavigationViewItemBase>? NavigationItems { get; set; }

    public MainPageViewModel(
        AppSettings appSettings,
        JellyfinApiClient jellyfinApiClient,
        NavigationManager navigationManager)
    {
        _appSettings = appSettings;
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;

        _ = InitializeNavigationItemsAsync();
    }

    [RelayCommand]
    private void OpenNavigation() => IsMenuOpen = true;

    [RelayCommand]
    private void CloseNavigation() => IsMenuOpen = false;

    [RelayCommand]
    private void ToggleNavigation() => IsMenuOpen = !IsMenuOpen;

    public void HandleParameters(MainPage.Parameters? parameters, Frame contentFrame)
    {
        _navigationManager.RegisterContentFrame(contentFrame);

        if (parameters is not null)
        {
            parameters.DeferredNavigationAction();
        }
        else if (contentFrame.CurrentSourcePageType is null)
        {
            // Default to home
            _navigationManager.NavigateToHome();
        }
    }

    public void NavigationItemSelected(NavigationView _, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is NavigationViewItemContext context)
        {
            context.NavigateAction();
            IsMenuOpen = false;
        }
    }

    public void UpdateSelectedMenuItem()
    {
        Guid? currentItem = _navigationManager.CurrentItem;
        if (!currentItem.HasValue)
        {
            return;
        }

        if (NavigationItems is not null)
        {
            foreach (NavigationViewItemBase item in NavigationItems)
            {
                if (item.Tag is NavigationViewItemContext context)
                {
                    if (context.ItemId == currentItem)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }
        }
    }

    private async Task InitializeNavigationItemsAsync()
    {
        List<NavigationViewItemBase> navigationItems = new();

        navigationItems.Add(new NavigationViewItem
        {
            Content = "Home",
            Icon = CreateFontIcon(Glyphs.Home),
            Tag = new NavigationViewItemContext(() => _navigationManager.NavigateToHome(), ItemId: NavigationManager.HomeId),
        });

        BaseItemDtoQueryResult? result = await _jellyfinApiClient.UserViews.GetAsync();
        if (result?.Items is not null)
        {
            if (result.Items.Count > 0)
            {
                navigationItems.Add(new NavigationViewItemHeader { Content = "Media" });
            }

            foreach (BaseItemDto item in result.Items)
            {
                if (!item.Id.HasValue)
                {
                    continue;
                }

                Guid itemId = item.Id.Value;

                navigationItems.Add(new NavigationViewItem
                {
                    Content = item.Name,
                    Icon = CreateFontIcon(Glyphs.Library),
                    Tag = new NavigationViewItemContext(() => _navigationManager.NavigateToItem(item), itemId),
                });
            }
        }

        navigationItems.Add(new NavigationViewItemHeader { Content = "User" });

        navigationItems.Add(new NavigationViewItem
        {
            Content = "Select Server",
            Icon = CreateFontIcon(Glyphs.Switch),
            Tag = new NavigationViewItemContext(() => _navigationManager.NavigateToServerSelection(), ItemId: null),
        });

        navigationItems.Add(new NavigationViewItem
        {
            Content = "Sign Out",
            Icon = CreateFontIcon(Glyphs.SignOut),
            Tag = new NavigationViewItemContext(
                () =>
                {
                    _appSettings.AccessToken = null;
                    _navigationManager.NavigateToLogin();

                    // After signing out, disallow going back to a logged-in page.
                    _navigationManager.ClearHistory();
                },
                ItemId: null),
        });

        NavigationItems = new ObservableCollection<NavigationViewItemBase>(navigationItems);
    }

    private static FontIcon CreateFontIcon(string glyph)
        => new()
        {
            Glyph = glyph,
            FontSize = 24
        };

    internal sealed record NavigationViewItemContext(Action NavigateAction, Guid? ItemId);
}