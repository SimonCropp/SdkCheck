namespace SdkCheck.Tool;

public static class Program
{
    public static int Main(string[] args) =>
        Parser.Default
            .ParseArguments<Options>(args)
            .MapResult(_ => Run(_), _ => ExitCode.Failed);

    /// <summary>
    /// Writers are injectable so tests can read the output without redirecting the process wide
    /// Console, which TUnit forbids for good reason - it breaks its own logging.
    /// </summary>
    public static int Run(Options options, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        try
        {
            var components = Resolve(options);
            if (components.Count == 0)
            {
                error.WriteLine("Nothing to check: no SDKs or runtimes were found or supplied.");
                return ExitCode.Failed;
            }

            var log = options.Verbose ? error.WriteLine : (Action<string>?) null;
            var checker = new SdkChecker(BuildOptions(options), log);
            var findings = checker.Check(components);

            Report(output, options, components, findings);

            var actionable = findings.Count(_ => _.Code != Diagnostics.Unavailable);
            if (actionable == 0 || options.WarnOnly)
            {
                return ExitCode.Clean;
            }

            return ExitCode.Findings;
        }
        catch (Exception exception)
        {
            error.WriteLine(exception.Message);
            return ExitCode.Failed;
        }
    }

    static void Report(
        TextWriter output,
        Options options,
        IReadOnlyList<Component> components,
        IReadOnlyList<Finding> findings)
    {
        if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine(JsonReport.Render(components, findings));
            return;
        }

        if (findings.Count == 0)
        {
            output.WriteLine($"Checked {components.Count} component(s). Nothing published against any of them.");
            return;
        }

        foreach (var finding in findings)
        {
            output.WriteLine($"{finding.Code}: {finding.Body()}");
            output.WriteLine();
        }

        var actionable = findings.Count(_ => _.Code != Diagnostics.Unavailable);
        output.WriteLine($"Checked {components.Count} component(s), {actionable} with findings.");
    }

    static IReadOnlyList<Component> Resolve(Options options)
    {
        var explicitly = options.Sdks
            .Select(_ => new Component(ComponentKind.Sdk, _.Trim()))
            .Concat(options.Runtimes.Select(_ => new Component(ComponentKind.Runtime, _.Trim())))
            .ToList();

        if (explicitly.Count > 0)
        {
            return explicitly;
        }

        return DotnetListParser.ParseSdks(Dotnet.Run("--list-sdks"))
            .Concat(DotnetListParser.ParseRuntimes(Dotnet.Run("--list-runtimes")))
            .ToList();
    }

    static FeedOptions BuildOptions(Options options)
    {
        var feed = new FeedOptions
        {
            Offline = options.Offline,
            OverrideDirectory = Blank(options.Feed),
            CacheDirectory = Blank(options.Cache)
        };

        if (options.CacheHours > 0)
        {
            feed.CacheTtl = TimeSpan.FromHours(options.CacheHours);
        }

        if (options.Timeout > 0)
        {
            feed.Timeout = TimeSpan.FromSeconds(options.Timeout);
        }

        return feed;
    }

    static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
}
