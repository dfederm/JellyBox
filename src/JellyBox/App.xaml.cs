using JellyBox.Services;
using JellyBox.Views;
using Jellyfin.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyBox;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
#pragma warning disable CA1515 // Consider making public types internal
sealed partial class App : Application
#pragma warning restore CA1515 // Consider making public types internal
{
    private readonly AppSettings _appSettings;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly NavigationManager _navigationManager;
    private readonly DeviceProfileManager _deviceProfileManager;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        ConfigureWebView2();

        _appSettings = AppServices.Instance.ServiceProvider.GetRequiredService<AppSettings>();
        _sdkClientSettings = AppServices.Instance.ServiceProvider.GetRequiredService<JellyfinSdkSettings>();
        _navigationManager = AppServices.Instance.ServiceProvider.GetRequiredService<NavigationManager>();
        _deviceProfileManager = AppServices.Instance.ServiceProvider.GetRequiredService<DeviceProfileManager>();

        InitializeComponent();

        Current.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

        Suspending += OnSuspending;
    }

    /// <inheritdoc/>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Frame? rootFrame = Window.Current.Content as Frame;

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active.
        if (rootFrame == null)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;

            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
            {
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                ApplicationViewScaling.TrySetDisableLayoutScaling(true);
            }
            else
            {
                // Xbox always renders at 1920 x 1080, so emulate that for a consistency when testing on Windows.
                ApplicationView.PreferredLaunchViewSize = new Size(1920, 1080);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            }

            if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
            {
                // TODO: Load state from previously suspended application
            }

            // Place the frame in the current Window
            Window.Current.Content = rootFrame;
        }

        if (!args.PrelaunchActivated)
        {
            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (_appSettings.ServerUrl is null)
                {
                    rootFrame.Navigate(typeof(ServerSelection), args.Arguments);
                }
                else
                {
                    // TODO: Validate the server is still reachable
                    _sdkClientSettings.SetServerUrl(_appSettings.ServerUrl);

                    if (_appSettings.AccessToken is null)
                    {
                        rootFrame.Navigate(typeof(Login), args.Arguments);
                    }
                    else
                    {
                        // TODO: Validate the access token is still valid
                        _sdkClientSettings.SetAccessToken(_appSettings.AccessToken);

                        rootFrame.Navigate(typeof(MainPage), args.Arguments);
                    }
                }
            }

            _navigationManager.Initialize(rootFrame);

            // Initialize the device profile asynchronously. If this fails, playback features may not work correctly.
            _ = InitializeDeviceProfileAsync();

            // Ensure the current window is active
            Window.Current.Activate();
        }
    }

    private async Task InitializeDeviceProfileAsync()
    {
        try
        {
            await _deviceProfileManager.InitializeAsync();
        }
        catch (Exception ex)
        {
            // Log initialization failure but don't crash the app - playback will handle missing profile gracefully
            System.Diagnostics.Debug.WriteLine($"Failed to initialize device profile: {ex}");
        }
    }

    /// <summary>
    /// Invoked when Navigation to a certain page fails.
    /// </summary>
    /// <param name="sender">The Frame which failed navigation.</param>
    /// <param name="e">Details about the navigation failure.</param>
    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
#pragma warning disable CA2201 // Do not raise reserved exception types
        throw new Exception($"Failed to load page '{e.SourcePageType.FullName}'.");
#pragma warning restore CA2201 // Do not raise reserved exception types
    }

    /// <summary>
    /// Invoked when application execution is being suspended. Application state is saved
    /// without knowing whether the application will be terminated or resumed with the contents
    /// of memory still intact.
    /// </summary>
    /// <param name="sender">The source of the suspend request.</param>
    /// <param name="e">Details about the suspend request.</param>
    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();

        // TODO: Save application state and stop any background activity
        deferral.Complete();
    }

    private static void ConfigureWebView2()
    {
        // Allow video to auto-play
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");

        // Avoid flashbang before loading the uri by setting the default background color to be transparent.
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00FFFFFF");
    }
}
