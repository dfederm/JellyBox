using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class LoginViewModel : ObservableObject, IDisposable
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private static readonly TimeSpan QuickConnectPollingInterval = TimeSpan.FromSeconds(5);

    private readonly AppSettings _appSettings;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

    private ThreadPoolTimer? _quickConnectPollingTimer;

    [ObservableProperty]
    public partial bool IsQuickConnectEnabled { get; set; } = false;

    [ObservableProperty]
    public partial bool IsInteractable { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowErrorMessage { get; set; }

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    public LoginViewModel(
        AppSettings appSettings,
        JellyfinSdkSettings sdkClientSettings,
        JellyfinApiClient jellyfinApiClient,
        NavigationManager navigationManager)
    {
        _appSettings = appSettings;
        _sdkClientSettings = sdkClientSettings;
        _jellyfinApiClient = jellyfinApiClient;
        _navigationManager = navigationManager;

        IsInteractable = true;
    }

    public void Dispose()
    {
        _quickConnectPollingTimer?.Cancel();
    }

    public async void Initialize()
    {
        try
        {
            IsQuickConnectEnabled = (await _jellyfinApiClient.QuickConnect.Enabled.GetAsync()).GetValueOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LoginViewModel.Initialize: {ex}");
        }
    }

    partial void OnUserNameChanged(string value) => SignInCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => SignInCommand.NotifyCanExecuteChanged();

    private bool CanSignIn() => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        IsInteractable = false;
        ShowErrorMessage = false;

        try
        {
            if (!CanSignIn())
            {
                UpdateErrorMessage("Username and password are required");
                return;
            }

            Console.WriteLine($"Logging into {_sdkClientSettings.ServerUrl}");

            AuthenticateUserByName request = new()
            {
                Username = UserName,
                Pw = Password,
            };
            AuthenticationResult? authenticationResult = await _jellyfinApiClient.Users.AuthenticateByName.PostAsync(request, cancellationToken: cancellationToken);

            HandleAuthenticationResult(authenticationResult);
        }
        catch (Exception ex)
        {
            // TODO: Need a friendlier message.
            UpdateErrorMessage(ex.Message);
            return;
        }
        finally
        {
            IsInteractable = true;
        }
    }

    [RelayCommand]
    private async Task QuickConnectAsync(CancellationToken cancellationToken)
    {
        IsInteractable = false;
        ShowErrorMessage = false;

        try
        {
            QuickConnectResult? initializeResult = await _jellyfinApiClient.QuickConnect.Initiate.PostAsync(cancellationToken: cancellationToken);
            if (initializeResult is null
                || string.IsNullOrEmpty(initializeResult.Secret)
                || string.IsNullOrEmpty(initializeResult.Code))
            {
                UpdateErrorMessage("Malformed quick connect response");
                return;
            }

            ContentDialog quickConnectDialog = new()
            {
                Title = "Quick Connect",
                Content = $"Enter code {initializeResult.Code} to login",
                CloseButtonText = "Cancel",
            };

            _quickConnectPollingTimer?.Cancel();
            _quickConnectPollingTimer = ThreadPoolTimer.CreatePeriodicTimer(PollQuickConnectAsync, QuickConnectPollingInterval);
            async void PollQuickConnectAsync(ThreadPoolTimer _)
            {
                try
                {
                    QuickConnectResult? connectResult = await _jellyfinApiClient.QuickConnect.Connect.GetAsync(
                        request =>
                        {
                            request.QueryParameters.Secret = initializeResult.Secret;
                        },
                        cancellationToken);
                    if (connectResult is null
                        || !connectResult.Authenticated.GetValueOrDefault())
                    {
                        return;
                    }

                    QuickConnectDto quickConnect = new()
                    {
                        Secret = initializeResult.Secret,
                    };
                    AuthenticationResult? authenticationResult = await _jellyfinApiClient.Users.AuthenticateWithQuickConnect.PostAsync(quickConnect, cancellationToken: cancellationToken);

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal,
                        () =>
                        {
                            if (HandleAuthenticationResult(authenticationResult))
                            {
                                quickConnectDialog.Hide();
                            }
                        });
                }
                catch (Exception ex)
                {
                    // Timer callbacks with async void can crash the app if exceptions propagate.
                    // Log and suppress to prevent app termination.
                    System.Diagnostics.Debug.WriteLine($"Error in PollQuickConnectAsync: {ex}");
                }
            }

            _ = await quickConnectDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // TODO: Need a friendlier message.
            UpdateErrorMessage(ex.Message);
            return;
        }
        finally
        {
            IsInteractable = true;
        }
    }

    [RelayCommand]
    private void ChangeServer()
    {
        _navigationManager.NavigateToServerSelection();
        _navigationManager.ClearHistory();
    }

    private bool HandleAuthenticationResult(AuthenticationResult? authenticationResult)
    {
        string? accessToken = authenticationResult?.AccessToken;
        if (accessToken is null)
        {
            // TODO
            UpdateErrorMessage("Unexpected authentication failure");
            return false;
        }

        // TODO: Save creds separately for each server
        _appSettings.AccessToken = accessToken;

        _sdkClientSettings.SetAccessToken(accessToken);

        Console.WriteLine("Authentication success.");

        _navigationManager.NavigateToHome();

        // After signing in, disallow accidentally coming back here.
        _navigationManager.ClearHistory();

        return true;
    }

    private void UpdateErrorMessage(string message)
    {
        ShowErrorMessage = true;
        ErrorMessage = message;
    }
}
