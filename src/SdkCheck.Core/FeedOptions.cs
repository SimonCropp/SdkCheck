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
    /// Never touch the network. A cache older than <see cref="CacheTtl"/> is still used - stale data
    /// beats no data, and the alternative is silence.
    /// </summary>
    public bool Offline { get; set; }
}
