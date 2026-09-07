namespace SdkCheck;

class Memo(DateTime resolved, FeedResult result)
{
    public DateTime Resolved { get; } = resolved;
    public FeedResult Result { get; } = result;
}
