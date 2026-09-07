namespace SdkCheck;

public class Release
{
    [JsonPropertyName("release-version")]
    public string ReleaseVersion { get; set; } = "";

    [JsonPropertyName("release-date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("security")]
    public bool Security { get; set; }

    [JsonPropertyName("cve-list")]
    public List<Cve> CveList { get; set; } = [];

    [JsonPropertyName("runtime")]
    public ComponentVersion? Runtime { get; set; }

    /// <summary>
    /// The newest SDK feature band shipped by this release.
    /// </summary>
    [JsonPropertyName("sdk")]
    public ComponentVersion? Sdk { get; set; }

    /// <summary>
    /// Every SDK feature band shipped by this release. One release commonly ships several - 8.0.30
    /// carries 8.0.424 and 8.0.315 - so matching an installed SDK against <see cref="Sdk"/> alone
    /// misses anyone on an older band.
    /// </summary>
    [JsonPropertyName("sdks")]
    public List<ComponentVersion> Sdks { get; set; } = [];

    [JsonPropertyName("aspnetcore-runtime")]
    public ComponentVersion? AspNetCoreRuntime { get; set; }

    [JsonPropertyName("windowsdesktop")]
    public ComponentVersion? WindowsDesktop { get; set; }

    /// <summary>
    /// Whether this release shipped the given SDK, on any band.
    /// </summary>
    /// <remarks>
    /// Walking <see cref="SdkVersions"/> rather than <see cref="Sdk"/> and <see cref="Sdks"/> again
    /// keeps one definition of what this release shipped. The one input the two forms answer
    /// differently is a blank version, which a malformed feed can carry in either member: it matches
    /// nothing here, where a second walk would have it match every release whose "sdk" has no version
    /// in it. Callers drop blank versions before this, so nothing real reaches the difference.
    /// </remarks>
    public bool ShipsSdk(string version) =>
        SdkVersions().Any(_ => string.Equals(_, version, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every SDK version this release shipped, newest band first, skipping any member the feed left
    /// without a version. Duplicates are possible, since <see cref="Sdk"/> is also the first entry of
    /// <see cref="Sdks"/> in every real feed.
    /// </summary>
    public IEnumerable<string> SdkVersions()
    {
        if (Sdk != null &&
            !string.IsNullOrWhiteSpace(Sdk.Version))
        {
            yield return Sdk.Version;
        }

        foreach (var sdk in Sdks)
        {
            if (!string.IsNullOrWhiteSpace(sdk.Version))
            {
                yield return sdk.Version;
            }
        }
    }

    public bool ShipsRuntime(string version) =>
        Matches(Runtime, version) ||
        Matches(AspNetCoreRuntime, version) ||
        Matches(WindowsDesktop, version);

    static bool Matches(ComponentVersion? component, string version) =>
        component != null &&
        string.Equals(component.Version, version, StringComparison.OrdinalIgnoreCase);
}
