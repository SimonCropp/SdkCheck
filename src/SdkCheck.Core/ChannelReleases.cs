namespace SdkCheck;

/// <summary>
/// One channel of https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json.
/// </summary>
/// <remarks>
/// Only the members used here are declared. Unknown JSON members are ignored, which is what keeps
/// this type usable as the cache format too: the live 8.0 feed is ~1.5 MB, almost all of it file
/// hashes and download urls, and round-tripping through these lean types writes a cache two orders
/// of magnitude smaller than the response it came from.
/// </remarks>
public class ChannelReleases
{
    [JsonPropertyName("channel-version")]
    public string ChannelVersion { get; set; } = "";

    [JsonPropertyName("latest-release")]
    public string? LatestRelease { get; set; }

    [JsonPropertyName("latest-sdk")]
    public string? LatestSdk { get; set; }

    [JsonPropertyName("latest-runtime")]
    public string? LatestRuntime { get; set; }

    /// <summary>
    /// "preview", "go-live", "active", "maintenance" or "eol".
    /// </summary>
    [JsonPropertyName("support-phase")]
    public string? SupportPhase { get; set; }

    /// <summary>
    /// ISO date, or null for a channel with no announced end of support (previews).
    /// </summary>
    [JsonPropertyName("eol-date")]
    public string? EolDate { get; set; }

    [JsonPropertyName("releases")]
    public List<Release> Releases { get; set; } = [];
}
