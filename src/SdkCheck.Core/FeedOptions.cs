namespace SdkCheck;

public class FeedOptions
{
    /// <summary>
    /// A directory of "{channel}.json" files to read instead of the network - the escape hatch that
    /// lets the integration suite run offline and deterministically.
    /// </summary>
    public string? OverrideDirectory { get; set; }

    public string? CacheDirectory { get; set; }

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a channel that could not be read is remembered as unreadable. Deliberately far
    /// shorter than <see cref="CacheTtl"/>: that one exists so a forty-project build reads a good
    /// feed once, while applying it to a failure would let one dropped connection silence the check
    /// until the build node exits, and MSBuild nodes outlive the build that started them.
    /// </summary>
    public TimeSpan FailureTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Never touch the network. A cache older than <see cref="CacheTtl"/> is still used - stale data
    /// beats no data, and the alternative is silence.
    /// </summary>
    public bool Offline { get; set; }
}
