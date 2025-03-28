using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyBox.Services;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;

namespace JellyBox.ViewModels;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.
internal sealed partial class LoginViewModel : ObservableObject
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
{
    private readonly AppSettings _appSettings;
    private readonly JellyfinSdkSettings _sdkClientSettings;
    private readonly JellyfinApiClient _jellyfinApiClient;
    private readonly NavigationManager _navigationManager;

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

            string? accessToken = authenticationResult?.AccessToken;
            if (accessToken is null)
            {
                // TODO
                UpdateErrorMessage("Unexpected authentication faiure");
                return;
            }

            // TODO: Save creds separately for each server
            _appSettings.AccessToken = accessToken;

            _sdkClientSettings.SetAccessToken(accessToken);

            Console.WriteLine("Authentication success.");

            _navigationManager.NavigateToHome();

            // After signing in, disallow accidentally coming back here.
            _navigationManager.ClearHistory();
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

    private void UpdateErrorMessage(string message)
    {
        ShowErrorMessage = true;
        ErrorMessage = message;
    }
}
