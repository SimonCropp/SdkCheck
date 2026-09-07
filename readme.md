# <img src="/src/icon.png" height="30px"> SdkCheck

[![Build status](https://img.shields.io/github/actions/workflow/status/SimonCropp/SdkCheck/test.yml?branch=main)](https://github.com/SimonCropp/SdkCheck/actions)
[![NuGet Status](https://img.shields.io/nuget/v/SdkCheck.svg?label=SdkCheck)](https://www.nuget.org/packages/SdkCheck/)
[![NuGet Status](https://img.shields.io/nuget/v/SdkCheck.Tool.svg?label=SdkCheck.Tool)](https://www.nuget.org/packages/SdkCheck.Tool/)

Warns at build time when the .NET SDK or runtime in use has published CVEs, or sits on a channel that
has reached end of support.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Why

`NuGetAudit` (NU1901-NU1904) covers NuGet packages. Nothing covers the SDK or the shared framework.

The usual substitutes all encode a constant that was correct the day it was written:

- A `global.json` version floor stops being a floor the moment a CVE is announced against the pinned
  version.
- An MSBuild comparison against a hand-written baseline has the same problem, plus someone has to
  remember to raise it.
- `dotnet sdk check` is live, but only knows support phase and latest-patch-available. It never
  mentions a CVE, and it exits 0 either way.

SdkCheck compares the SDK and runtime actually in use against Microsoft's live
[release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json),
which carries a `security` flag and a `cve-list` for every release. A CVE published tomorrow is
reported tomorrow, with nothing to bump.


## Usage

Reference the package. There is nothing else to configure.

<!-- snippet: Consumer.Basic.csproj -->
<a id='snippet-Consumer.Basic.csproj'></a>
```csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SdkCheck" Version="$(SdkCheckVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```
<sup><a href='/IntegrationTests/Fixtures/Consumer.Basic/Consumer.Basic.csproj#L1-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-Consumer.Basic.csproj' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A build with a vulnerable SDK then says:

```
warning SdkCheck001: SDK has published CVEs. SDK 8.0.100 is affected by 70 CVEs published since it
shipped, fixed in later .NET 8.0 releases. Update to 8.0.424. CVEs: CVE-2024-0056, CVE-2024-0057,
CVE-2024-20672, CVE-2024-21319, CVE-2024-21386, ...
```

The package is a `DevelopmentDependency` with no dependencies of its own. It contributes nothing to
compile references, build output, or a consumer's dependency graph.


## What is checked

| | |
|---|---|
| The SDK running the build | `$(NETCoreSdkVersion)` |
| The runtime the output targets | the targeting or runtime pack resolved for each target framework |
| Self-contained runtime packs | `@(ResolvedRuntimePack)`, for self-contained and AOT publishes |
| End of support | the channel's support phase and end-of-support date |

Most .NET CVEs land in the runtime rather than the SDK, which is why checking only the SDK misses the
bigger half.


## Diagnostics

| Code | Meaning | Default |
|---|---|---|
| [SdkCheck001](/docs/DiagnosticCodes.md#sdkcheck001) | SDK has published CVEs | Warning |
| [SdkCheck002](/docs/DiagnosticCodes.md#sdkcheck002) | Channel is out of support | **Error** |
| [SdkCheck003](/docs/DiagnosticCodes.md#sdkcheck003) | Runtime has published CVEs | Warning |
| [SdkCheck004](/docs/DiagnosticCodes.md#sdkcheck004) | Release metadata unavailable | Message |

Only SdkCheck002 fails a build by default. Everything else has a version to move to on the same
channel; an out-of-support channel does not, so a CVE published against it is unfixable in place.

Each code is suppressible with `NoWarn` in the ordinary way. See
[the full reference](/docs/DiagnosticCodes.md).


## Settings

| Property | Default | |
|---|---|---|
| `SdkCheckEnabled` | `true` | Turn the whole check off. |
| `SdkCheckTreatAsError` | `false` | Fail the build on SdkCheck001 and SdkCheck003. |
| `SdkCheckEolAsError` | `true` | Fail the build on SdkCheck002. |
| `SdkCheckIncludeRuntime` | `true` | Check the runtime the output targets. |
| `SdkCheckIncludeRuntimePacks` | `true` | Check self-contained runtime packs. |
| `SdkCheckCacheHours` | `24` | How long a fetched feed stays fresh. |
| `SdkCheckTimeoutSeconds` | `5` | Per-request timeout. |
| `SdkCheckOffline` | `false` | Never fetch. Uses the cache past its TTL rather than saying nothing. |
| `SdkCheckCacheDirectory` | `%LOCALAPPDATA%/SdkCheck` | Also settable with the `SDKCHECK_CACHE` environment variable. |
| `SdkCheckFeedOverride` | | A directory of `{channel}.json` files to read instead of the network. |

Set these where every project in the build sees the same value — `Directory.Build.props`, or `-p:`
on the command line. A finding is reported once per build, keyed by code and version, and the
project that reaches the check first is the one that reports it, at the severity that project was
configured with; every project after that skips it silently. The SDK is shared by the whole build,
so `SdkCheckTreatAsError` set in one csproj of many fails the build only when that csproj happens to
run first, which build order does not guarantee. A single-project build has no such ordering.


## A build is not a schedule

An MSBuild task only fires when someone builds. A repository that is quiet for six weeks sleeps
through two Patch Tuesdays, and no build-time check can do anything about that.

The tool covers the other half:

```
dotnet tool install --global SdkCheck.Tool
sdkcheck
```

With no arguments it checks every installed SDK and runtime and exits non-zero if anything is found,
so a nightly job can gate on it. `--sdk` and `--runtime` check specific versions instead,
`--format json` emits the full CVE list per finding, and `--warn-only` reports without failing.


## Cost

One HTTP request per .NET channel in use, per day, per machine. The response is reduced to the fields
that matter and cached — the live 8.0 feed is about 1.5 MB, and the cache written from it is about
20 KB — and the parsed result is held for the life of the MSBuild node, so a forty-project solution
does not read anything forty times.

The check never fails a build because of the network. Offline, behind a proxy, a dropped VPN, a 500:
the finding becomes a low-importance message and the build carries on. An unreachable CDN is not
something the person running the build can fix.


## Release metadata

The data comes from Microsoft's own release feed, one file per channel:

```
https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json
```

For each release it carries `security`, a `cve-list` of `{cve-id, cve-url}`, and the SDK, runtime,
ASP.NET Core and Windows Desktop versions that release shipped — plus the channel's `support-phase`
and `eol-date`. SdkCheck finds the release that shipped a given version, then unions the CVEs of
every security release after it.


## Icon

https://thenounproject.com/icon/pattern-7843781/
