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

    /// <summary>
    /// A feed that stays unreachable is retried less and less. Each attempt on a network that drops
    /// packets rather than refusing them costs a project's BeforeBuild the whole timeout, and once a
    /// minute for the length of the build is a cost with nothing to show for it.
    /// </summary>
    [Test]
    public async Task RepeatedFailuresAreRetriedLessOften()
    {
        using var directory = new TempDirectory();
        var feed = Feed(new() { OverrideDirectory = directory });

        await Assert.That(feed.Get("8.0", resolved).Channel).IsNull();

        // Past the first minute, so this resolves and fails again. Two in a row now.
        var second = resolved.AddSeconds(90);
        await Assert.That(feed.Get("8.0", second).Channel).IsNull();

        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), Path.Combine(directory, "8.0.json"));

        // Another 90 seconds is past the first minute but inside the two the second failure earns,
        // so the readable feed is not looked for yet.
        await Assert.That(feed.Get("8.0", second.AddSeconds(90)).Channel).IsNull();

        await Assert.That(feed.Get("8.0", second.AddMinutes(3)).Channel).IsNotNull();
    }

    /// <summary>
    /// The doubling stops at the cache TTL. Nothing is held longer for having failed than a good
    /// feed is held for having worked.
    /// </summary>
    [Test]
    public async Task BackoffIsCappedAtTheCacheTtl()
    {
        using var directory = new TempDirectory();
        var feed = Feed(
            new()
            {
                OverrideDirectory = directory,
                CacheTtl = TimeSpan.FromSeconds(90)
            });

        await Assert.That(feed.Get("8.0", resolved).Channel).IsNull();

        var second = resolved.AddSeconds(70);
        await Assert.That(feed.Get("8.0", second).Channel).IsNull();

        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), Path.Combine(directory, "8.0.json"));

        // Uncapped the second failure would be held two minutes. Capped it is held ninety seconds,
        // so a hundred is past it.
        await Assert.That(feed.Get("8.0", second.AddSeconds(100)).Channel).IsNotNull();
    }

    /// <summary>
    /// A feed that comes back clears what it was owed. The next failure is a first failure, not the
    /// third of a run that ended an hour ago.
    /// </summary>
    [Test]
    public async Task SuccessResetsTheBackoff()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory, "8.0.json");
        var feed = Feed(new() { OverrideDirectory = directory });

        await Assert.That(feed.Get("8.0", resolved).Channel).IsNull();

        var second = resolved.AddSeconds(90);
        await Assert.That(feed.Get("8.0", second).Channel).IsNull();

        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), path);

        var recovered = second.AddMinutes(3);
        await Assert.That(feed.Get("8.0", recovered).Channel).IsNotNull();

        File.Delete(path);

        var failedAgain = recovered.AddHours(25);
        await Assert.That(feed.Get("8.0", failedAgain).Channel).IsNull();

        File.Copy(Path.Combine(Feeds.Directory, "8.0.json"), path);

        // A minute and a half. Still a run of two, and this would be held for two minutes.
        await Assert.That(feed.Get("8.0", failedAgain.AddSeconds(90)).Channel).IsNotNull();
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
