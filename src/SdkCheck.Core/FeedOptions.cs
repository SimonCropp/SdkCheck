namespace SdkCheck;

public class FeedOptions
{
    /// <summary>
    /// A directory of "{channel}.json" files to read instead of the network - the escape hatch that
    /// lets the integration suite run offline and deterministically.
    /// </summary>
    public string? OverrideDirectory { get; set; }

    public string? CacheDirectory { get; set; }

    /// <summary>
    /// The root the per-channel "{channel}/releases.json" documents hang off. Overridable so a build
    /// can point at a mirror, and so tests can exercise the unreachable-feed paths without the
    /// network.
    /// </summary>
    public string BaseUrl { get; set; } = "https://builds.dotnet.microsoft.com/dotnet/release-metadata";

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the first fetch failure is remembered, whether it left nothing at all or fell back to
    /// an expired cache. Deliberately far shorter than <see cref="CacheTtl"/>: that one exists so a
    /// forty-project build reads a good feed once, while applying it to a failure would let one
    /// dropped connection silence the check until the build node exits, and MSBuild nodes outlive the
    /// build that started them.
    ///
    /// Each consecutive failure after the first doubles the wait, up to <see cref="CacheTtl"/>, so a
    /// feed that is unreachable for the length of a build is not retried once a minute for all of it.
    /// </summary>
    public TimeSpan FailureTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Never touch the network. A cache older than <see cref="CacheTtl"/> is still used - stale data
    /// beats no data, and the alternative is silence.
    /// </summary>
    public bool Offline { get; set; }
}
