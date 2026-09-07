namespace testing;

public class ProgramTests
{
    static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Test]
    public async Task VulnerableSdkExitsNonZero()
    {
        var (code, output) = Run(new() { Sdks = ["8.0.100"] });

        await Assert.That(code).IsEqualTo(1);
        await Assert.That(output).Contains("SdkCheck001");
    }

    [Test]
    public async Task PatchedSdkExitsZero()
    {
        var (code, output) = Run(new() { Sdks = ["8.0.204"] });

        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output).Contains("Nothing published");
    }

    [Test]
    public async Task EolExitsNonZero()
    {
        var (code, output) = Run(new() { Sdks = ["6.0.428"] });

        await Assert.That(code).IsEqualTo(1);
        await Assert.That(output).Contains("SdkCheck002");
    }

    /// <summary>
    /// For a scheduled job that should report without failing the pipeline.
    /// </summary>
    [Test]
    public async Task WarnOnlyStillExitsZero()
    {
        var (code, output) = Run(new() { Sdks = ["8.0.100"], WarnOnly = true });

        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output).Contains("SdkCheck001");
    }

    /// <summary>
    /// An empty --sdk is skipped rather than treated as a version, so a script passing through an
    /// unset variable gets silence instead of a crash.
    /// </summary>
    [Test]
    public async Task BlankVersionIsSkipped()
    {
        var (code, output) = Run(new() { Sdks = [""] });

        await Assert.That(code).IsEqualTo(0);
        await Assert.That(output).Contains("Nothing published");
    }

    [Test]
    public Task TextOutput() =>
        Verify(Run(new() { Sdks = ["6.0.428"] }).Output)
            .Snapshot(
                """
                SdkCheck002: The .NET 6.0 channel reached end of support on 2024-11-12.
                No further security patches will ship for SDK 6.0.428, so any CVE found in it from now on is unfixable in place.
                Move to a supported channel.

                Checked 1 component(s), 1 with findings.

                """);

    [Test]
    public Task JsonOutput() =>
        Verify(Run(new() { Sdks = ["8.0.204"], Format = "json" }).Output)
            .Snapshot(
                """
                {
                  "checked": [
                    {
                      "kind": "Sdk",
                      "version": "8.0.204"
                    }
                  ],
                  "findings": []
                }

                """);

    [Test]
    public async Task JsonForAFindingIsParseable()
    {
        var (_, output) = Run(new() { Sdks = ["6.0.428"], Format = "json" });

        using var document = JsonDocument.Parse(output);
        var finding = document.RootElement.GetProperty("findings")[0];

        await Assert.That(finding.GetProperty("code").GetString()).IsEqualTo("SdkCheck002");
        await Assert.That(finding.GetProperty("eolDate").GetString()).IsEqualTo("2024-11-12");
    }

    /// <summary>
    /// The band fields ride along with the version, since the prose in "message" is the only other
    /// place they appear and a report consumer should not have to parse it.
    /// </summary>
    [Test]
    public async Task JsonCarriesTheFeatureBandFields()
    {
        // 8.0.106 removed from the security release 8.0.4, so the 1xx band stops at 8.0.105, which
        // shipped before it - the case where the fix has to leave the band.
        var channel = JsonNode.Parse(File.ReadAllText(Path.Combine(Fixtures, "8.0.json")))!;
        var sdks = channel["releases"]!.AsArray()
            .Single(_ => (string?) _!["release-version"] == "8.0.4")!["sdks"]!
            .AsArray();
        sdks.Remove(sdks.Single(_ => (string?) _!["version"] == "8.0.106"));

        using var directory = new TempDirectory();
        File.WriteAllText(Path.Combine(directory, "8.0.json"), channel.ToJsonString());

        var (_, output) = Run(new() { Sdks = ["8.0.100"], Format = "json" }, directory);

        using var document = JsonDocument.Parse(output);
        var finding = document.RootElement.GetProperty("findings")[0];

        await Assert.That(finding.GetProperty("fixedIn").GetString()).IsEqualTo("8.0.204");
        await Assert.That(finding.GetProperty("crossesFeatureBand").GetBoolean()).IsTrue();
        await Assert.That(finding.GetProperty("bandNewest").GetString()).IsEqualTo("8.0.105");
    }

    /// <summary>
    /// Absent rather than null when there is nothing on the band to report, while the crossing
    /// itself is always written.
    /// </summary>
    [Test]
    public async Task JsonOmitsTheBandNewestWhenThereIsNone()
    {
        var (_, output) = Run(new() { Sdks = ["8.0.100"], Format = "json" });

        using var document = JsonDocument.Parse(output);
        var finding = document.RootElement.GetProperty("findings")[0];

        await Assert.That(finding.GetProperty("fixedIn").GetString()).IsEqualTo("8.0.106");
        await Assert.That(finding.GetProperty("crossesFeatureBand").GetBoolean()).IsFalse();
        await Assert.That(finding.TryGetProperty("bandNewest", out _)).IsFalse();
    }

    static (int Code, string Output) Run(Options options, string? feed = null)
    {
        options.Feed = feed ?? Fixtures;
        ReleaseFeed.ResetMemo();

        var writer = new StringWriter();
        return (Program.Run(options, writer, writer), writer.ToString());
    }
}
