# SdkCheck

Warns at build time when the .NET SDK or runtime in use has published CVEs, or sits on a channel that has reached end of support.

A CVE in the .NET SDK or runtime is fixed in the next patch release. The SDK a build runs on is whatever was installed on the machine, baked into the CI image or pinned in `global.json`, and it picks the runtime the output is built against. A project keeps compiling, testing and shipping on a version with published CVEs until someone notices.

`NuGetAudit` covers NuGet packages. `CheckSdkVulnerabilities` covers the SDK, on .NET 11 and later. That leaves the runtime, where most .NET CVEs land, and every SDK before 11. The usual substitutes - `dotnet sdk check`, a `global.json` floor, a hand-written version baseline - never name a CVE.

SdkCheck compares the SDK and runtime actually in use against Microsoft's live release metadata, so a CVE published tomorrow is reported tomorrow with nothing to bump.

See https://github.com/SimonCropp/SdkCheck for full documentation.
