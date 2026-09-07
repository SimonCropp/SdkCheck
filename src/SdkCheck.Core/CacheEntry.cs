namespace SdkCheck;

class CacheEntry
{
    [JsonPropertyName("fetched")]
    public DateTime Fetched { get; set; }

    [JsonPropertyName("channel")]
    public ChannelReleases? Channel { get; set; }
}
