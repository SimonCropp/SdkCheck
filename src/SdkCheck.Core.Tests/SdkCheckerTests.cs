public class SdkCheckerTests
{
    static readonly DateTime now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public Task SdkAndRuntimeTogether() =>
        Verify(Check(
                new Component(ComponentKind.Sdk, "8.0.100"),
                new Component(ComponentKind.Runtime, "8.0.2")))
            .Snapshot(
                """
                [
                  SdkCheck001: SDK 8.0.100 is affected by 3 CVEs published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.106.
                CVEs:
                 * CVE-2024-0002
                 * CVE-2024-0003
                 * CVE-2024-0004,
                  SdkCheck003: runtime 8.0.2 is affected by 1 CVE published since it shipped, fixed in later .NET 8.0 releases. Update to 8.0.4.
                CVEs:
                 * CVE-2024-0004
                ]
                """);

    [Test]
    public async Task NothingToReport() =>
        await Assert.That(Check(new Component(ComponentKind.Sdk, "8.0.204"))).IsEmpty();

    /// <summary>
    /// A self-contained publish resolves the same runtime version for several RID packs, and the
    /// shared framework reference resolves it again. One report, not four.
    /// </summary>
    [Test]
    public async Task RepeatedVersionsAreReportedOnce()
    {
        var findings = Check(
            new Component(ComponentKind.Runtime, "8.0.0", "Microsoft.NETCore.App"),
            new Component(ComponentKind.RuntimePack, "8.0.0", "win-x64"),
            new Component(ComponentKind.RuntimePack, "8.0.0", "linux-x64"),
            new Component(ComponentKind.RuntimePack, "8.0.0", "osx-arm64"));

        await Assert.That(findings).HasSingleItem();
    }

    [Test]
    public async Task ComponentsWithNoVersionAreSkipped() =>
        await Assert.That(Check(new Component(ComponentKind.Sdk, ""))).IsEmpty();

    [Test]
    public async Task UnparseableVersionsAreSkipped() =>
        await Assert.That(Check(new Component(ComponentKind.Sdk, "latest"))).IsEmpty();

    /// <summary>
    /// An unreadable channel is reported once, not once per component sitting on it, and never as
    /// anything the consumer's build could fail on.
    /// </summary>
    [Test]
    public async Task UnreadableChannelIsReportedOncePerChannel()
    {
        using var cache = new TempDirectory();
        ReleaseFeed.ResetMemo();
        var checker = new SdkChecker(new()
        {
            CacheDirectory = cache,
            Offline = true
        });

        var findings = checker.Check(
            [
                new(ComponentKind.Sdk, "8.0.100"),
                new(ComponentKind.Runtime, "8.0.2"),
                new(ComponentKind.Runtime, "8.0.0")
            ],
            now);

        await Assert.That(findings).HasSingleItem();
        await Assert.That(findings[0].Code).IsEqualTo(Diagnostics.Unavailable);
    }

    [Test]
    public Task EolChannel() =>
        Verify(Check(new Component(ComponentKind.Sdk, "6.0.428")))
            .Snapshot(
                """
                [
                  SdkCheck002: The .NET 6.0 channel reached end of support on 2024-11-12.
                No further security patches will ship for SDK 6.0.428, so any CVE found in it from now on is unfixable in place.
                Move to a supported channel.
                ]
                """);

    static IReadOnlyList<string> Check(params Component[] components)
    {
        ReleaseFeed.ResetMemo();
        var checker = new SdkChecker(new()
        {
            OverrideDirectory = Feeds.Directory
        });
        return checker.Check(components, now)
            .Select(_ => $"{_.Code}: {_.Body()}")
            .ToList();
    }
}
