namespace SdkCheck;

// Source generated rather than reflection based, so the netstandard2.0 leg carries no reflection
// dependency into whatever MSBuild process loads it.
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChannelReleases))]
[JsonSerializable(typeof(CacheEntry))]
partial class FeedJsonContext : JsonSerializerContext;
