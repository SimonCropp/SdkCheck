# SdkCheck

Warns at build time when the .NET SDK or runtime in use has published CVEs, or sits on a channel that
has reached end of support.

`NuGetAudit` covers NuGet packages. Nothing covers the SDK or the shared framework, and the usual
substitutes - a `global.json` floor, a hand-written version baseline, `dotnet sdk check` - all encode a
constant that was true the day it was written.

SdkCheck compares the SDK and runtime actually in use against Microsoft's live release metadata, so a CVE
published tomorrow is reported tomorrow with nothing to bump.

See https://github.com/SimonCropp/SdkCheck for full documentation.
