namespace SdkCheck;

class Memo(DateTime resolved, FeedResult result, TimeSpan ttl)
{
    public DateTime Resolved { get; } = resolved;
    public FeedResult Result { get; } = result;

    /// <summary>
    /// Carried per entry rather than read from the options at use, since a success and a failure are
    /// held for very different lengths of time.
    /// </summary>
    public TimeSpan Ttl { get; } = ttl;

    public bool IsFresh(DateTime now) =>
        now - Resolved < Ttl;
}
