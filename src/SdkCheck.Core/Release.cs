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

    public bool ShipsSdk(string version)
    {
        if (Sdk != null &&
            string.Equals(Sdk.Version, version, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Sdks.Any(_ => string.Equals(_.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    public bool ShipsRuntime(string version) =>
        Matches(Runtime, version) ||
        Matches(AspNetCoreRuntime, version) ||
        Matches(WindowsDesktop, version);

    static bool Matches(ComponentVersion? component, string version) =>
        component != null &&
        string.Equals(component.Version, version, StringComparison.OrdinalIgnoreCase);
}
