namespace SdkCheck;

/// <summary>
/// Checks a set of components against the release feed. Shared by the MSBuild task and the tool so
/// both reach the same verdict from the same data.
/// </summary>
public class SdkChecker(FeedOptions options, Action<string>? log = null)
{
    readonly ReleaseFeed feed = new(options, log);

    public IReadOnlyList<Finding> Check(IEnumerable<Component> components) =>
        Check(components, DateTime.UtcNow);

    public IReadOnlyList<Finding> Check(IEnumerable<Component> components, DateTime utcNow)
    {
        var findings = new List<Finding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Grouped by channel so a solution targeting several TFMs reads each channel once.
        var byChannel = components
            .Where(_ => !string.IsNullOrWhiteSpace(_.Version))
            .GroupBy(_ => ReleaseVersion.Channel(_.Version));

        foreach (var group in byChannel)
        {
            var channel = group.Key;
            if (channel == null)
            {
                continue;
            }

            var result = feed.Get(channel);
            if (result.Channel == null)
            {
                // One message per unreadable channel, not one per component on it.
                var first = group.First();
                Add(findings, seen, new(Diagnostics.Unavailable, first, channel, detail: result.Error));
                continue;
            }

            foreach (var component in group)
            {
                var finding = CveScanner.Scan(component, result.Channel, utcNow);
                if (finding != null)
                {
                    Add(findings, seen, finding);
                }
            }
        }

        return findings;
    }

    static void Add(List<Finding> findings, HashSet<string> seen, Finding finding)
    {
        // A self contained publish resolves the same runtime version as a shared framework one, and
        // several RID packs resolve the same version as each other. Report the version once.
        if (seen.Add($"{finding.Code}|{finding.Component.Version}"))
        {
            findings.Add(finding);
        }
    }
}
