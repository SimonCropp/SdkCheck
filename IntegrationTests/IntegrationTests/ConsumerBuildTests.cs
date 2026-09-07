namespace SdkCheck.IntegrationTests;

/// <summary>
/// Builds real projects against the real package. This is the only place the tasks/ layout, the
/// $(MSBuildRuntimeType) switch and the NuGet build/ auto-import are actually exercised - a unit
/// test constructs the task directly and proves none of them.
/// </summary>
public class ConsumerBuildTests
{
    [Test]
    public async Task VulnerableSdkWarnsInARealBuild()
    {
        var result = await Build("Consumer.Basic", FeedShape.Vulnerable);

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).Contains("SdkCheck001").Because(result.Combined);
        await Assert.That(result.Combined).Contains("CVE-2099-0001").Because(result.Combined);
    }

    [Test]
    public async Task PatchedSdkBuildsSilently()
    {
        var result = await Build("Consumer.Basic", FeedShape.Clean);

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).DoesNotContain("SdkCheck").Because(result.Combined);
    }

    /// <summary>
    /// An out-of-support channel fails the build by default. Nothing else in this package does.
    /// </summary>
    [Test]
    public async Task EolChannelFailsTheBuild()
    {
        var result = await Build("Consumer.Basic", FeedShape.Eol);

        await Assert.That(result.ExitCode).IsNotEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).Contains("SdkCheck002").Because(result.Combined);
    }

    [Test]
    public async Task EolCanBeDowngradedByTheConsumer()
    {
        var result = await Build(
            "Consumer.Basic",
            FeedShape.Eol,
            new Dictionary<string, string> { ["SdkCheckEolAsError"] = "false" });

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).Contains("SdkCheck002").Because(result.Combined);
    }

    [Test]
    public async Task ConsumerCanEscalateToAnError()
    {
        var result = await Build(
            "Consumer.Basic",
            FeedShape.Vulnerable,
            new Dictionary<string, string> { ["SdkCheckTreatAsError"] = "true" });

        await Assert.That(result.ExitCode).IsNotEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).Contains("SdkCheck001").Because(result.Combined);
    }

    [Test]
    public async Task ConsumerCanTurnItOff()
    {
        var result = await Build(
            "Consumer.Basic",
            FeedShape.Eol,
            new Dictionary<string, string> { ["SdkCheckEnabled"] = "false" });

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).DoesNotContain("SdkCheck00").Because(result.Combined);
    }

    /// <summary>
    /// The warning is suppressible the ordinary way. MSBuild honours NoWarn for a task diagnostic
    /// only because the task passes a real code through the long LogWarning overload.
    /// </summary>
    [Test]
    public async Task NoWarnSuppressesIt()
    {
        var result = await Build(
            "Consumer.Basic",
            FeedShape.Vulnerable,
            new Dictionary<string, string> { ["NoWarn"] = "SdkCheck001" });

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).DoesNotContain("SdkCheck001").Because(result.Combined);
    }

    /// <summary>
    /// The target runs once per target framework because the resolved runtime differs per TFM, so
    /// the per-build deduplication is the only thing stopping a multi-targeted project reporting the
    /// same SDK twice.
    /// </summary>
    [Test]
    public async Task MultiTargetedProjectReportsOnce()
    {
        var result = await Build("Consumer.MultiTargeted", FeedShape.Vulnerable);

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        // Counting the raw string would count noise: MSBuild prints each line of a multi-line
        // warning separately and repeats the lot in its summary. The set of target frameworks it was
        // annotated with is the thing being asserted.
        await Assert.That(FrameworksWarnedAbout(result.Combined)).HasSingleItem().Because(result.Combined);
    }

    /// <summary>
    /// No feed, no cache, no network permitted: the build still has to succeed and say nothing.
    /// </summary>
    [Test]
    public async Task OfflineWithNoCacheIsSilentAndSucceeds()
    {
        using var cache = new TempDirectory();
        var result = await Build(
            "Consumer.Basic",
            FeedShape.Clean,
            new Dictionary<string, string>
            {
                ["SdkCheckFeedOverride"] = "",
                ["SdkCheckOffline"] = "true",
                ["SdkCheckCacheDirectory"] = cache
            });

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Combined);
        await Assert.That(result.Combined).DoesNotContain("SdkCheck00").Because(result.Combined);
    }

    /// <summary>
    /// Names the assembly the build actually loaded the task from.
    /// </summary>
    /// <remarks>
    /// Nothing else here would notice if the $(MSBuildRuntimeType) condition in SdkCheck.targets
    /// were inverted: netstandard2.0 loads perfectly well under MSBuild on .NET, so every other test
    /// would still pass while every Visual Studio user got nothing at all.
    /// </remarks>
    [Test]
    public async Task DotnetBuildLoadsTheNetCoreTaskAssembly()
    {
        var result = await Build("Consumer.Basic", FeedShape.Vulnerable, verbosity: "diagnostic");

        var loaded = result.Combined
            .Split('\n')
            .Where(_ => _.Contains("SdkCheckTask", StringComparison.Ordinal) &&
                        _.Contains("tasks", StringComparison.Ordinal))
            .ToList();

        await Assert.That(loaded).IsNotEmpty()
            .Because("no line reported which assembly SdkCheckTask was loaded from");
        await Assert.That(loaded.Any(_ => _.Contains(@"tasks\net10.0\SdkCheck.dll", StringComparison.OrdinalIgnoreCase)))
            .IsTrue()
            .Because(string.Join(Environment.NewLine, loaded));
    }

    static IReadOnlyList<string> FrameworksWarnedAbout(string output) =>
        output.Split('\n')
            .Where(_ => _.Contains("is affected by", StringComparison.Ordinal))
            .Select(_ => Regex.Match(_, @"TargetFramework=([^\]\s]+)"))
            .Where(_ => _.Success)
            .Select(_ => _.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    static async Task<CliResult> Build(
        string fixture,
        FeedShape shape,
        IReadOnlyDictionary<string, string>? extraProperties = null,
        string verbosity = "minimal",
        [CallerMemberName] string caller = "")
    {
        var package = PackageUnderTest.Ensure();
        var work = TestEnvironment.MakeWorkDirectory(caller);
        TestEnvironment.CopyDirectory(Path.Combine(TestEnvironment.FixturesDirectory, fixture), work);
        TestEnvironment.WriteNugetConfig(work, package.Feed);

        // Empty sentinels so the temp directory cannot inherit this repo's Directory.Build.* from
        // somewhere up the temp path, and so the fixture is built exactly as a stranger's project
        // would be.
        File.WriteAllText(Path.Combine(work, "Directory.Build.props"), "<Project/>");
        File.WriteAllText(Path.Combine(work, "Directory.Build.targets"), "<Project/>");

        // The repo's global.json goes with it. Without it the fixture resolves whichever SDK is
        // newest on the machine, whose bundled targeting packs may not be published yet (NU1102) -
        // and the generated feed would be describing a different SDK than the one doing the build.
        File.Copy(
            Path.Combine(TestEnvironment.RepoRoot, "global.json"),
            Path.Combine(work, "global.json"),
            overwrite: true);

        var properties = new Dictionary<string, string>
        {
            ["SdkCheckVersion"] = package.Version,
            ["SdkCheckFeedOverride"] = FeedBuilder.Write(DotnetCliRunner.SdkVersion(work), shape),
            ["Configuration"] = "Release"
        };

        if (extraProperties != null)
        {
            foreach (var property in extraProperties)
            {
                properties[property.Key] = property.Value;
            }
        }

        var project = Directory.GetFiles(work, "*.csproj").Single();
        var packages = Path.Combine(work, ".pkgs");
        Directory.CreateDirectory(packages);
        return await DotnetCliRunner.Run("build", project, properties, work, packages, verbosity);
    }
}
