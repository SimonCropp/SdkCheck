namespace SdkCheck;

public static class CveScanner
{
    /// <summary>
    /// Decides whether a component is affected by anything published against it.
    /// </summary>
    public static Finding? Scan(Component component, ChannelReleases channel, DateTime utcNow)
    {
        // End of support is tested first, and deliberately without reference to the component's
        // version. On a dead channel there are no newer security releases, so the "am I behind a
        // security release?" test below reports nothing - it would go permanently silent at exactly
        // the point exposure stops being fixable.
        if (IsEol(channel, utcNow))
        {
            return new(
                Diagnostics.Eol,
                component,
                channel.ChannelVersion,
                eolDate: channel.EolDate);
        }

        var shipped = FindShippingRelease(component, channel);
        if (shipped == null)
        {
            // A preview, or newer than the feed. Either way there is nothing published against it.
            return null;
        }

        var shippedVersion = ReleaseVersion.ParseNumeric(shipped.ReleaseVersion);
        if (shippedVersion == null)
        {
            return null;
        }

        // Only the releases that named a CVE, not every release flagged security. A security release
        // with an empty cve-list has nothing to be behind, and counting it would raise the bar an
        // SDK has to clear and reject one that carries every fix reported.
        var fixes = NewerSecurityReleases(channel, shippedVersion)
            .Where(_ => _.CveList.Any(cve => !string.IsNullOrWhiteSpace(cve.Id)))
            .ToList();

        var cves = fixes
            .SelectMany(_ => _.CveList)
            .Where(_ => !string.IsNullOrWhiteSpace(_.Id))
            .GroupBy(_ => _.Id, StringComparer.OrdinalIgnoreCase)
            .Select(_ => _.First())
            .OrderBy(_ => _.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cves.Count == 0)
        {
            return null;
        }

        if (!component.IsSdk)
        {
            return new(
                Diagnostics.RuntimeCve,
                component,
                channel.ChannelVersion,
                cves,
                channel.LatestRuntime);
        }

        var (fixedIn, crossesFeatureBand, bandNewest) = SdkFix(component, channel, fixes);

        return new(
            Diagnostics.SdkCve,
            component,
            channel.ChannelVersion,
            cves,
            fixedIn,
            crossesFeatureBand: crossesFeatureBand,
            bandNewest: bandNewest);
    }

    /// <summary>
    /// The SDK version to move to, given the releases that fixed the CVEs being reported.
    /// </summary>
    /// <remarks>
    /// Naming the channel's latest tells anyone on an older feature band to make a change they may
    /// not be able to make: a global.json pinned to 8.0.1xx does not roll to 8.0.4xx. The newest SDK
    /// on the component's own band is named instead - but only when it shipped in a release at least
    /// as new as the last release that contributed one of the CVEs listed, because an older one
    /// carries some of them and not the rest, and naming it would report the problem as solved when
    /// it is not.
    /// <para>
    /// Both reasons the band cannot be held to end at the channel's latest, so what the search found
    /// on the band comes back with it: <see cref="Finding.BandNewest"/> is the version that was there
    /// and was too old, or null when the band has stopped shipping. The message states which, rather
    /// than asserting the one that reads better.
    /// </para>
    /// </remarks>
    static (string? FixedIn, bool CrossesFeatureBand, string? BandNewest) SdkFix(
        Component component,
        ChannelReleases channel,
        List<Release> fixes)
    {
        var current = ReleaseVersion.ParseNumeric(component.Version);
        if (current == null)
        {
            // Not a version Version parses, so there is no band to hold the recommendation to.
            return (channel.LatestSdk, false, null);
        }

        var band = ReleaseVersion.FeatureBand(current);
        if (band == null)
        {
            // No patch component, so the same again.
            return (channel.LatestSdk, false, null);
        }

        // Never null: NewerSecurityReleases keeps only releases whose version parsed, and Scan
        // returns before here unless one of them listed a CVE.
        var lastFix = fixes.Max(_ => ReleaseVersion.ParseNumeric(_.ReleaseVersion))!;

        var onBand = BandSdks(channel, current, band.Value)
            .OrderByDescending(_ => _.Parsed)
            .ToList();

        var best = onBand
            .Where(_ => _.Release >= lastFix)
            .Select(_ => _.Sdk)
            .FirstOrDefault();

        if (best != null)
        {
            // Selected on the component's own band, so it cannot be a band change.
            return (best, false, null);
        }

        var latestBand = ReleaseVersion.FeatureBand(channel.LatestSdk);

        return (
            channel.LatestSdk,
            latestBand != null && latestBand != band,
            onBand.Select(_ => _.Sdk).FirstOrDefault());
    }

    /// <summary>
    /// The stable SDKs on <paramref name="band"/> newer than <paramref name="current"/>, each with
    /// the version it parsed to and the version of the release that shipped it. Nothing is dropped
    /// for being too old here: the ones that are too old to carry every fix are what the message
    /// reports when the band cannot be held to.
    /// </summary>
    static IEnumerable<(string Sdk, Version Parsed, Version Release)> BandSdks(
        ChannelReleases channel,
        Version current,
        int band)
    {
        foreach (var release in channel.Releases)
        {
            var releaseVersion = ReleaseVersion.ParseNumeric(release.ReleaseVersion);
            if (releaseVersion == null)
            {
                continue;
            }

            foreach (var sdk in release.SdkVersions())
            {
                // Parsing keeps only the numeric part, so an rc would otherwise pass for the stable
                // it led to.
                if (ReleaseVersion.IsPrerelease(sdk))
                {
                    continue;
                }

                var parsed = ReleaseVersion.ParseNumeric(sdk);
                if (parsed != null &&
                    parsed > current &&
                    ReleaseVersion.FeatureBand(parsed) == band)
                {
                    yield return (sdk, parsed, releaseVersion);
                }
            }
        }
    }

    public static bool IsEol(ChannelReleases channel, DateTime utcNow)
    {
        if (string.Equals(channel.SupportPhase, "eol", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DateTime.TryParse(
                   channel.EolDate,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                   out var eol) &&
               eol < utcNow;
    }

    static Release? FindShippingRelease(Component component, ChannelReleases channel) =>
        channel.Releases.FirstOrDefault(
            _ =>
            {
                if (component.IsSdk)
                {
                    return _.ShipsSdk(component.Version);
                }

                return _.ShipsRuntime(component.Version);
            });

    static IEnumerable<Release> NewerSecurityReleases(ChannelReleases channel, Version shipped) =>
        channel.Releases
            .Where(_ => _.Security)
            // Prereleases are skipped rather than compared. A prerelease release-version is always
            // older than the stable it led to, and feeding one to Version.Parse throws.
            .Where(_ => !ReleaseVersion.IsPrerelease(_.ReleaseVersion))
            .Where(_ =>
            {
                var version = ReleaseVersion.ParseNumeric(_.ReleaseVersion);
                return version != null && version > shipped;
            });
}
