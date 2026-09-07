namespace SdkCheck.IntegrationTests;

/// <summary>
/// The package's own layout. Nothing here needs a build, but a regression in any of it breaks every
/// consumer silently rather than loudly.
/// </summary>
public class PackageShapeTests
{
    [Test]
    public Task Layout() =>
        Verify(Entries().Where(_ => !_.EndsWith(".dll", StringComparison.Ordinal)))
            .Snapshot(
                """
                [
                  SdkCheck.nuspec,
                  build/SdkCheck.targets,
                  buildMultiTargeting/SdkCheck.targets,
                  icon.png,
                  nuget.md
                ]
                """);

    [Test]
    public async Task BothMsBuildRuntimesAreCovered()
    {
        var entries = Entries();

        // SdkCheck.targets picks by $(MSBuildRuntimeType): Core loads net10.0, .NET Framework
        // MSBuild loads netstandard2.0. A missing leg is a silent no-op in that host.
        await Assert.That(entries).Contains("tasks/net10.0/SdkCheck.dll");
        await Assert.That(entries).Contains("tasks/netstandard2.0/SdkCheck.dll");
    }

    /// <summary>
    /// A task package must contribute nothing to a consumer's compile or runtime references.
    /// </summary>
    [Test]
    public async Task NothingIsShippedAsALibrary()
    {
        var entries = Entries();

        await Assert.That(entries.Any(_ => _.StartsWith("lib/", StringComparison.Ordinal))).IsFalse();
        await Assert.That(entries.Any(_ => _.StartsWith("ref/", StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>
    /// DevelopmentDependency plus PrivateAssets on every reference plus
    /// SuppressDependenciesWhenPacking. Any one of them slipping puts System.Text.Json into the
    /// dependency graph of every consuming project.
    /// </summary>
    [Test]
    public async Task NuspecDeclaresNoDependencies()
    {
        var nuspec = Nuspec();

        await Assert.That(nuspec.Contains("<dependencies")).IsFalse();
        await Assert.That(nuspec).Contains("<developmentDependency>true</developmentDependency>");
    }

    /// <summary>
    /// MSBuild on Linux normalizes a trailing backslash in PackagePath and NuGet then appends
    /// another, which produces 'tasks/netstandard2.0//SdkCheck.dll' - a path no UsingTask resolves.
    /// </summary>
    [Test]
    public async Task NoDoubledSeparators() =>
        await Assert.That(Entries().Any(_ => _.Contains("//", StringComparison.Ordinal))).IsFalse();

    static List<string> Entries()
    {
        using var zip = ZipFile.OpenRead(PackageUnderTest.Ensure().NupkgPath);
        return zip.Entries
            .Select(_ => _.FullName)
            .Where(_ => !_.StartsWith("_rels/", StringComparison.Ordinal) &&
                        !_.StartsWith("package/", StringComparison.Ordinal) &&
                        !_.StartsWith("[Content_Types]", StringComparison.Ordinal))
            .OrderBy(_ => _, StringComparer.Ordinal)
            .ToList();
    }

    static string Nuspec()
    {
        using var zip = ZipFile.OpenRead(PackageUnderTest.Ensure().NupkgPath);
        using var stream = zip.GetEntry("SdkCheck.nuspec")!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
