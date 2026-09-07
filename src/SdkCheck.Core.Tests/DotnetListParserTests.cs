public class DotnetListParserTests
{
    const string sdks =
        """
        8.0.424 [C:\Program Files\dotnet\sdk]
        9.0.317 [C:\Program Files\dotnet\sdk]
        10.0.400 [C:\Program Files\dotnet\sdk]
        11.0.100-rc.1.26431.118 [C:\Program Files\dotnet\sdk]
        """;

    const string runtimes =
        """
        Microsoft.AspNetCore.App 8.0.30 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        Microsoft.NETCore.App 8.0.30 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
        Microsoft.WindowsDesktop.App 8.0.30 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
        """;

    [Test]
    public Task Sdks() =>
        Verify(DotnetListParser.ParseSdks(sdks).Select(_ => $"{_.Kind} {_.Version}"))
            .Snapshot(
                """
                [
                  Sdk 8.0.424,
                  Sdk 9.0.317,
                  Sdk 10.0.400,
                  Sdk 11.0.100-rc.1.26431.118
                ]
                """);

    [Test]
    public Task Runtimes() =>
        Verify(DotnetListParser.ParseRuntimes(runtimes).Select(_ => $"{_.Kind} {_.Version} {_.Label}"))
            .Snapshot(
                """
                [
                  AspNetCoreRuntime 8.0.30 Microsoft.AspNetCore.App,
                  Runtime 8.0.30 Microsoft.NETCore.App,
                  WindowsDesktopRuntime 8.0.30 Microsoft.WindowsDesktop.App
                ]
                """);

    [Test]
    public async Task EmptyOutput()
    {
        await Assert.That(DotnetListParser.ParseSdks("")).IsEmpty();
        await Assert.That(DotnetListParser.ParseRuntimes("")).IsEmpty();
    }

    [Test]
    public async Task DuplicatesCollapse() =>
        await Assert.That(DotnetListParser.ParseSdks("8.0.424 [a]\n8.0.424 [b]")).HasSingleItem();
}
