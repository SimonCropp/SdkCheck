namespace SdkCheck;

public class Finding(
    string code,
    Component component,
    string channel,
    IReadOnlyList<Cve>? cves = null,
    string? fixedIn = null,
    string? eolDate = null,
    string? detail = null)
{
    public string Code { get; } = code;
    public Component Component { get; } = component;
    public string Channel { get; } = channel;
    public IReadOnlyList<Cve> Cves { get; } = cves ?? [];

    /// <summary>
    /// The latest SDK or runtime version on this channel - what to move to.
    /// </summary>
    public string? FixedIn { get; } = fixedIn;

    public string? EolDate { get; } = eolDate;

    /// <summary>
    /// Free text for <see cref="Diagnostics.Unavailable"/>: why the feed could not be read.
    /// </summary>
    public string? Detail { get; } = detail;

    public string Body()
    {
        if (Code == Diagnostics.Eol)
        {
            var when = EolDate == null ? "" : $" on {EolDate}";
            return
                $"""
                 The .NET {Channel} channel reached end of support{when}.
                 No further security patches will ship for {Component.Describe()}, so any CVE found in it from now on is unfixable in place.
                 Move to a supported channel.
                 """;
        }

        if (Code == Diagnostics.Unavailable)
        {
            return $"Release metadata for .NET {Channel} could not be read ({Detail}), so {Component.Describe()} was not checked.";
        }

        var upgrade = FixedIn == null ? "" : $" Update to {FixedIn}.";
        return
            $"{Component.Describe()} is affected by {Cves.Count} {(Cves.Count == 1 ? "CVE" : "CVEs")} published since it shipped, fixed in later .NET {Channel} releases.{upgrade} {ListCves()}";
    }

    public string Message() =>
        Diagnostics.Render(Code, Body());

    /// <summary>
    /// Every id, never a subset.
    /// </summary>
    /// <remarks>
    /// The count and the version to move to are already stated, and they are what drives the action -
    /// it is the same whether three CVEs apply or seventy. The ids are here for audit traceability,
    /// and a truncated audit list is the one form with no use: too long to skim, too short to rely
    /// on. The build log is also the artifact that gets kept and grepped, so withholding ids from it
    /// means the data exists only behind a separate tool invocation.
    /// </remarks>
    string ListCves() =>
        $"CVEs: {string.Join(", ", Cves.Select(_ => _.Id))}";
}
