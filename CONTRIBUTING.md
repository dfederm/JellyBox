# Contributing to JellyBox

Thanks for your interest in improving JellyBox — a native Xbox/UWP app for
[Jellyfin](https://jellyfin.org/). This guide covers what we expect from a
contribution so review can stay fast and focused. Please read it before opening
a pull request.

## Keep pull requests single-purpose

This is the most important guideline.

- **One logical change per PR.** Don't combine unrelated changes — e.g. a build
  fix and a feature, or a refactor and a bug fix. Mixing them blocks merging the
  good part while the rest is still under review, and multiplies the effort to
  review either.
- **Keep PRs small and reviewable.** If a change is large, split it into a
  stacked series of focused PRs that each stand on their own.
- If you find an unrelated problem while working, note it (or open an issue)
  rather than folding the fix into the current PR.

A tightly-scoped PR is far more likely to merge quickly than a large one.

## Build is the only gate

There are no automated tests. **The build is the only validation step, and it
must be green with 0 warnings** — `TreatWarningsAsErrors` and `AnalysisMode=All`
mean any analyzer warning is a build error.

Build with **MSBuild, not `dotnet build`** (UWP tooling requires MSBuild):

```
msbuild JellyBox.sln -t:Build -p:Configuration=Debug -p:Platform=x64
```

Only the `x64` platform is configured, and NuGet restore runs automatically as
part of the build (no separate restore step). CI builds every PR with an MSBuild
`Release` x64 build; please make sure your local build passes first.

## Manual testing

Because there are no automated tests — and because this is a TV / controller app
where focus and playback behavior can't be judged from a diff — **changes to UI,
playback, or controller handling must be manually tested before review.**

Testing on a **real Xbox is strongly preferred**: it's the primary target, and
behavior such as controller focus, performance, and video playback can differ
there. We know a physical Xbox is a high barrier to entry, though, so testing on
**Windows** (the app is UWP and runs on the desktop) is acceptable too — ideally
with a controller to exercise focus and navigation.

Whichever you use, **state in your PR which platform(s) you tested on and what
you exercised**, for example:

- "Xbox Series X — subtitle switching across 3 tracks (off → English → forced)."
- "Windows desktop (Xbox controller) — Back and Menu buttons during video
  playback."
- "Windows — focus navigation through the Home rows with a gamepad; not yet
  verified on Xbox."

## Writing a good PR description

A clear description is part of the contribution. Please include:

- **What and why** — what the change does and the problem it solves. Link any
  related issue (e.g. `Fixes #123`).
- **Screenshots or a short screen recording for any UI/UX change.** Visual and
  focus behavior on a TV can't be reviewed from the diff alone, so this is
  required for UI changes.
- **Manual test steps you performed** (see above).
- **Anything deliberately left out of scope** — follow-ups you're intentionally
  not doing in this PR, so reviewers know what to expect.

## Code style and conventions

JellyBox enforces its conventions through analyzers, and the build treats
warnings as errors — so most style issues fail the build rather than needing
reviewer nitpicks. The main conventions:

- **This is UWP XAML (`Windows.UI.Xaml`), not WinUI 3 (`Microsoft.UI.Xaml`).**
  Always use UWP APIs.
- Dependencies use **central package management** — add a `<PackageVersion>` to
  `Directory.Packages.props`, then reference the package without a version.
- File-scoped namespaces; types `internal` by default; `x:Bind` over `{Binding}`;
  semantic brushes (from `Resources/Styles.xaml`) for XAML styling; and no
  `ConfigureAwait` (UWP needs the synchronization context).

## Questions

If you're unsure whether a change fits, or how best to split a larger effort,
open an issue to discuss it before investing in a big PR.
