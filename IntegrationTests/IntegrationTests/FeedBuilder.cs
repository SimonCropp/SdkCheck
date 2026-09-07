namespace SdkCheck.IntegrationTests;

public enum FeedShape
{
    /// <summary>The SDK running the build is the newest release on its channel.</summary>
    Clean,

    /// <summary>A security release shipped after the SDK running the build.</summary>
    Vulnerable,

    /// <summary>The channel is out of support.</summary>
    Eol
}

/// <summary>
/// Writes a fixture feed naming the SDK that is actually running the build.
/// </summary>
/// <remarks>
/// The alternative - a checked-in feed plus a way to tell the task which version to pretend it is -
/// would need test-only plumbing in the shipped targets, and would not exercise
/// $(NETCoreSdkVersion) at all. Generating the feed around the real version keeps the production
/// path intact and the assertions deterministic on any machine.
///
/// No runtime versions are declared, so a targeting pack the consumer resolves matches no release
/// and nothing is published against it. End of support is the one finding that reaches a runtime
/// anyway, since it is tested without reference to a version - which is what the second channel
/// below exists to reach. That leaves each test asserting one thing.
/// </remarks>
public static class FeedBuilder
{
    public static string Write(string sdkVersion, FeedShape shape, string? eolChannel = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "sdkcheck-it-feeds", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var parts = sdkVersion.Split('-')[0].Split('.');
        var channel = $"{parts[0]}.{parts[1]}";

        File.WriteAllText(Path.Combine(directory, $"{channel}.json"), Channel(channel, sdkVersion, shape));

        // A second channel, out of support, for a target framework older than the SDK doing the
        // build. It is the only way to reach a runtime finding while the SDK itself is clean, and
        // the two default to different severities.
        if (eolChannel != null)
        {
            File.WriteAllText(
                Path.Combine(directory, $"{eolChannel}.json"),
                Channel(eolChannel, $"{eolChannel}.100", FeedShape.Eol));
        }

        return directory;
    }

    static string Channel(string channel, string sdkVersion, FeedShape shape)
    {
        var supportPhase = shape == FeedShape.Eol ? "eol" : "active";
        var eolDate = shape == FeedShape.Eol ? "2024-11-12" : "2099-11-10";
        var latestSdk = shape == FeedShape.Vulnerable ? Bump(sdkVersion) : sdkVersion;

        var releases = new List<string>();
        if (shape == FeedShape.Vulnerable)
        {
            releases.Add($$"""
                           {
                               "release-version": "{{channel}}.1",
                               "security": true,
                               "cve-list": [
                                 { "cve-id": "CVE-2099-0001", "cve-url": "https://example.invalid/1" },
                                 { "cve-id": "CVE-2099-0002", "cve-url": "https://example.invalid/2" }
                               ],
                               "sdk": { "version": "{{Bump(sdkVersion)}}" },
                               "sdks": [ { "version": "{{Bump(sdkVersion)}}" } ]
                             }
                           """);
        }

        releases.Add($$"""
                      {
                           "release-version": "{{channel}}.0",
                           "security": false,
                           "cve-list": [],
                           "sdk": { "version": "{{sdkVersion}}" },
                           "sdks": [ { "version": "{{sdkVersion}}" } ]
                         }
                      """);

        return $$"""
                 {
                   "channel-version": "{{channel}}",
                   "latest-release": "{{channel}}.{{(shape == FeedShape.Vulnerable ? 1 : 0)}}",
                   "latest-sdk": "{{latestSdk}}",
                   "latest-runtime": "{{channel}}.0",
                   "support-phase": "{{supportPhase}}",
                   "eol-date": "{{eolDate}}",
                   "releases": [ {{string.Join(", ", releases)}} ]
                 }
                 """;
    }

    /// <summary>
    /// The next SDK patch on the same feature band: 10.0.400 becomes 10.0.401.
    /// </summary>
    static string Bump(string sdkVersion)
    {
        var numeric = sdkVersion.Split('-')[0];
        var parts = numeric.Split('.');
        return $"{parts[0]}.{parts[1]}.{int.Parse(parts[2]) + 1}";
    }
}
