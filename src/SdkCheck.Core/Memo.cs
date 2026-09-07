namespace SdkCheck;

class Memo(DateTime resolved, FeedResult result)
{
    public DateTime Resolved { get; } = resolved;
    public FeedResult Result { get; } = result;

    public bool IsFresh(DateTime now, TimeSpan ttl) =>
        now - Resolved < ttl;
}
