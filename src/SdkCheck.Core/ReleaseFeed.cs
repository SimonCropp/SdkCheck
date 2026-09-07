namespace SdkCheck;

/// <summary>
/// Reads per-channel release metadata, preferring an override directory, then a disk cache, then the
/// network. Never throws: a channel that cannot be read comes back as <see cref="FeedResult.Error"/>
/// for the caller to report as a message.
/// </summary>
public class ReleaseFeed(FeedOptions options, Action<string>? log = null)
{
    // MSBuild reuses build nodes, so one process serves many builds over its lifetime. Memoising
    // here is what keeps a solution-wide build at one fetch per channel per day rather than one per
    // project - the disk cache alone would still re-read and re-parse for every project.
    static readonly ConcurrentDictionary<string, Memo> memos = new(StringComparer.Ordinal);

    public FeedResult Get(string channel)
    {
        var key = $"{options.OverrideDirectory}|{CacheDirectory()}|{options.BaseUrl}|{channel}";
        if (memos.TryGetValue(key, out var memo) &&
            memo.IsFresh(DateTime.UtcNow))
        {
            return memo.Result;
        }

        var result = Resolve(channel);

        // A fetch that failed is held briefly whether or not an expired cache stood in for it. The
        // stale channel is worth reporting against, but it is not a fresh read: holding it for the
        // cache TTL would mean one dropped connection pins the node to month old data all day, and
        // the disk cache it came from never gets rewritten either.
        var ttl = result.Channel == null || result.FetchFailed ? options.FailureTtl : options.CacheTtl;
        memos[key] = new(DateTime.UtcNow, result, ttl);
        return result;
    }

    /// <summary>
    /// Drops the process wide memo. For tests, which would otherwise see one another's feeds.
    /// </summary>
    public static void ResetMemo() =>
        memos.Clear();

    FeedResult Resolve(string channel)
    {
        if (options.OverrideDirectory != null)
        {
            return FromOverride(channel);
        }

        var cached = ReadCache(channel);
        if (cached != null &&
            DateTime.UtcNow - cached.Fetched < options.CacheTtl &&
            cached.Channel != null)
        {
            return new(cached.Channel);
        }

        if (options.Offline)
        {
            // Stale beats nothing: the cache is the only thing that can say anything at all here.
            if (cached?.Channel != null)
            {
                return new(cached.Channel);
            }

            return new(error: "offline and nothing cached");
        }

        try
        {
            var fetched = Fetch(channel);
            WriteCache(channel, fetched);
            return new(fetched);
        }
        catch (Exception exception)
        {
            var reason = Describe(exception);
            log?.Invoke($"SdkCheck: fetching .NET {channel} release metadata failed: {reason}");

            // A CDN being unreachable - a dropped VPN, a proxy, a 500 - is not something the person
            // running this build can fix, so it must never be what fails their build.
            if (cached?.Channel != null)
            {
                return new(cached.Channel, fetchFailed: true);
            }

            return new(error: reason, fetchFailed: true);
        }
    }

    FeedResult FromOverride(string channel)
    {
        var path = Path.Combine(options.OverrideDirectory!, $"{channel}.json");
        if (!File.Exists(path))
        {
            return new(error: $"no override feed at '{path}'");
        }

        try
        {
            using var file = File.OpenRead(path);
            return new(Deserialize(file));
        }
        catch (Exception exception)
        {
            return new(error: $"override feed '{path}' is unreadable: {Describe(exception)}");
        }
    }

    ChannelReleases Fetch(string channel)
    {
        var url = $"{options.BaseUrl}/{channel}/releases.json";
        log?.Invoke($"SdkCheck: fetching {url}");

        using var cancellation = new CancelSource(options.Timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = HttpClientFactory.Get().Send(request, cancellation.Token);
        response.EnsureSuccessStatusCode();
        using var content = response.Content.ReadAsStream(cancellation.Token);

        return Deserialize(content);
    }

    /// <summary>
    /// Reads straight from the stream rather than through an intermediate string. The live 8.0 feed
    /// is around 1.5 MB, so materialising it first allocates a 1.5 MB string for no reason.
    /// </summary>
    static ChannelReleases Deserialize(Stream json)
    {
        var channel = JsonSerializer.Deserialize(json, FeedJsonContext.Default.ChannelReleases);
        if (channel == null)
        {
            throw new InvalidOperationException("feed deserialized to null");
        }

        return channel;
    }

    string CacheDirectory() =>
        options.CacheDirectory ??
        Environment.GetEnvironmentVariable("SDKCHECK_CACHE") ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SdkCheck");

    string CachePath(string channel) =>
        Path.Combine(CacheDirectory(), $"{channel}.json");

    CacheEntry? ReadCache(string channel)
    {
        var path = CachePath(channel);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var file = File.OpenRead(path);
            return JsonSerializer.Deserialize(file, FeedJsonContext.Default.CacheEntry);
        }
        catch (Exception exception)
        {
            // A truncated or half written cache file is a reason to re-fetch, not to fail.
            log?.Invoke($"SdkCheck: ignoring unreadable cache '{path}': {Describe(exception)}");
            return null;
        }
    }

    void WriteCache(string channel, ChannelReleases channelReleases)
    {
        var path = CachePath(channel);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(CacheDirectory());
            var entry = new CacheEntry
            {
                Fetched = DateTime.UtcNow,
                Channel = channelReleases
            };
            File.WriteAllText(temp, JsonSerializer.Serialize(entry, FeedJsonContext.Default.CacheEntry));

            // Written to a temp file and moved into place so a concurrent project in the same build
            // cannot read a half written file. Losing the race is fine - the other writer wrote the
            // same content.
            MoveOver(temp, path);
        }
        catch (Exception exception)
        {
            log?.Invoke($"SdkCheck: could not write cache '{path}': {Describe(exception)}");
            Discard(temp);
        }
    }

    /// <summary>
    /// Replaces the cache file with the temp one, without the file ever being absent.
    /// </summary>
    /// <remarks>
    /// Deleting first and moving second leaves a window in which a concurrent reader finds no cache,
    /// and goes to the network for a feed that is sitting right there - which is the cost the cache
    /// exists to avoid. netstandard2.0 has no overwriting File.Move, so the move is attempted first
    /// and File.Replace handles the case where a file is already there.
    /// </remarks>
    static void MoveOver(string temp, string path)
    {
        try
        {
            File.Move(temp, path);
        }
        catch (IOException)
        {
            File.Replace(temp, path, null);
        }
    }

    static void Discard(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort. A leftover temp file in the cache directory harms nothing.
        }
    }

    static string Describe(Exception exception)
    {
        if (exception is OperationCanceledException or TaskCanceledException)
        {
            return "timed out";
        }

        return exception.Message;
    }
}
