namespace testing;

public static class Feeds
{
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Deserializes fresh on every call rather than going through ReleaseFeed, whose process wide
    /// memo would hand every test the same instance - and tests that mutate a channel to exercise
    /// an end-of-support variant would then corrupt it for everything that ran after them.
    /// </summary>
    public static ChannelReleases Load(string channel)
    {
        var path = Path.Combine(Directory, $"{channel}.json");
        return JsonSerializer.Deserialize<ChannelReleases>(File.ReadAllText(path)) ??
               throw new InvalidOperationException($"Could not load '{path}'.");
    }
}
