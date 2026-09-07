/// <summary>
/// Serialized because every test here clears the process wide memo through <see cref="Feed"/>, and
/// one test's reset landing between another's two calls to Get is the difference between a memoised
/// result and a re-resolved one.
/// </summary>
[NotInParallel]
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

    /// <summary>
    /// A success is held for the full cache TTL. Forty projects in one build read the feed once.
    /// </summary>
    [Test]
    public async Task SuccessIsMemoised()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory, "8.0.json");
        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), path);

        var feed = Feed(new() { OverrideDirectory = directory });

        await Assert.That(feed.Get("8.0", resolved).Channel).IsNotNull();

        File.Delete(path);

        // Memoised, so the now missing file is never looked for.
        await Assert.That(feed.Get("8.0", resolved.AddHours(12)).Channel).IsNotNull();
    }

    /// <summary>
    /// A failure is not held that long. MSBuild reuses build nodes across builds, so a failure kept
    /// for the cache TTL outlives the build it happened in: one dropped connection and the check
    /// says nothing for the rest of the day, long after the network came back.
    /// </summary>
    [Test]
    public async Task FailureIsNotHeldForTheCacheTtl()
    {
        using var directory = new TempDirectory();
        var feed = Feed(new() { OverrideDirectory = directory });

        await Assert.That(feed.Get("8.0", resolved).Channel).IsNull();

        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), Path.Combine(directory, "8.0.json"));

        await Assert.That(feed.Get("8.0", retried).Channel).IsNotNull();
    }

    /// <summary>
    /// A fetch that fell back to an expired cache is a failure too. Held for the cache TTL, one
    /// dropped connection would pin a reused build node to month old data for the rest of the day,
    /// and the disk cache it reads from would never be rewritten either.
    /// </summary>
    [Test]
    public async Task StaleFallbackIsNotHeldForTheCacheTtl()
    {
        using var cache = new TempDirectory();
        WriteCache(cache, "8.0", resolved.AddDays(-30));

        var feed = Feed(
            new()
            {
                CacheDirectory = cache,
                BaseUrl = unreachable,
                Timeout = TimeSpan.FromSeconds(1)
            });

        // Expired, and the feed cannot be reached, so the fallback is what comes back.
        await Assert.That(feed.Get("8.0", resolved).Channel!.LatestSdk).IsEqualTo("8.0.204");

        // The network coming back, stood in for by a fresh cache holding a different channel: the
        // memo has to be past by then for it to be seen at all.
        WriteCache(cache, "8.0", retried, "6.0");

        await Assert.That(feed.Get("8.0", retried).Channel!.LatestSdk).IsEqualTo("6.0.428");
    }

    // Nothing listens on port 1, so a fetch fails there without the network being involved.
    const string unreachable = "http://127.0.0.1:1";

    // An hour on: past the default FailureTtl and far short of the default CacheTtl, so what these
    // tests see is the shipped policy rather than TTLs they set for themselves. Stepping the clock
    // rather than setting the TTLs is what keeps that distinction testable.
    static readonly DateTime resolved = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime retried = resolved.AddHours(1);

    static ReleaseFeed Feed(FeedOptions options)
    {
        // The memo is process wide, so tests would otherwise see one another's channels.
        ReleaseFeed.ResetMemo();
        return new(options);
    }

    static void WriteCache(string directory, string channel, DateTime fetched, string? content = null)
    {
        var json = File.ReadAllText(Path.Combine(Feeds.Directory, $"{content ?? channel}.json"));
        var entry = $$"""
                      {
                        "fetched": "{{fetched.ToString("O", CultureInfo.InvariantCulture)}}",
                        "channel": {{json}}
                      }
                      """;
        File.WriteAllText(Path.Combine(directory, $"{channel}.json"), entry);
    }
}
