# Copilot Instructions for JellyBox

Trust these instructions. Only search the codebase if the information here is incomplete or found to be incorrect.

## Project Overview

JellyBox is a native Xbox/UWP app for [Jellyfin](https://jellyfin.org/), an open-source media server. Single C# project (~50 source files) in `src/JellyBox/`. It targets `net9.0-windows10.0.26100.0` with `UseUwp=true`, `PublishAot=true`, and `DisableRuntimeMarshalling=true`. There are no test projects.

**This is UWP XAML (`Windows.UI.Xaml`), NOT WinUI 3 (`Microsoft.UI.Xaml`).** Always use UWP-specific APIs.

## Building — IMPORTANT

**Always use MSBuild to build. `dotnet build` does NOT work** (it fails with an `XmlPoke` error because UWP tooling requires MSBuild).

```
msbuild JellyBox.sln -t:Build -p:Configuration=Debug -p:Platform=x64
```

- Always use `-` (dash) for MSBuild parameters, not `/` (slash).
- NuGet restore runs automatically (configured in `Directory.Build.rsp` which includes `/Restore`). No separate restore step needed.
- Only `x64` platform is configured in the solution. Do not use `AnyCPU` or other platforms.
- Build takes ~25 seconds. Allow at least 60 seconds of wait time.
- Build output goes to `artifacts/` (via `UseArtifactsOutput=true` in `Directory.Build.props`).
- `TreatWarningsAsErrors=true` with `AnalysisMode=All` — **any analyzer warning is a build error**. The build must produce 0 warnings and 0 errors.
- There are no tests, no linter commands, and no scripts in the repo. The build is the only validation step.
- **CI** (`.github/workflows/build.yml`): runs `msbuild -property:Configuration=Release -property:Platform=x64` on `windows-latest`. Requires .NET 9 SDK and MSBuild (via `microsoft/setup-msbuild@v2`).

To add a new NuGet package, add it to `Directory.Packages.props` (central package management) with a `<PackageVersion>` entry, then reference it without a version in `src/JellyBox/JellyBox.csproj`.

## Project Structure

```
src/JellyBox/
├── App.xaml(.cs)          # Entry point, startup flow
├── AppServices.cs         # DI container (singleton pattern, all registrations here)
├── AppSettings.cs         # Persistent settings via ApplicationData.Current.LocalSettings
├── MainPage.xaml(.cs)     # Shell page with content frame + navigation overlay
├── Behaviors/             # XAML behaviors (focus, scrolling, commands)
├── Controls/              # Custom controls (media transport, lazy images, loading overlay)
├── Converters/            # XAML value converters
├── Models/                # Data models (Card, Section, CardFactory, INavigable)
├── Resources/             # XAML resource dictionaries (Styles, Templates, TransportControlsStyles)
├── Services/              # NavigationManager, JellyfinImageResolver, DeviceProfileManager, DisplayModeManager
├── ViewModels/            # MVVM ViewModels
└── Views/                 # XAML pages (Home, Library, ItemDetails, Login, ServerSelection, Video, WebVideo)
```

Key repo-root files: `Directory.Build.props` (shared MSBuild properties), `Directory.Build.rsp` (default MSBuild args), `Directory.Packages.props` (central package versions), `.editorconfig` (code style rules enforced as errors), `version.json` (Nerdbank.GitVersioning).

## Code Conventions (enforced by analyzers as errors)

- **All types are `internal` by default.** Use `#pragma warning disable CA1515` only where `public` is required (e.g., `App` class).
- DI-registered types must suppress CA1812: `#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Used via dependency injection.`
- File-scoped namespaces: `namespace JellyBox;`
- Private fields: `_camelCase`. Private static fields: `s_camelCase`. Constants: `PascalCase`.
- No `this.` qualification (enforced as error).
- `readonly` on fields that can be (enforced as error).
- Unused parameters are errors (CA: `dotnet_code_quality_unused_parameters = all:error`).
- Remove unnecessary usings (IDE0005 is error).
- Do not use `ConfigureAwait` — UWP needs the synchronization context.
- Errors are caught and logged via `System.Diagnostics.Debug.WriteLine`, not `ILogger`.
- XAML uses compiled bindings (`x:Bind`), not `{Binding}`.
- XAML styles use semantic brushes from `Resources/Styles.xaml` (e.g., `BackgroundBase`, `TextPrimary`, `AccentColor`).

## Architecture Patterns

**MVVM** with [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/):
- ViewModels inherit `ObservableObject`, use `[ObservableProperty]` with partial property syntax (`public partial bool IsLoading { get; set; }`), and `[RelayCommand]` for commands.
- `[RelayCommand]` async methods get an auto-injected `CancellationToken` — pass it to API calls.
- ViewModels are **Transient** in DI; services (including `JellyfinApiClient` and `IRequestAdapter`) are **Singleton**.

**Navigation**: `NavigationManager` (in `Services/`) handles all navigation. Content pages use `NavigateContentFrame`; full-screen pages use `NavigateAppFrame`. Parameters are strongly-typed records on the View class.

**Jellyfin API**: ViewModels inject `JellyfinApiClient` (Kiota-generated). Types are in `Jellyfin.Sdk.Generated.Models`. Image URLs via `JellyfinImageResolver`.

**Complex types** may be split across partial class files by concern (e.g., `VideoViewModel` has 6 files: `.cs`, `.Controls.cs`, `.MediaSource.cs`, `.Playback.cs`, `.Reporting.cs`, `.Tracks.cs`).

### Adding a New View / ViewModel

1. Create XAML page in `Views/` and ViewModel in `ViewModels/`.
2. ViewModel: `internal sealed partial class MyViewModel : ObservableObject` with CA1812 pragma.
3. Register ViewModel as **Transient** in `AppServices.cs`.
4. View code-behind resolves ViewModel: `AppServices.Instance.ServiceProvider.GetRequiredService<MyViewModel>()`.
5. Add navigation in `NavigationManager` if needed.
6. Parameters: define `internal sealed record Parameters(...)` on the View.

## Reference Resources

- [jellyfin/jellyfin-web](https://github.com/jellyfin/jellyfin-web) — Primary reference for feature implementation. JellyBox ports logic from it.
- [jellyfin/jellyfin](https://github.com/jellyfin/jellyfin) — Server codebase for understanding API behavior.
- [Jellyfin SDK for C#](https://github.com/jellyfin/jellyfin-sdk-csharp) — Kiota-generated SDK. Types in `Jellyfin.Sdk.Generated.Models`.
- [CommunityToolkit.Mvvm docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — MVVM framework.
- [UWP docs](https://learn.microsoft.com/en-us/windows/uwp/) — **Not** WinUI 3.
