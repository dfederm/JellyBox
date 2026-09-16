# Repository Instructions for JellyBox

Trust these instructions. Search the repository when task-specific context or current configuration values are needed.

## Essential Context

JellyBox is a small, single-project native Xbox app for Jellyfin. The application is under `src/JellyBox/`. It is an SDK-style modern .NET project, not a .NET Framework project; read the project file for the current target framework.

**This is UWP XAML (`Windows.UI.Xaml`), not WinUI 3 (`Microsoft.UI.Xaml`).** Use UWP APIs even though the project references the `Microsoft.UI.Xaml` package for controls.

Read `CONTRIBUTING.md` for contributor workflow, manual-testing expectations, and pull request requirements.

## Build and Validation

**Always build with MSBuild. `dotnet build` does not work** because the UWP packaging targets require MSBuild.

```powershell
msbuild JellyBox.sln -t:Build -p:Configuration=Debug -p:Platform=x64
```

- Use `-` for MSBuild switches; `/` is parsed as a path by some agent shells.
- Do not run a separate restore. `Directory.Build.rsp` enables restore automatically.
- Build only `x64`; it is the solution's configured platform.
- Allow at least 60 seconds. Output is written under `artifacts/`.
- Warnings and analyzer findings fail the build. A successful build must have 0 warnings and 0 errors.
- There are no automated tests or separate lint commands. The build is the required repository validation; apply the manual-testing guidance in `CONTRIBUTING.md` when behavior changes.
- Treat `.github/workflows/build.yml` and the project files as authoritative for current CI, SDK, runtime, and action versions rather than copying those values into documentation.

## Non-obvious Implementation Rules

- For formatting, naming, and analyzer-enforced C# conventions, `.editorconfig` is authoritative. The rules below cover additional repository conventions.
- Types are `internal` by default. Suppress CA1515 only where a framework requires a public type.
- DI-registered internal types may need the repository's CA1812 suppression.
- Do not use `ConfigureAwait`; UWP code depends on its synchronization context.
- Use `x:Bind` rather than `{Binding}`, and use semantic resources from `Resources/Styles.xaml` instead of literal colors.
- Use injected `ILogger<T>` and source-generated `[LoggerMessage]` methods for application logging. Direct `Debug.WriteLine` calls are reserved for failures inside the logging infrastructure.
- Manage package versions centrally in `Directory.Packages.props`; omit versions from `PackageReference` entries.

## Architecture

- `AppServices.cs` is the composition root. ViewModels are transient; application services, `JellyfinApiClient`, and `IRequestAdapter` are singletons.
- ViewModels use CommunityToolkit.Mvvm: inherit `ObservableObject`, use partial `[ObservableProperty]` properties, and use `[RelayCommand]` methods. Pass generated command cancellation tokens to API calls.
- `NavigationManager` owns navigation. Use the content frame for shell content and the app frame for full-screen flows. Put strongly typed navigation parameter records on the destination view.
- Inject the Kiota-generated `JellyfinApiClient`; generated models are under `Jellyfin.Sdk.Generated.Models`. Resolve image URLs through `JellyfinImageResolver`.
- Split large types into partial files by concern rather than growing a single monolithic file.

### Adding a View and ViewModel

1. Add the page under `Views/` and the ViewModel under `ViewModels/`.
2. Make the ViewModel an `internal sealed partial` `ObservableObject` and apply the standard CA1812 suppression.
3. Register it as transient in `AppServices.cs`.
4. Resolve it from `AppServices.Instance.ServiceProvider` in the page code-behind.
5. Route navigation through `NavigationManager`; define a `Parameters` record on the view when arguments are required.

## External References

- Use [jellyfin/jellyfin-web](https://github.com/jellyfin/jellyfin-web) as the primary behavioral reference when porting Jellyfin features.
- Use [jellyfin/jellyfin](https://github.com/jellyfin/jellyfin) for server behavior and [jellyfin/jellyfin-sdk-csharp](https://github.com/jellyfin/jellyfin-sdk-csharp) for generated client details.
- Use the [UWP documentation](https://learn.microsoft.com/en-us/windows/uwp/), not WinUI 3 documentation.
