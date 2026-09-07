namespace SdkCheck;

public class Finding(
    string code,
    Component component,
    string channel,
    IReadOnlyList<Cve>? cves = null,
    string? fixedIn = null,
    string? eolDate = null,
    string? detail = null,
    bool crossesFeatureBand = false,
    string? bandNewest = null)
{
    public string Code { get; } = code;
    public Component Component { get; } = component;
    public string Channel { get; } = channel;
    public IReadOnlyList<Cve> Cves { get; } = cves ?? [];

    /// <summary>
    /// The version to move to. The channel's latest for a runtime, and for an SDK the newest on the
    /// band in use when that carries every CVE listed - 8.0.100 is sent to 8.0.106 while the
    /// channel's latest is 8.0.204, since a global.json pinned to the band cannot roll to it.
    /// <see cref="CrossesFeatureBand"/> is set when the band could not be held to after all.
    /// </summary>
    public string? FixedIn { get; } = fixedIn;

    /// <summary>
    /// Whether <see cref="FixedIn"/> sits on a different SDK feature band than the component, in
    /// either direction: the channel's latest can be on an earlier band than a component sitting on
    /// one the channel has since stopped shipping. A reader pinned to a band has more to change than
    /// a version number, so the message says so, and <see cref="BandNewest"/> says why the band could
    /// not be held to.
    /// </summary>
    public bool CrossesFeatureBand { get; } = crossesFeatureBand;

    /// <summary>
    /// The newest SDK on the component's own feature band, when one shipped and was still not named
    /// as the fix: it came before the last release that fixed one of the CVEs listed, so it carries
    /// some of them and not the rest. Null when the band has stopped shipping, which is the other
    /// reason <see cref="FixedIn"/> can leave the band.
    /// </summary>
    public string? BandNewest { get; } = bandNewest;

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

        var upgrade = "";
        if (FixedIn != null)
        {
            upgrade = $" Update to {FixedIn}{BandNote()}.";
        }
        return
            $"""
             {Component.Describe()} is affected by {Cves.Count} {(Cves.Count == 1 ? "CVE" : "CVEs")} published since it shipped, fixed in later .NET {Channel} releases.{upgrade}
             {ListCves()}
             """;
    }

    /// <summary>
    /// Why the version named is not one on the component's own feature band, in the cases where it
    /// is not. The two reasons end at the same version and read the same to anyone pinned to the
    /// band, but they are not the same thing: a band that has stopped shipping offers nothing at all,
    /// while one whose newest predates the last security release offers a version that carries part
    /// of the list. Naming it leaves that choice with the reader.
    /// </summary>
    string BandNote()
    {
        var band = CrossesFeatureBand ? ", on a different feature band" : "";

        if (BandNewest != null)
        {
            return $"{band}: the band in use stops at {BandNewest}, which shipped before the last of these fixes";
        }

        if (CrossesFeatureBand)
        {
            return $"{band}: nothing newer shipped on the band in use";
        }

        return "";
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
        $"""
         CVEs:
         {string.Join('\n', Cves.Select(_ => $" * {_.Id}"))}
         """;
}
