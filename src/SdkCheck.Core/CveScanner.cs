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

        var fixedIn = SdkFix(component, channel, newerSecurity);

        return new(
            Diagnostics.SdkCve,
            component,
            channel.ChannelVersion,
            cves,
            fixedIn,
            crossesFeatureBand: CrossesFeatureBand(component.Version, fixedIn));
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
    static string? SdkFix(Component component, ChannelReleases channel, List<Release> newerSecurity)
    {
        var band = ReleaseVersion.FeatureBand(component.Version);
        var current = ReleaseVersion.ParseNumeric(component.Version);
        if (band == null ||
            current == null)
        {
            return channel.LatestSdk;
        }

        var newestSecurity = Newest(newerSecurity.Select(_ => _.ReleaseVersion));

        string? best = null;
        Version? bestParsed = null;

        foreach (var release in channel.Releases)
        {
            var releaseVersion = ReleaseVersion.ParseNumeric(release.ReleaseVersion);
            if (releaseVersion == null ||
                (newestSecurity != null && releaseVersion < newestSecurity))
            {
                continue;
            }

            foreach (var sdk in release.SdkVersions())
            {
                if (ReleaseVersion.IsPrerelease(sdk) ||
                    ReleaseVersion.FeatureBand(sdk) != band)
                {
                    continue;
                }

                var parsed = ReleaseVersion.ParseNumeric(sdk);
                if (parsed == null ||
                    parsed <= current)
                {
                    continue;
                }

                if (bestParsed == null ||
                    parsed > bestParsed)
                {
                    best = sdk;
                    bestParsed = parsed;
                }
            }
        }

        return best ?? channel.LatestSdk;
    }

    static Version? Newest(IEnumerable<string> versions)
    {
        Version? newest = null;
        foreach (var version in versions)
        {
            var parsed = ReleaseVersion.ParseNumeric(version);
            if (parsed != null &&
                (newest == null || parsed > newest))
            {
                newest = parsed;
            }
        }

        return newest;
    }

    static bool CrossesFeatureBand(string version, string? fixedIn)
    {
        var from = ReleaseVersion.FeatureBand(version);
        var to = ReleaseVersion.FeatureBand(fixedIn);

        return from != null &&
               to != null &&
               from != to;
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
