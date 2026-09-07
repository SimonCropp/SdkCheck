public class FindingTests
{
    static readonly Component sdk = new(ComponentKind.Sdk, "8.0.100");

    [Test]
    public Task SdkCve() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(3), "8.0.204").Message())
            .Snapshot(
                """
                SDK has published CVEs.
                SDK 8.0.100 is affected by 3 CVEs published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204.
                CVEs:
                 * CVE-2024-0001
                 * CVE-2024-0002
                 * CVE-2024-0003
                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck001
                """);

    /// <summary>
    /// When the band changes, the message says so: a reader pinned to the old one has more to change
    /// than a version number. The band is named as different rather than later, since the channel's
    /// latest can sit on an earlier one than a component on a band that has stopped shipping.
    /// </summary>
    [Test]
    public Task SdkCveCrossingAFeatureBand() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(1), "8.0.204", crossesFeatureBand: true).Body())
            .Snapshot(
                """
                SDK 8.0.100 is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204, on a different feature band: nothing newer shipped on the band in use.
                CVEs:
                 * CVE-2024-0001
                """);

    /// <summary>
    /// The other reason the band is left: something did ship on it, and predates the security release
    /// that carries the rest of the list. Naming it leaves the reader to weigh a partial fix that
    /// keeps the pin against a whole one that does not.
    /// </summary>
    [Test]
    public Task SdkCveWhereTheBandNewestIsTooOld() =>
        Verify(
                new Finding(
                        Diagnostics.SdkCve,
                        sdk,
                        "8.0",
                        Cves(1),
                        "8.0.204",
                        crossesFeatureBand: true,
                        bandNewest: "8.0.105")
                    .Body())
            .Snapshot(
                """
                SDK 8.0.100 is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204, on a different feature band: the band in use stops at 8.0.105, which predates the channel's last security release.
                CVEs:
                 * CVE-2024-0001
                """);

    [Test]
    public Task Eol() =>
        Verify(new Finding(Diagnostics.Eol, sdk, "6.0", eolDate: "2024-11-12").Message())
            .Snapshot(
                """
                Channel is out of support.
                The .NET 6.0 channel reached end of support on 2024-11-12.
                No further security patches will ship for SDK 8.0.100, so any CVE found in it from now on is unfixable in place.
                Move to a supported channel.
                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck002
                """);

    [Test]
    public Task Runtime() =>
        Verify(new Finding(
                Diagnostics.RuntimeCve,
                new(ComponentKind.RuntimePack, "8.0.0", "win-x64"),
                "8.0",
                Cves(1),
                "8.0.4")
            .Message())
            .Snapshot(
                """
                Runtime has published CVEs.
                runtime pack 8.0.0 (win-x64) is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.4.
                CVEs:
                 * CVE-2024-0001
                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck003
                """);

    [Test]
    public Task Unavailable() =>
        Verify(new Finding(Diagnostics.Unavailable, sdk, "8.0", detail: "timed out").Message())
            .Snapshot(
                """
                Release metadata unavailable.
                Release metadata for .NET 8.0 could not be read (timed out), so SDK 8.0.100 was not checked.
                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck004
                """);

    /// <summary>
    /// Seventy is what a real .NET 8 SDK two years behind produces. Every id is listed.
    /// </summary>
    [Test]
    public Task LongCveListIsNotTruncated() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(70), "8.0.204").Body())
            .NotInline();

    [Test]
    public Task SingleCveIsNotPluralised() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(1), "8.0.204").Body())
            .Snapshot(
                """
                SDK 8.0.100 is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204.
                CVEs:
                 * CVE-2024-0001
                """);

    static List<Cve> Cves(int count) =>
        Enumerable.Range(1, count)
            .Select(_ => new Cve { Id = $"CVE-2024-{_:0000}", Url = $"https://example.invalid/{_}" })
            .ToList();
}
