public class SdkCheckTaskTests
{
    static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Test]
    public async Task VulnerableSdkWarns()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.100");

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Errors).IsEmpty();
        await Assert.That(engine.Warnings).HasSingleItem();
        await Assert.That(engine.Warnings[0].Code).IsEqualTo("SdkCheck001");
        await Verify(engine.Warnings.Select(_ => _.Code))
            .Snapshot(
                """
                [
                  SdkCheck001
                ]
                """);
    }

    [Test]
    public async Task PatchedSdkIsSilent()
    {
        var engine = new StubBuildEngine();

        await Assert.That(Create(engine, "8.0.204").Execute()).IsTrue();
        await Assert.That(engine.Warnings).IsEmpty();
        await Assert.That(engine.Errors).IsEmpty();
    }

    /// <summary>
    /// An out-of-support channel is an error by default: unlike a CVE with a patch behind it, there
    /// is no version to move to on that channel at all.
    /// </summary>
    [Test]
    public async Task EolIsAnErrorByDefault()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "6.0.428");

        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors).HasSingleItem();
        await Assert.That(engine.Errors[0].Code).IsEqualTo("SdkCheck002");
    }

    [Test]
    public async Task EolCanBeDowngradedToAWarning()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "6.0.428");
        task.EolAsError = "false";

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Errors).IsEmpty();
        await Assert.That(engine.Warnings[0].Code).IsEqualTo("SdkCheck002");
    }

    [Test]
    public async Task CvesCanBeEscalatedToAnError()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.100");
        task.TreatAsError = "true";

        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors).HasSingleItem();
        await Assert.That(engine.Errors[0].Code).IsEqualTo("SdkCheck001");
    }

    /// <summary>
    /// The governing rule: nothing this task discovers about an unreachable feed is something the
    /// person running the build can fix, so it must never be what fails their build.
    /// </summary>
    [Test]
    public async Task UnreachableFeedIsNeitherErrorNorWarning()
    {
        using var empty = new TempDirectory();
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.100");
        task.FeedOverride = "";
        task.CacheDirectory = empty;
        task.Offline = "true";

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Errors).IsEmpty();
        await Assert.That(engine.Warnings).IsEmpty();
        await Assert.That(engine.Messages.Any(_ => _.Code == "SdkCheck004")).IsTrue();
    }

    [Test]
    public async Task GarbageSdkVersionIsSurvivable()
    {
        var engine = new StubBuildEngine();

        await Assert.That(Create(engine, "not-a-version").Execute()).IsTrue();
        await Assert.That(engine.Errors).IsEmpty();
        await Assert.That(engine.Warnings).IsEmpty();
    }

    /// <summary>
    /// Framework-dependent builds resolve a targeting pack and no runtime pack, so
    /// TargetingPackVersion is the runtime the output is built against.
    /// </summary>
    [Test]
    public async Task TargetingPackVersionIsCheckedAsARuntime()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.204");
        task.FrameworkReferences =
        [
            Item("Microsoft.NETCore.App", ("TargetingPackVersion", "8.0.0"))
        ];

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Warnings).HasSingleItem();
        await Assert.That(engine.Warnings[0].Code).IsEqualTo("SdkCheck003");
    }

    /// <summary>
    /// A self-contained build carries both. The runtime pack is what actually ships, so it wins.
    /// </summary>
    [Test]
    public async Task RuntimePackVersionBeatsTargetingPackVersion()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.204");
        task.FrameworkReferences =
        [
            Item(
                "Microsoft.NETCore.App",
                ("TargetingPackVersion", "8.0.4"),
                ("RuntimePackVersion", "8.0.0"))
        ];

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Warnings).HasSingleItem();
    }

    [Test]
    public async Task RuntimePacksAreChecked()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.204");
        task.RuntimePacks =
        [
            Item("Microsoft.NETCore.App.Runtime.win-x64", ("NuGetPackageVersion", "8.0.0"))
        ];

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Warnings).HasSingleItem();
        await Assert.That(engine.Warnings[0].Code).IsEqualTo("SdkCheck003");
    }

    [Test]
    public async Task RuntimeChecksCanBeTurnedOff()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "8.0.204");
        task.IncludeRuntime = "false";
        task.IncludeRuntimePacks = "false";
        task.FrameworkReferences = [Item("Microsoft.NETCore.App", ("TargetingPackVersion", "8.0.0"))];
        task.RuntimePacks = [Item("Microsoft.NETCore.App.Runtime.win-x64", ("NuGetPackageVersion", "8.0.0"))];

        await Assert.That(task.Execute()).IsTrue();
        await Assert.That(engine.Warnings).IsEmpty();
    }

    /// <summary>
    /// MSBuild hands every property across as a string, and an unset one arrives empty rather than
    /// as the default the consumer would have written. A nonsense value falls back to the default
    /// rather than failing the build over a typo.
    /// </summary>
    [Test]
    public async Task UnparseableFlagsFallBackToTheirDefault()
    {
        var engine = new StubBuildEngine();
        var task = Create(engine, "6.0.428");
        task.EolAsError = "yes-please";

        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors[0].Code).IsEqualTo("SdkCheck002");
    }

    [Test]
    public Task WarningTextIsStable()
    {
        var engine = new StubBuildEngine();
        Create(engine, "8.0.100").Execute();
        return Verify(engine.Warnings);
    }

    static SdkCheckTask Create(IBuildEngine engine, string sdkVersion) =>
        new()
        {
            BuildEngine = engine,
            SdkVersion = sdkVersion,
            FeedOverride = Fixtures
        };

    static ITaskItem Item(string spec, params (string Name, string Value)[] metadata)
    {
        var item = new TaskItem(spec);
        foreach (var (name, value) in metadata)
        {
            item.SetMetadata(name, value);
        }

        return item;
    }
}
