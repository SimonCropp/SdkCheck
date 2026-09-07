/// <summary>
/// The cache write, over a real fetch. Every other test here either points at an override directory
/// or runs offline, so nothing else in any of the four projects reaches WriteCache at all - and the
/// replace it performs is what runs on every refresh a real machine ever does.
/// </summary>
[NotInParallel]
public class CacheWriteTests
{
    [Test]
    public async Task FetchWritesTheCache()
    {
        using var server = new FeedServer("8.0");
        using var cache = new TempDirectory();

        var result = Feed(server, cache).Get("8.0");

        await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
        await Assert.That(File.Exists(Path.Combine(cache, "8.0.json"))).IsTrue();
        await Assert.That(server.Requests).IsEqualTo(1);
    }

    /// <summary>
    /// The second write onwards goes through File.Replace rather than File.Move, since there is now
    /// something in the way. A platform where that throws would degrade to fetching 1.5 MB per
    /// process forever, and say so only in a low-importance log line.
    /// </summary>
    [Test]
    public async Task RefreshReplacesTheCacheInPlace()
    {
        using var server = new FeedServer("8.0");
        using var cache = new TempDirectory();
        var path = Path.Combine(cache, "8.0.json");

        Feed(server, cache).Get("8.0");
        var first = File.ReadAllText(path);

        // Nothing is held for any time, so this fetches again rather than reading what it just wrote.
        var result = Feed(server, cache, TimeSpan.Zero).Get("8.0");

        await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
        await Assert.That(server.Requests).IsEqualTo(2);
        await Assert.That(File.Exists(path)).IsTrue();
        await Assert.That(File.ReadAllText(path)).IsNotEqualTo(first);
    }

    /// <summary>
    /// Written to a temp file and moved over, so a concurrent reader never sees half of one. The
    /// temp files are not left behind either, on the path where the move works or the one where it
    /// does not.
    /// </summary>
    [Test]
    public async Task NoTempFilesAreLeftBehind()
    {
        using var server = new FeedServer("8.0");
        using var cache = new TempDirectory();

        Feed(server, cache).Get("8.0");
        Feed(server, cache, TimeSpan.Zero).Get("8.0");

        await Assert.That(Directory.GetFiles(cache)).HasSingleItem();
    }

    /// <summary>
    /// A cache file that cannot be written over - held by another process, read-only, a directory
    /// that has gone away - costs the write, never the check. The feed was fetched, so the answer is
    /// the fresh one either way.
    /// </summary>
    [Test]
    public async Task CacheThatCannotBeWrittenIsSurvivable()
    {
        using var server = new FeedServer("8.0");
        using var cache = new TempDirectory();
        var path = Path.Combine(cache, "8.0.json");

        Feed(server, cache).Get("8.0");

        var log = new List<string>();
        using (Block(cache, path))
        {
            var result = new ReleaseFeed(Options(server, cache, TimeSpan.Zero), log.Add).Get("8.0");

            await Assert.That(result.Channel!.LatestSdk).IsEqualTo("8.0.204");
        }

        // The cache path and the reason it could not be taken, rather than a second failure about
        // the temp file that was on its way there.
        var failure = log.Single(_ => _.Contains("could not write cache"));

        await Assert.That(failure).Contains("8.0.json");
        await Assert.That(File.Exists(path)).IsTrue();
    }

    /// <summary>
    /// Stops the cache from being written, for as long as the result is held.
    /// </summary>
    /// <remarks>
    /// Windows holds the cache file open with no sharing, which is the sharing violation a
    /// concurrent process produces. A rename on Unix answers to the directory rather than to any
    /// handle on the file, so a held file there is replaced without complaint - the write is stopped
    /// by taking write permission off the directory instead, which fails the temp file rather than
    /// the move. Either way WriteCache reports it against the cache path.
    /// </remarks>
    static IDisposable Block(string cache, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        return new ReadOnlyDirectory(cache);
    }

    [UnsupportedOSPlatform("windows")]
    sealed class ReadOnlyDirectory :
        IDisposable
    {
        string directory;
        UnixFileMode mode;

        public ReadOnlyDirectory(string directory)
        {
            this.directory = directory;
            mode = File.GetUnixFileMode(directory);
            File.SetUnixFileMode(directory, mode & ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
        }

        public void Dispose() => File.SetUnixFileMode(directory, mode);
    }

    static ReleaseFeed Feed(FeedServer server, string cache, TimeSpan? ttl = null) =>
        new(Options(server, cache, ttl));

    static FeedOptions Options(FeedServer server, string cache, TimeSpan? ttl = null)
    {
        // The memo is process wide, and holds a channel across feeds pointed at the same place.
        ReleaseFeed.ResetMemo();
        return new()
        {
            BaseUrl = server.Url,
            CacheDirectory = cache,
            CacheTtl = ttl ?? TimeSpan.FromHours(24)
        };
    }
}
