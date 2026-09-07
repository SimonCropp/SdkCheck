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
    /// <summary>
    /// How many CVE ids to name inline before summarising the rest. A single .NET 8 SDK that is two
    /// years behind is affected by ~70, which is not a readable build warning.
    /// </summary>
    public const int MaxListedCves = 10;

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
            return $"The .NET {Channel} channel reached end of support{when}. No further security " +
                   $"patches will ship for {Component.Describe()}, so any CVE found in it from now on " +
                   "is unfixable in place. Move to a supported channel.";
        }

        if (Code == Diagnostics.Unavailable)
        {
            return $"Release metadata for .NET {Channel} could not be read ({Detail}), so " +
                   $"{Component.Describe()} was not checked.";
        }

        var upgrade = FixedIn == null ? "" : $" Update to {FixedIn}.";
        return $"{Component.Describe()} is affected by {Cves.Count} " +
               $"{(Cves.Count == 1 ? "CVE" : "CVEs")} published since it shipped, fixed in later " +
               $".NET {Channel} releases.{upgrade} {ListCves()}";
    }

    public string Message() =>
        Diagnostics.Render(Code, Body());

    string ListCves()
    {
        var ids = Cves.Select(_ => _.Id).ToList();
        if (ids.Count <= MaxListedCves)
        {
            return $"CVEs: {string.Join(", ", ids)}";
        }

        var listed = string.Join(", ", ids.Take(MaxListedCves));
        return $"CVEs: {listed} and {ids.Count - MaxListedCves} more";
    }
}
