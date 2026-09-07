namespace SdkCheck;

public class FeedResult(ChannelReleases? channel = null, string? error = null, bool fetchFailed = false)
{
    public ChannelReleases? Channel { get; } = channel;

    /// <summary>
    /// Why the channel could not be read. Null when <see cref="Channel"/> is set.
    /// </summary>
    public string? Error { get; } = error;

    /// <summary>
    /// The network was tried and failed. <see cref="Channel"/>, when set alongside this, came from an
    /// expired cache: usable, but not a reason to stop trying the network, so the result is held only
    /// for <see cref="FeedOptions.FailureTtl"/>. Offline resolution never sets this - no fetch was
    /// attempted, so there is nothing to retry.
    /// </summary>
    public bool FetchFailed { get; } = fetchFailed;
}
