namespace SdkCheck;

class Memo(DateTime resolved, FeedResult result, int failures)
{
    public DateTime Resolved { get; } = resolved;
    public FeedResult Result { get; } = result;

    /// <summary>
    /// How many times in a row resolving this channel has failed, this one included. Carried across
    /// entries rather than derived, since each new entry replaces the one that counted before it.
    /// </summary>
    public int Failures { get; } = failures;

    public bool IsFresh(DateTime now, TimeSpan ttl) =>
        now - Resolved < ttl;
}
