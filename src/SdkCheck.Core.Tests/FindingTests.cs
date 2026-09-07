namespace testing;

public class FindingTests
{
    static readonly Component sdk = new(ComponentKind.Sdk, "8.0.100");

    [Test]
    public Task SdkCve() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(3), "8.0.204").Message())
            .Snapshot(
                """
                SDK has published CVEs. SDK 8.0.100 is affected by 3 CVEs published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204. CVEs: CVE-2024-0001, CVE-2024-0002, CVE-2024-0003

                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck001
                """);

    [Test]
    public Task Eol() =>
        Verify(new Finding(Diagnostics.Eol, sdk, "6.0", eolDate: "2024-11-12").Message())
            .Snapshot(
                """
                Channel is out of support. The .NET 6.0 channel reached end of support on 2024-11-12. No further security patches will ship for SDK 8.0.100, so any CVE found in it from now on is unfixable in place. Move to a supported channel.

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
                Runtime has published CVEs. runtime pack 8.0.0 (win-x64) is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.4. CVEs: CVE-2024-0001

                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck003
                """);

    [Test]
    public Task Unavailable() =>
        Verify(new Finding(Diagnostics.Unavailable, sdk, "8.0", detail: "timed out").Message())
            .Snapshot(
                """
                Release metadata unavailable. Release metadata for .NET 8.0 could not be read (timed out), so SDK 8.0.100 was not checked.

                See: https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md#sdkcheck004
                """);

    /// <summary>
    /// An SDK two years behind is affected by ~70 CVEs, which is not a readable build warning.
    /// </summary>
    [Test]
    public Task LongCveListIsSummarised() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(70), "8.0.204").Body())
            .Snapshot("SDK 8.0.100 is affected by 70 CVEs published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204. CVEs: CVE-2024-0001, CVE-2024-0002, CVE-2024-0003, CVE-2024-0004, CVE-2024-0005, CVE-2024-0006, CVE-2024-0007, CVE-2024-0008, CVE-2024-0009, CVE-2024-0010 and 60 more");

    [Test]
    public Task SingleCveIsNotPluralised() =>
        Verify(new Finding(Diagnostics.SdkCve, sdk, "8.0", Cves(1), "8.0.204").Body())
            .Snapshot("SDK 8.0.100 is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.204. CVEs: CVE-2024-0001");

    static List<Cve> Cves(int count) =>
        Enumerable.Range(1, count)
            .Select(_ => new Cve { Id = $"CVE-2024-{_:0000}", Url = $"https://example.invalid/{_}" })
            .ToList();
}
