public class CveScannerTests
{
    static readonly DateTime now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 8.0.100 is on the 1xx band, so the version to move to is 8.0.106 - the 1xx SDK shipped by the
    /// newest security release - rather than the channel's latest 8.0.204, which a global.json
    /// pinned to the band cannot roll to.
    /// </summary>
    [Test]
    public async Task SdkBehindTwoSecurityReleases()
    {
        var finding = Scan(new(ComponentKind.Sdk, "8.0.100"));

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.SdkCve);
        await Assert.That(finding.FixedIn).IsEqualTo("8.0.106");
        await Assert.That(finding.CrossesFeatureBand).IsFalse();
        await Verify(finding.Cves.Select(_ => _.Id))
            .Snapshot(
                """
                [
                  CVE-2024-0002,
                  CVE-2024-0003,
                  CVE-2024-0004
                ]
                """);
    }

    /// <summary>
    /// 8.0.4 and 8.0.2 both list CVE-2024-0004. It should be named once.
    /// </summary>
    [Test]
    public async Task CvesAreDeduplicated()
    {
        var ids = Scan(new(ComponentKind.Sdk, "8.0.100"))!.Cves.Select(_ => _.Id).ToList();

        await Assert.That(ids.Distinct().Count()).IsEqualTo(ids.Count);
    }

    /// <summary>
    /// The 8.0 fixture carries a security release 8.0.5-rc.1 whose numeric part is higher than every
    /// stable release. If prereleases were compared rather than skipped, its CVE would leak into
    /// every result - and feeding its version to Version.Parse would throw first.
    /// </summary>
    [Test]
    public async Task PrereleaseSecurityReleasesAreIgnored()
    {
        var finding = Scan(new(ComponentKind.Sdk, "8.0.100"))!;

        await Assert.That(finding.Cves.Select(_ => _.Id)).DoesNotContain("CVE-2024-PRERELEASE");
    }

    [Test]
    public async Task LatestSdkIsClean() =>
        await Assert.That(Scan(new(ComponentKind.Sdk, "8.0.204"))).IsNull();

    /// <summary>
    /// 8.0.106 is the older feature band shipped alongside 8.0.204 by release 8.0.4. Matching only
    /// against the release's "sdk" member would miss it and report it as unknown.
    /// </summary>
    [Test]
    public async Task OlderFeatureBandOfTheLatestReleaseIsClean() =>
        await Assert.That(Scan(new(ComponentKind.Sdk, "8.0.106"))).IsNull();

    [Test]
    public async Task OlderFeatureBandBehindASecurityRelease()
    {
        var finding = Scan(new(ComponentKind.Sdk, "8.0.104"));

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.SdkCve);
        await Verify(finding.Cves.Select(_ => _.Id))
            .Snapshot(
                """
                [
                  CVE-2024-0004
                ]
                """);
    }

    /// <summary>
    /// A band that has stopped shipping has nothing to move to on itself, so the channel's latest is
    /// named and the message says the band changes.
    /// </summary>
    [Test]
    public async Task DiscontinuedFeatureBandFallsBackToTheChannelLatest()
    {
        var channel = Feeds.Load("8.0");
        // 8.0.300 shipped with the original release and never again.
        channel.Releases.Single(_ => _.ReleaseVersion == "8.0.0").Sdks.Add(new() { Version = "8.0.300" });

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "8.0.300"), channel, now);

        await Assert.That(finding!.FixedIn).IsEqualTo("8.0.204");
        await Assert.That(finding.CrossesFeatureBand).IsTrue();
    }

    /// <summary>
    /// The newest SDK on the band is only the answer when it shipped in a release at least as new as
    /// the last security release counted. 8.0.105 came with 8.0.3, before the security release 8.0.4,
    /// so naming it would report a fix that carries only some of the CVEs.
    /// </summary>
    [Test]
    public async Task StaleFeatureBandIsNotNamedAsTheFix()
    {
        var channel = Feeds.Load("8.0");
        channel.Releases.Single(_ => _.ReleaseVersion == "8.0.4").Sdks.RemoveAll(_ => _.Version == "8.0.106");

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "8.0.100"), channel, now);

        await Assert.That(finding!.FixedIn).IsEqualTo("8.0.204");
        await Assert.That(finding.CrossesFeatureBand).IsTrue();
    }

    /// <summary>
    /// A feed value the version parser cannot make sense of has to come back as a message rather
    /// than an exception - nothing this package discovers may fail a build. There is no band to hold
    /// the recommendation to, so the channel's latest is named, and nothing claims a band change.
    /// </summary>
    [Test]
    public async Task SdkVersionThatDoesNotParseFallsBackToTheChannelLatest()
    {
        var channel = Feeds.Load("8.0");
        channel.Releases.Single(_ => _.ReleaseVersion == "8.0.0").Sdks.Add(new() { Version = "8.0.banana" });

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "8.0.banana"), channel, now);

        await Assert.That(finding!.FixedIn).IsEqualTo("8.0.204");
        await Assert.That(finding.CrossesFeatureBand).IsFalse();
    }

    /// <summary>
    /// The same for a version with no patch component. "8.0" parses, but sits on no feature band, so
    /// there is again nothing to hold the fix to and nothing to say has changed.
    /// </summary>
    [Test]
    public async Task SdkVersionWithNoFeatureBandFallsBackToTheChannelLatest()
    {
        var channel = Feeds.Load("8.0");
        channel.Releases.Single(_ => _.ReleaseVersion == "8.0.0").Sdks.Add(new() { Version = "8.0" });

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "8.0"), channel, now);

        await Assert.That(finding!.FixedIn).IsEqualTo("8.0.204");
        await Assert.That(finding.CrossesFeatureBand).IsFalse();
    }

    [Test]
    public async Task UnknownVersionIsNotAFinding() =>
        await Assert.That(Scan(new(ComponentKind.Sdk, "8.0.999"))).IsNull();

    [Test]
    public async Task RuntimeUsesTheRuntimeCode()
    {
        var finding = Scan(new(ComponentKind.Runtime, "8.0.2"));

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.RuntimeCve);
        await Assert.That(finding.FixedIn).IsEqualTo("8.0.4");
    }

    [Test]
    public async Task AspNetCoreAndWindowsDesktopVersionsResolve()
    {
        await Assert.That(Scan(new(ComponentKind.AspNetCoreRuntime, "8.0.0"))).IsNotNull();
        await Assert.That(Scan(new(ComponentKind.WindowsDesktopRuntime, "8.0.0"))).IsNotNull();
    }

    /// <summary>
    /// The trap case. In the 6.0 fixture the component being checked is the newest release on the
    /// channel, so there is nothing newer to compare against - end of support has to be decided
    /// before the version comparison or this reports nothing at all.
    /// </summary>
    [Test]
    public async Task EolChannelReportsEvenWhenNothingIsNewer()
    {
        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "6.0.428"), Feeds.Load("6.0"), now);

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.Eol);
        await Assert.That(finding.EolDate).IsEqualTo("2024-11-12");
    }

    [Test]
    public async Task EolByDateAlone()
    {
        var channel = Feeds.Load("6.0");
        channel.SupportPhase = "maintenance";

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "6.0.428"), channel, now);

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.Eol);
    }

    [Test]
    public async Task EolByPhaseAlone()
    {
        var channel = Feeds.Load("6.0");
        channel.EolDate = null;

        var finding = CveScanner.Scan(new(ComponentKind.Sdk, "6.0.428"), channel, now);

        await Assert.That(finding!.Code).IsEqualTo(Diagnostics.Eol);
    }

    /// <summary>
    /// An eol-date in the future is a supported channel, not an expired one.
    /// </summary>
    [Test]
    public async Task FutureEolDateIsNotEol() =>
        await Assert.That(CveScanner.IsEol(Feeds.Load("8.0"), now)).IsFalse();

    [Test]
    public async Task EolIsDecidedAgainstTheSuppliedTime()
    {
        var channel = Feeds.Load("6.0");
        channel.SupportPhase = "maintenance";

        var beforeEol = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Assert.That(CveScanner.IsEol(channel, beforeEol)).IsFalse();
        await Assert.That(CveScanner.IsEol(channel, now)).IsTrue();
    }

    static Finding? Scan(Component component) =>
        CveScanner.Scan(component, Feeds.Load("8.0"), now);
}
