# <img src="/src/icon.png" height="30px"> SdkCheck

[![Build status](https://img.shields.io/github/actions/workflow/status/SimonCropp/SdkCheck/test.yml?branch=main)](https://github.com/SimonCropp/SdkCheck/actions)
[![NuGet Status](https://img.shields.io/nuget/v/SdkCheck.svg?label=SdkCheck)](https://www.nuget.org/packages/SdkCheck/)
[![NuGet Status](https://img.shields.io/nuget/v/SdkCheck.Tool.svg?label=SdkCheck.Tool)](https://www.nuget.org/packages/SdkCheck.Tool/)

Warns at build time when the .NET SDK or runtime in use has published CVEs, or sits on a channel that has reached end of support.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Why

A CVE in the .NET SDK or runtime is fixed in the next patch release. The SDK a build runs on is whatever was installed on the machine, baked into the CI image or pinned in `global.json`, and it picks the runtime the output is built against. A project keeps compiling, testing and shipping on a version with published CVEs until someone notices.

`NuGetAudit` (NU1901-NU1904) covers NuGet packages. `CheckSdkVulnerabilities` covers the SDK, on .NET 11 and later, behind an opt-in property. That leaves the runtime, where most .NET CVEs land, and every SDK before 11. The usual substitutes never name a CVE:

- `dotnet sdk check` reports support phase and whether a newer patch exists, for whatever is installed rather than what a build used, and exits 0 either way.
- A `global.json` floor or a hand-written version baseline is a version someone typed once. It was right that day, and the next security release makes it wrong until someone remembers to raise it.

SdkCheck compares the SDK and runtime actually in use against Microsoft's live [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json), which carries a `security` flag and a `cve-list` for every release. A CVE published tomorrow is reported tomorrow, with nothing to bump.


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
warning SdkCheck001: SDK has published CVEs.
SDK 8.0.100 is affected by 70 CVEs published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.130.
CVEs:
 * CVE-2024-0056
 * CVE-2024-0057
 * CVE-2024-20672
 * CVE-2024-21319 ...
```

The package is a `DevelopmentDependency` with no dependencies of its own. It contributes nothing to compile references, build output, or a consumer's dependency graph.


## What is checked

| | |
|---|---|
| The SDK running the build | `$(NETCoreSdkVersion)` |
| The runtime the output targets | the targeting or runtime pack resolved for each target framework |
| Self-contained runtime packs | `@(ResolvedRuntimePack)`, for self-contained and AOT publishes |
| End of support | the channel's support phase and end-of-support date |


## Versus the built-in SDK check

.NET 11 Preview 5 added `CheckSdkVulnerabilities`, an opt-in property that warns when the SDK running the build has published CVEs ([NETSDK1238](https://learn.microsoft.com/dotnet/core/tools/sdk-errors/netsdk1238)), has reached end of support (NETSDK1239), or sits on a feature band with no newer release (NETSDK1240). It reads the same Microsoft release metadata. For the SDK, on .NET 11, it needs no package reference and is the better answer.

| | SdkCheck | The .NET SDK |
|---|---|---|
| SDK CVEs | SdkCheck001 | NETSDK1238, .NET 11+, opt-in |
| SDK channel out of support | SdkCheck002 | NETSDK1239, .NET 11+, opt-in |
| Feature band no longer shipping | named in the SdkCheck001 fix | NETSDK1240, .NET 11+, opt-in |
| Target framework out of support | SdkCheck002 | NETSDK1138, on by default |
| Runtime CVEs | SdkCheck003 | - |
| Runtime pack CVEs, self-contained and AOT | SdkCheck003 | - |
| SDK 8, 9 and 10 | yes | - |
| Away from a build | `sdkcheck` | - |

The gap is the runtime. NETSDK1238 reports the SDK that ran the build, and says nothing about the shared framework the output was compiled against or the runtime pack a self-contained publish embeds.


## Diagnostics

| Code | Meaning | Default |
|---|---|---|
| [SdkCheck001](/docs/DiagnosticCodes.md#sdkcheck001) | SDK has published CVEs | Warning |
| [SdkCheck002](/docs/DiagnosticCodes.md#sdkcheck002) | Channel is out of support | **Error** for the SDK, warning for a runtime |
| [SdkCheck003](/docs/DiagnosticCodes.md#sdkcheck003) | Runtime has published CVEs | Warning |
| [SdkCheck004](/docs/DiagnosticCodes.md#sdkcheck004) | Release metadata unavailable | Message |

Only an out-of-support SDK fails a build by default. Everything else has a version to move to on the same channel; a dead channel does not, so a CVE published against it is unfixable in place. The SDK is a property of the machine running the build, and installing a supported one fixes it that day.

A target framework on a dead channel warns instead. That one is a support decision the project made deliberately - a library targets net6.0 for the consumers still there - and a build that has not changed should not start failing on the date the channel dies. `SdkCheckEolAsError` set explicitly applies one level to both.

Each code is suppressible with `NoWarn` in the ordinary way. See [the full reference](/docs/DiagnosticCodes.md).


## Settings

| Property | Default | |
|---|---|---|
| `SdkCheckEnabled` | `true` | Turn the whole check off. |
| `SdkCheckTreatAsError` | `false` | Fail the build on SdkCheck001 and SdkCheck003. |
| `SdkCheckEolAsError` | SDK only | `true` fails the build on SdkCheck002 for every component, `false` for none. |
| `SdkCheckIncludeRuntime` | `true` | Check the runtime the output targets. |
| `SdkCheckIncludeRuntimePacks` | `true` | Check self-contained runtime packs. |
| `SdkCheckCacheHours` | `24` | How long a fetched feed stays fresh. |
| `SdkCheckTimeoutSeconds` | `5` | Per-request timeout. |
| `SdkCheckOffline` | `false` | Never fetch. Uses the cache past its TTL rather than saying nothing. |
| `SdkCheckCacheDirectory` | `%LOCALAPPDATA%/SdkCheck` | Also settable with the `SDKCHECK_CACHE` environment variable. |
| `SdkCheckFeedOverride` | | A directory of `{channel}.json` files to read instead of the network. |

Set these where every project in the build sees the same value — `Directory.Build.props`, or `-p:` on the command line. A finding is reported once per MSBuild node, keyed by code and version, and the project that reaches the check first on that node is the one that reports it, at the severity that project was configured with; every project after that skips it silently. The SDK is shared by the whole build, so `SdkCheckTreatAsError` set in one csproj of many fails the build only when that csproj happens to run first, which build order does not guarantee. A single-project build has no such ordering.


## A build is not a schedule

An MSBuild task only fires when someone builds. A repository that is quiet for six weeks sleeps through two Patch Tuesdays, and no build-time check can do anything about that.

The tool covers the other half:

```
dotnet tool install --global SdkCheck.Tool
sdkcheck
```

With no arguments it checks every installed SDK and runtime and exits non-zero if anything is found, so a nightly job can gate on it. `--sdk` and `--runtime` check specific versions instead, `--format json` emits the full CVE list per finding, and `--warn-only` reports without failing.


## Cost

One HTTP request per .NET channel in use, per day, per machine. The response is reduced to the fields that matter and cached — the live 8.0 feed is about 1.5 MB, and the cache written from it is about 20 KB — and the parsed result is held for the life of the MSBuild node, so a forty-project solution does not read anything forty times.

The check never fails a build because of the network. Offline, behind a proxy, a dropped VPN, a 500: the finding becomes a low-importance message and the build carries on. An unreachable CDN is not something the person running the build can fix.


## Release metadata

The data comes from Microsoft's own release feed, one file per channel:

```
https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json
```

For each release it carries `security`, a `cve-list` of `{cve-id, cve-url}`, and the SDK, runtime, ASP.NET Core and Windows Desktop versions that release shipped — plus the channel's `support-phase` and `eol-date`. SdkCheck finds the release that shipped a given version, then unions the CVEs of every security release after it.


## Icon

https://thenounproject.com/icon/pattern-7843781/
