using JellyBox.Views;
using Jellyfin.Sdk.Generated.Models;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace JellyBox.Services;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed class NavigationManager
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    // Fake item id used to identify the home page
    public static readonly Guid HomeId = new Guid("CDF95D47-90C2-4057-B12C-BA81C34F2CB9");

    // Fake item id used to identify the search page
    public static readonly Guid SearchId = new Guid("A4B8C2E1-6F3D-4A91-9B7E-2D5C8F1A3E64");

    /// <summary>
    /// Opens the shell navigation menu.
    /// </summary>
    public Action? OpenNavigationMenu { get; set; }

    /// <summary>
    /// Toggles the shell navigation menu.
    /// </summary>
    public Action? ToggleNavigationMenu { get; set; }

    /// <summary>
    /// Closes the shell navigation menu when open. Returns true if the menu was closed.
    /// </summary>
    public Func<bool>? TryCloseNavigationMenu { get; set; }

    private Frame _appFrame = null!; // TODO

    private Frame? _contentFrame;

    private object? _currentAppParameter;

    private object? _currentContentParameter;

    /// <summary>
    /// Gets the item associated with the current page.
    /// </summary>
    // TODO: This is pretty hacky. Find a better way to integrate the NavigationManager with the NavigationViewItems
    public Guid? CurrentItem { get; private set; }

    public void Initialize(Frame appFrame)
    {
        ArgumentNullException.ThrowIfNull(appFrame);

        if (_appFrame is not null)
        {
            throw new InvalidOperationException("Frame already initialized.");
        }

        _appFrame = appFrame;

        SystemNavigationManager.GetForCurrentView().BackRequested += BackRequested;
        Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += AcceleratorKeyActivated;
        Window.Current.CoreWindow.KeyDown += CoreWindowKeyDown;
        Window.Current.CoreWindow.PointerPressed += PointerPressed;
    }

    public void RegisterContentFrame(Frame contentFrame)
    {
        ArgumentNullException.ThrowIfNull(contentFrame);

        if (_contentFrame is not null)
        {
            throw new InvalidOperationException("Cannot register content frame when one is already registered.");
        }

        _contentFrame = contentFrame;
    }

    public void ClearHistory()
    {
        _contentFrame?.BackStack.Clear();
        _contentFrame?.ForwardStack.Clear();
        _appFrame.BackStack.Clear();
        _appFrame.ForwardStack.Clear();
    }

    public void NavigateToServerSelection() => NavigateAppFrame<ServerSelection>();

    public void NavigateToLogin() => NavigateAppFrame<Login>();

    public void RequestOpenMenu() => OpenNavigationMenu?.Invoke();

    public void NavigateToHome()
    {
        CurrentItem = HomeId;
        NavigateContentFrame<Home>();
    }

    public void NavigateToSearch(string query)
    {
        CurrentItem = SearchId;
        Search.Parameters parameters = new(query);

        if (_contentFrame is not null
            && _contentFrame.CurrentSourcePageType == typeof(Search)
            && Equals(_currentContentParameter, parameters))
        {
            if (_contentFrame.Content is Search searchPage)
            {
                searchPage.ViewModel.HandleParameters(parameters);
            }

            return;
        }

        NavigateContentFrame<Search>(parameters);
    }

    public void NavigateToItem(Guid itemId)
    {
        CurrentItem = itemId;
        NavigateContentFrame<ItemDetails>(new ItemDetails.Parameters(itemId));
    }

    public void NavigateToItem(BaseItemDto item)
    {
        Guid itemId = item.Id!.Value;
        if (item.Type == BaseItemDto_Type.CollectionFolder)
        {
            switch (item.CollectionType)
            {
                case BaseItemDto_CollectionType.Movies:
                {
                    CurrentItem = itemId;
                    NavigateContentFrame<Library>(new Library.Parameters(itemId, BaseItemKind.Movie, item.Name ?? "Movies"));
                    return;
                }
                case BaseItemDto_CollectionType.Tvshows:
                {
                    CurrentItem = itemId;
                    NavigateContentFrame<Library>(new Library.Parameters(itemId, BaseItemKind.Series, item.Name ?? "TV Shows"));
                    return;
                }
            }

            // TODO: Some kind of error message. Or genericize the collection view.
        }
        else
        {
            CurrentItem = itemId;
            NavigateContentFrame<ItemDetails>(new ItemDetails.Parameters(itemId));
        }
    }

    public void NavigateToPerson(BaseItemPerson person)
    {
        Guid itemId = person.Id!.Value;
        CurrentItem = itemId;
        NavigateContentFrame<ItemDetails>(new ItemDetails.Parameters(itemId));
    }

    public void NavigateToVideo(BaseItemDto item, string? mediaSourceId, int? audioStreamIndex, int? subtitleStreamIndex, long startPositionTicks)
    {
        CurrentItem = item.Id;
        NavigateAppFrame<Video>(new Video.Parameters(item, mediaSourceId, audioStreamIndex, subtitleStreamIndex, startPositionTicks));
    }

    public void NavigateToWebVideo(Uri videoUri)
    {
        CurrentItem = null;
        NavigateAppFrame<WebVideo>(new WebVideo.Parameters(videoUri));
    }

    private void NavigateAppFrame<TPage>(object? parameter = null)
        where TPage : Page
    {
        CurrentItem = null;
        _contentFrame = null;
        _currentContentParameter = null;
        NavigateFrame<TPage>(_appFrame, ref _currentAppParameter, parameter);
    }

    private void NavigateContentFrame<TPage>(object? parameter = null)
    {
        if (_contentFrame is null)
        {
            MainPage.Parameters mainPageParameters = new(DeferredNavigationAction: () => NavigateFrame<TPage>(_contentFrame!, ref _currentContentParameter, parameter));
            NavigateFrame<MainPage>(_appFrame, ref _currentAppParameter, mainPageParameters);
        }
        else
        {
            NavigateFrame<TPage>(_contentFrame, ref _currentContentParameter, parameter);
        }
    }

    private static void NavigateFrame<TPage>(Frame frame, ref object? currentParameter, object? parameter = null)
    {
        // Only navigate if the selected page isn't currently loaded with the same parameter.
        Type pageType = typeof(TPage);
        if (frame.CurrentSourcePageType == pageType && Equals(currentParameter, parameter))
        {
            return;
        }

        currentParameter = parameter;
        frame.Navigate(pageType, parameter);
    }

    /// <summary>
    /// Indicates whether or not a back navigation can occur.
    /// </summary>
    /// <returns>True if a back navigation can occur else false.</returns>
    public bool CanGoBack() => (_contentFrame is not null && _contentFrame.CanGoBack) || _appFrame.CanGoBack;

    /// <summary>
    /// Indicates whether or not a forward navigation can occur.
    /// </summary>
    /// <returns>True if a forward navigation can occur else false.</returns>
    public bool CanGoForward() => (_contentFrame is not null && _contentFrame.CanGoForward) || _appFrame.CanGoForward;

    /// <summary>
    /// Navigates back one page.
    /// </summary>
    /// <returns>True if a back navigation occurred else false.</returns>
    public bool GoBack()
    {
        if (_contentFrame is not null && _contentFrame.CanGoBack)
        {
            _contentFrame.GoBack();
            return true;
        }

        if (_appFrame.CanGoBack)
        {
            _appFrame.GoBack();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Navigates forward one page.
    /// </summary>
    /// <returns>True if a forward navigation occurred else false.</returns>
    public bool GoForward()
    {
        if (_contentFrame is not null && _contentFrame.CanGoForward)
        {
            _contentFrame.GoForward();
            return true;
        }

        if (_appFrame.CanGoForward)
        {
            _appFrame.GoForward();
            return true;
        }

        return false;
    }

    private void BackRequested(object? sender, BackRequestedEventArgs e) => e.Handled = GoBack();

    /// <summary>
    /// Invoked on every keystroke, including system keys such as Alt key combinations, when
    /// this page is active and occupies the entire window.  Used to detect keyboard navigation
    /// between pages even when the page itself doesn't have focus.
    /// </summary>
    /// <param name="sender">Instance that triggered the event.</param>
    /// <param name="e">Event data describing the conditions that led to the event.</param>
    private void AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs e)
    {
        if (e.EventType is not CoreAcceleratorKeyEventType.SystemKeyDown
            and not CoreAcceleratorKeyEventType.KeyDown)
        {
            return;
        }

        if (GamepadInput.IsTextInputFocused())
        {
            return;
        }

        CoreWindow coreWindow = Window.Current.CoreWindow;
        CoreVirtualKeyStates downState = CoreVirtualKeyStates.Down;
        bool menuKey = (coreWindow.GetKeyState(VirtualKey.Menu) & downState) == downState;
        bool controlKey = (coreWindow.GetKeyState(VirtualKey.Control) & downState) == downState;
        bool shiftKey = (coreWindow.GetKeyState(VirtualKey.Shift) & downState) == downState;
        bool noModifiers = !menuKey && !controlKey && !shiftKey;
        bool onlyAlt = menuKey && !controlKey && !shiftKey;
        VirtualKey virtualKey = e.VirtualKey;

        if (GamepadInput.IsBackKey(virtualKey) && noModifiers)
        {
            if (TryCloseNavigationMenu?.Invoke() == true)
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = GoBack();
            }
        }
        else if (virtualKey is VirtualKey.Left && onlyAlt)
        {
            e.Handled = GoBack();
        }
        else if ((virtualKey is VirtualKey.GoForward && noModifiers)
            || (virtualKey is VirtualKey.Right && onlyAlt))
        {
            e.Handled = GoForward();
        }
    }

    /// <summary>
    /// Global gamepad and keyboard shortcuts for the main app shell (home, library, details).
    /// Full-screen pages such as video playback register their own handlers.
    /// </summary>
    private void CoreWindowKeyDown(CoreWindow sender, KeyEventArgs e)
    {
        // Shell shortcuts only apply inside MainPage content (login/video use app-frame pages).
        if (_contentFrame is null || GamepadInput.IsTextInputFocused())
        {
            return;
        }

        VirtualKey key = e.VirtualKey;

        if (GamepadInput.IsMenuToggleKey(key))
        {
            ToggleNavigationMenu?.Invoke();
            e.Handled = true;
            return;
        }

        if (GamepadInput.IsBackKey(key) && TryCloseNavigationMenu?.Invoke() == true)
        {
            e.Handled = true;
            return;
        }

        if (GamepadInput.IsNavigateRightKey(key) && TryCloseNavigationMenu?.Invoke() == true)
        {
            e.Handled = true;
            return;
        }

        if (GamepadInput.IsNavigateLeftKey(key) && !FocusManager.TryMoveFocus(FocusNavigationDirection.Left))
        {
            OpenNavigationMenu?.Invoke();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Invoked on every mouse click, touch screen tap, or equivalent interaction when this
    /// page is active and occupies the entire window.  Used to detect browser-style next and
    /// previous mouse button clicks to navigate between pages.
    /// </summary>
    /// <param name="sender">Instance that triggered the event.</param>
    /// <param name="e">Event data describing the conditions that led to the event.</param>
    private void PointerPressed(CoreWindow sender, PointerEventArgs e)
    {
        PointerPointProperties properties = e.CurrentPoint.Properties;

        // Ignore button chords with the left, right, and middle buttons
        if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed || properties.IsMiddleButtonPressed)
        {
            return;
        }

        // If back or forward are pressed (but not both) navigate appropriately
        bool backPressed = properties.IsXButton1Pressed;
        bool forwardPressed = properties.IsXButton2Pressed;
        if (backPressed ^ forwardPressed)
        {
            if (backPressed)
            {
                e.Handled = GoBack();
            }

            if (forwardPressed)
            {
                e.Handled = GoForward();
            }
        }
    }
}