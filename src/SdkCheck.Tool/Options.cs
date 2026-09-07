namespace SdkCheck.Tool;

public class Options
{
    [Option("sdk", HelpText = "SDK version to check. Repeatable. Defaults to every installed SDK.")]
    public IEnumerable<string> Sdks { get; set; } = [];

    [Option("runtime", HelpText = "Runtime version to check. Repeatable. Defaults to every installed runtime.")]
    public IEnumerable<string> Runtimes { get; set; } = [];

    [Option("offline", HelpText = "Never fetch. Uses the cache even when it is older than the TTL.")]
    public bool Offline { get; set; }

    [Option("cache", HelpText = "Cache directory. Defaults to SDKCHECK_CACHE, then LocalApplicationData/SdkCheck.")]
    public string? Cache { get; set; }

    [Option("cache-hours", Default = 24d, HelpText = "How long a cached feed stays fresh.")]
    public double CacheHours { get; set; }

    [Option("timeout", Default = 15d, HelpText = "Per request timeout in seconds.")]
    public double Timeout { get; set; }

    [Option("feed", HelpText = "Directory of '{channel}.json' files to read instead of the network.")]
    public string? Feed { get; set; }

    [Option("format", Default = "text", HelpText = "text or json.")]
    public string Format { get; set; } = "text";

    [Option("warn-only", HelpText = "Always exit 0, even when something is found.")]
    public bool WarnOnly { get; set; }

    [Option('v', "verbose", HelpText = "Report what is being checked and where the data came from.")]
    public bool Verbose { get; set; }
}
