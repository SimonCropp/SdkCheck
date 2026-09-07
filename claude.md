# SdkCheck

An MSBuild task and a dotnet tool that warn when the .NET SDK or runtime in use has published CVEs,
or sits on an out-of-support channel. Data comes from Microsoft's live release metadata feed, so no
version baseline is maintained here.

## Layout

- `src/` holds the solution. The repo root is one level up: `ProjectDefaults` derives `RepoDir` from
  the `.git` directory above `$(SolutionDir)` and writes packages to `<root>/nugets`.
- `src/SdkCheck.Core` - the logic. Not published; it reaches consumers inside the task package and
  the tool.
- `src/SdkCheck` - the MSBuild task package. `netstandard2.0` only, and loaded by every host.
- `src/SdkCheck.Tool` - the `sdkcheck` command.
- `IntegrationTests/` is a separate solution. It builds real consumer projects against the nupkg the
  `src` Release build emits, so `dotnet build src -c Release` must run first.

## Conventions

- `ProjectDefaults` supplies packaging metadata, nullable, implicit usings and code style. Do not set
  those per-project, and never hand-edit `.editorconfig` or `Shared.sln.DotSettings` - both are
  overwritten from the package on every build.
- One `NoWarn` property in `src/Directory.Build.props`. A second declaration replaces the first
  rather than appending.
- Tests are TUnit plus Verify, run with
  `dotnet run --project <project> -c Release --no-build -- --no-ansi --progress off`. Filtering uses
  `--treenode-filter`, not `--filter`.
- Snapshots under ten lines are inlined into the test source by `VerifierSettings.Inline`.
- `readme.md` and `docs/*.md` are maintained in place by MarkdownSnippets (`InPlaceOverwrite`), and
  `ValidateContent` fails the build on stale content. It also enforces a house style: no second
  person, and no "just", "simple", "easy", "we", "our", "please".
- Snippets come from the real fixture files under `IntegrationTests/Fixtures`, so documented usage
  cannot drift from what is tested.

## Rules

- Adding or changing a diagnostic code means updating `docs/DiagnosticCodes.md` in the same change.
  `Diagnostics.DocsUrl` deep-links every emitted message to a section there.
- The SDK version named as the fix stays on the feature band the component is on. A `global.json`
  pinned to 8.0.1xx does not roll to 8.0.4xx, so the channel's `latest-sdk` is an instruction that
  cannot always be followed. The band's newest counts only when it shipped in a release at least as
  new as the last security release contributing CVEs - an older one is a partial fix reported as a
  whole one. A band that has stopped shipping falls back to `latest-sdk`, and the message then says
  the band changes.
- Nothing this package discovers may fail a consumer's build except the SDK running it sitting on an
  out-of-support channel. An unreachable feed, a corrupt cache, a version the feed has never heard
  of: all resolve to a low-importance message. A package that breaks builds when a network hiccups
  gets removed long before it catches a CVE.
- An out-of-support target framework warns rather than erroring. The SDK is a machine property that
  installing a supported one fixes that day; a target framework is a support decision the project
  made deliberately, so erroring turns the date a channel dies into a build break with no change
  behind it. `SdkCheckEolAsError` set explicitly still applies to both.
- End of support is tested before the version comparison. On a dead channel there is no newer
  security release, so a plain "am I behind?" check reports nothing - it goes silent at exactly the
  point exposure stops being fixable.
- Never hand a raw feed version to `Version.Parse`. Prerelease release-versions like `8.0.0-rc.2`
  throw. Use `ReleaseVersion`.
- The task assembly stays `netstandard2.0`, and `SdkCheck.targets` selects it unconditionally.
  `$(MSBuildRuntimeType) == 'Core'` means "MSBuild on .NET" and never says which version, so
  selecting a framework-specific asset on it ships a package that only loads on the newest SDK.
  That shipped once, in 0.2.0, and broke consumers with MSB4062.
- The SDK check hangs off `BeforeBuild`; only the runtime check may depend on
  `ResolveFrameworkReferences`. A netstandard2.0 project resolves no framework reference, so
  anything downstream of that target silently never runs for it.
