namespace testing;

public class ReleaseFeedTests
{
    [Test]
    public async Task OverrideDirectoryIsRead()
    {
        var result = Feed(new() { OverrideDirectory = Feeds.Directory }).Get("8.0");

        await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task MissingOverrideIsReportedNotThrown()
    {
        var result = Feed(new() { OverrideDirectory = Feeds.Directory }).Get("1.0");

        await Assert.That(result.Channel).IsNull();
        await Assert.That(result.Error).Contains("no override feed");
    }

    [Test]
    public async Task UnreadableOverrideIsReportedNotThrown()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory, "8.0.json"), "{ not json");

        var result = Feed(new() { OverrideDirectory = directory }).Get("8.0");

        await Assert.That(result.Channel).IsNull();
        await Assert.That(result.Error).Contains("unreadable");
    }

    [Test]
    public async Task FreshCacheIsUsedWithoutTheNetwork()
    {
        using var cache = new TempDirectory();
        WriteCache(cache, "8.0", DateTime.UtcNow);

        // Offline, so reaching the network at all would fail this rather than silently pass.
        var result = Feed(new() { CacheDirectory = cache, Offline = true }).Get("8.0");

        await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
    }

    /// <summary>
    /// Stale beats nothing. When the feed cannot be reached, an expired cache is the only thing that
    /// can say anything at all, and saying nothing is the failure mode this package exists to avoid.
    /// </summary>
    [Test]
    public async Task ExpiredCacheIsStillUsedWhenOffline()
    {
        using var cache = new TempDirectory();
        WriteCache(cache, "8.0", DateTime.UtcNow.AddDays(-30));

        var result = Feed(new() { CacheDirectory = cache, Offline = true }).Get("8.0");

        await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
    }

    [Test]
    public async Task OfflineWithNoCacheIsReportedNotThrown()
    {
        using var cache = new TempDirectory();

        var result = Feed(new() { CacheDirectory = cache, Offline = true }).Get("8.0");

        await Assert.That(result.Channel).IsNull();
        await Assert.That(result.Error).Contains("offline");
    }

    /// <summary>
    /// A half written or truncated cache file is a reason to re-fetch, never to fail.
    /// </summary>
    [Test]
    public async Task CorruptCacheIsIgnored()
    {
        using var cache = new TempDirectory();
        File.WriteAllText(Path.Combine(cache, "8.0.json"), "{\"fetched\":\"2024");

        var result = Feed(new() { CacheDirectory = cache, Offline = true }).Get("8.0");

        await Assert.That(result.Channel).IsNull();
        await Assert.That(result.Error).Contains("offline");
    }

    [Test]
    public async Task CacheDirectoryThatCannotBeCreatedIsSurvivable()
    {
        // A file where the directory should be: creating the cache directory throws, and the check
        // still has to complete.
        using var directory = new TempDirectory();
        var blocked = Path.Combine(directory, "blocked");
        File.WriteAllText(blocked, "");

        var result = Feed(new() { CacheDirectory = blocked, Offline = true }).Get("8.0");

        await Assert.That(result.Channel).IsNull();
        await Assert.That(result.Error).IsNotNull();
    }

    static ReleaseFeed Feed(FeedOptions options)
    {
        // The memo is process wide, so tests would otherwise see one another's channels.
        ReleaseFeed.ResetMemo();
        return new(options);
    }

    static void WriteCache(string directory, string channel, DateTime fetched)
    {
        var json = File.ReadAllText(Path.Combine(Feeds.Directory, $"{channel}.json"));
        var entry = $$"""
                      {
                        "fetched": "{{fetched.ToString("O", CultureInfo.InvariantCulture)}}",
                        "channel": {{json}}
                      }
                      """;
        File.WriteAllText(Path.Combine(directory, $"{channel}.json"), entry);
    }
}
