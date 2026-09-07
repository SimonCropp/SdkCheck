namespace SdkCheck;

public class FeedResult(ChannelReleases? channel = null, string? error = null)
{
    public ChannelReleases? Channel { get; } = channel;

    /// <summary>
    /// Why the channel could not be read. Null when <see cref="Channel"/> is set.
    /// </summary>
    public string? Error { get; } = error;
}
