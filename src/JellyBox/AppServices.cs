using System.Net.Http.Headers;
using JellyBox.Services;
using JellyBox.ViewModels;
using Jellyfin.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace JellyBox;

internal sealed class AppServices
{
    private AppServices()
    {
        string clientName = Package.Current.DisplayName;
        PackageVersion clientVersion = Package.Current.Id.Version;
        string packageVersionStr = $"{clientVersion.Major}.{clientVersion.Minor}.{clientVersion.Build}.{clientVersion.Revision}";
        EasClientDeviceInformation deviceInformation = new();

        JellyfinSdkSettings sdkClientSettings = new();
        sdkClientSettings.Initialize(
            Package.Current.DisplayName,
            packageVersionStr,
            deviceInformation.FriendlyName,
            deviceInformation.Id.ToString());

        ServiceCollection serviceCollection = new();

        serviceCollection.AddLogging(builder =>
        {
            // TODO: Set up better logging
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        serviceCollection.AddHttpClient(
            "Jellyfin",
            httpClient =>
            {
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(clientName, packageVersionStr));
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 1.0));
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
            });

        // Add Jellyfin SDK services.
        serviceCollection.AddSingleton(sdkClientSettings);
        serviceCollection.AddSingleton<IAuthenticationProvider, JellyfinAuthenticationProvider>();
        serviceCollection.AddScoped<IRequestAdapter, JellyfinRequestAdapter>(s => new JellyfinRequestAdapter(
            s.GetRequiredService<IAuthenticationProvider>(),
            s.GetRequiredService<JellyfinSdkSettings>(),
            s.GetRequiredService<IHttpClientFactory>().CreateClient("Jellyfin")));
        serviceCollection.AddScoped<JellyfinApiClient>();

        serviceCollection.AddSingleton<AppSettings>();
        serviceCollection.AddSingleton<NavigationManager>();
        serviceCollection.AddSingleton<DeviceProfileManager>();

        // View Models
        serviceCollection.AddTransient<HomeViewModel>();
        serviceCollection.AddTransient<ItemDetailsViewModel>();
        serviceCollection.AddTransient<LazyLoadedImageViewModel>();
        serviceCollection.AddTransient<LoginViewModel>();
        serviceCollection.AddTransient<MainPageViewModel>();
        serviceCollection.AddTransient<MoviesViewModel>();
        serviceCollection.AddTransient<ServerSelectionViewModel>();
        serviceCollection.AddTransient<ShowsViewModel>();
        serviceCollection.AddTransient<VideoViewModel>();
        serviceCollection.AddTransient<WebVideoViewModel>();

        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    public IServiceProvider ServiceProvider { get; }

    private static AppServices? _instance;

    private static readonly object InstanceLock = new();

    private static AppServices GetInstance()
    {
        lock (InstanceLock)
        {
            return _instance ??= new AppServices();
        }
    }

    public static AppServices Instance => _instance ?? GetInstance();
}
