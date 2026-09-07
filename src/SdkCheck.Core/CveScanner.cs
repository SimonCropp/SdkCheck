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

        var newerSecurity = NewerSecurityReleases(channel, shippedVersion).ToList();

        var cves = newerSecurity
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

        var (fixedIn, crossesFeatureBand) = SdkFix(component, channel, newerSecurity);

        return new(
            Diagnostics.SdkCve,
            component,
            channel.ChannelVersion,
            cves,
            fixedIn,
            crossesFeatureBand: crossesFeatureBand);
    }

    /// <summary>
    /// The SDK version to move to.
    /// </summary>
    /// <remarks>
    /// Naming the channel's latest tells anyone on an older feature band to make a change they may
    /// not be able to make: a global.json pinned to 8.0.1xx does not roll to 8.0.4xx. The newest SDK
    /// on the component's own band is named instead - but only when it shipped in a release at least
    /// as new as the last security release counted above, because an older one carries some of the
    /// fixes and not the rest, and naming it would report the problem as solved when it is not. A
    /// band that has stopped shipping leaves the channel's latest as the only honest answer, and
    /// <see cref="Finding.CrossesFeatureBand"/> then says so in the message.
    /// </remarks>
    static (string? FixedIn, bool CrossesFeatureBand) SdkFix(
        Component component,
        ChannelReleases channel,
        List<Release> newerSecurity)
    {
        var current = ReleaseVersion.ParseNumeric(component.Version);
        if (current == null)
        {
            // Not a version Version parses, so there is no band to hold the recommendation to.
            return (channel.LatestSdk, false);
        }

        var band = ReleaseVersion.FeatureBand(current);
        if (band == null)
        {
            // No patch component, so the same again.
            return (channel.LatestSdk, false);
        }

        // Never null: NewerSecurityReleases keeps only releases whose version parsed, and Scan
        // returns before here unless one of them listed a CVE.
        var newestSecurity = newerSecurity.Max(_ => ReleaseVersion.ParseNumeric(_.ReleaseVersion))!;

        var best = BandFixes(channel, current, band.Value, newestSecurity)
            .OrderByDescending(_ => _.Parsed)
            .Select(_ => _.Sdk)
            .FirstOrDefault();

        if (best != null)
        {
            // Selected on the component's own band, so it cannot be a band change.
            return (best, false);
        }

        var latestBand = ReleaseVersion.FeatureBand(channel.LatestSdk);

        return (channel.LatestSdk, latestBand != null && latestBand != band);
    }

    /// <summary>
    /// The stable SDKs on <paramref name="band"/> newer than <paramref name="current"/>, from the
    /// releases at least as new as <paramref name="newestSecurity"/>. Each carries the version it
    /// parsed to, so no version string here is parsed twice.
    /// </summary>
    static IEnumerable<(string Sdk, Version Parsed)> BandFixes(
        ChannelReleases channel,
        Version current,
        int band,
        Version newestSecurity)
    {
        foreach (var release in channel.Releases)
        {
            var releaseVersion = ReleaseVersion.ParseNumeric(release.ReleaseVersion);
            if (releaseVersion == null ||
                releaseVersion < newestSecurity)
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
                    yield return (sdk, parsed);
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
