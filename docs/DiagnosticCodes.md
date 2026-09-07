# Diagnostic codes

Every message SdkCheck emits carries one of these codes, and every code deep-links back to its
section here.

All of them are suppressible the ordinary way, because the task passes a real code through MSBuild's
long `LogWarning` overload:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);SdkCheck001</NoWarn>
</PropertyGroup>
```


## SdkCheck001

- **Name:** SDK has published CVEs
- **Level:** Warning. Set `SdkCheckTreatAsError` to `true` to fail the build instead.
- **Meaning:** The SDK running this build shipped in a release that has been followed by one or more
  security releases on the same channel. The CVEs listed were published against it.
- **Syntax:** `SDK {version} is affected by {n} CVEs published since it shipped, fixed in later .NET
  {channel} releases. Update to {version}. CVEs: {ids}`
- **Example:** `SDK 8.0.100 is affected by 70 CVEs published since it shipped, fixed in later .NET
  8.0 releases. Update to 8.0.424. CVEs: CVE-2024-0056, CVE-2024-0057, CVE-2024-20672, ...`
- **Fix:** Install the named SDK. If a `global.json` pins the version, raise it there too, otherwise
  the machine keeps selecting the old one.

Every id is listed, never a subset. The count and the version to move to already state everything
that drives the action, so the ids are there for audit traceability - and a truncated audit list is
the one form with no use. An SDK two years behind lists around seventy, which is a long warning, but
the count is stated first so the length is never a surprise. `--format json` on the tool gives the
same ids with their urls.


## SdkCheck002

- **Name:** Channel is out of support
- **Level:** **Error** when the SDK running the build sits on the channel, warning when a runtime
  does. Set `SdkCheckEolAsError` to `true` or `false` to apply one level to both.
- **Meaning:** The channel has passed its end-of-support date, or is marked `eol` in the release
  metadata. No further security patches will ship for it, so any CVE published against it from now on
  is unfixable in place.
- **Syntax:** `The .NET {channel} channel reached end of support on {date}. No further security
  patches will ship for {component}, so any CVE found in it from now on is unfixable in place. Move
  to a supported channel.`
- **Example:** `The .NET 6.0 channel reached end of support on 2024-11-12. ...`
- **Fix:** Move to a supported channel.

The SDK is the one case that errors by default. Everything else has a version to move to on the same
channel; a dead channel does not. The SDK doing the build is a property of the machine, so installing
a supported one fixes it that day.

A target framework on a dead channel is left as a warning. Targeting one is a decision the project
made deliberately, for consumers who are still there, and no build that has not changed should start
failing on the date the channel dies - least of all on the say-so of a remote feed.

It is also the case that is easiest to get wrong, and the reason end of support is tested before the
version comparison. On a dead channel there is by definition no newer security release, so a plain
"am I behind?" check reports nothing — it goes silent at exactly the point exposure stops being
fixable.


## SdkCheck003

- **Name:** Runtime has published CVEs
- **Level:** Warning. Set `SdkCheckTreatAsError` to `true` to fail the build instead.
- **Meaning:** As SdkCheck001, but for a runtime rather than an SDK: the shared framework the output
  is built against, or a runtime pack resolved for a self-contained publish.
- **Syntax:** `runtime {version} is affected by {n} CVEs published since it shipped, ...`
- **Example:** `runtime pack 8.0.0 (win-x64) is affected by 3 CVEs published since it shipped, fixed
  in later .NET 8.0 releases. Update to 8.0.4. CVEs: ...`
- **Fix:** For a framework-dependent build, move the target framework or the installed runtime
  forward. For a self-contained one, `TargetLatestRuntimePatch` or an explicit
  `RuntimeFrameworkVersion` controls which runtime is embedded.

Most .NET CVEs land in the runtime rather than the SDK, and for a self-contained publish the runtime
is what actually ships. Set `SdkCheckIncludeRuntime` or `SdkCheckIncludeRuntimePacks` to `false` to
turn either half off.


## SdkCheck004

- **Name:** Release metadata unavailable
- **Level:** Message, at low importance. It never fails a build and never appears at default
  verbosity.
- **Meaning:** The release feed for a channel could not be read — offline, a proxy, a timeout, a
  corrupt cache with nothing to fall back to — so nothing on that channel was checked.
- **Syntax:** `Release metadata for .NET {channel} could not be read ({reason}), so {component} was
  not checked.`
- **Example:** `Release metadata for .NET 8.0 could not be read (timed out), so SDK 8.0.100 was not
  checked.`
- **Fix:** Usually none required. Build again when the network is available.

Deliberately not a warning. An unreachable CDN is not something the person running the build can fix,
and a package that breaks builds when a network hiccups would be removed long before it ever caught a
CVE. Raise verbosity to `detailed` to see it.
