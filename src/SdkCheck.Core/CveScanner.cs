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

        var cves = NewerSecurityReleases(channel, shippedVersion)
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

        return new(
            component.IsSdk ? Diagnostics.SdkCve : Diagnostics.RuntimeCve,
            component,
            channel.ChannelVersion,
            cves,
            component.IsSdk ? channel.LatestSdk : channel.LatestRuntime);
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
            _ => component.IsSdk
                ? _.ShipsSdk(component.Version)
                : _.ShipsRuntime(component.Version));

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
