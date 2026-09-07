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

    static (int Code, string Output) Run(Options options)
    {
        options.Feed = Fixtures;
        ReleaseFeed.ResetMemo();

        var writer = new StringWriter();
        return (Program.Run(options, writer, writer), writer.ToString());
    }
}
