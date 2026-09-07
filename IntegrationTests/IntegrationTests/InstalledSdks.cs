namespace SdkCheck.IntegrationTests;

public static class InstalledSdks
{
    /// <summary>
    /// The newest installed SDK whose major version is lower than the one this repo builds with, or
    /// null when there is none.
    /// </summary>
    /// <remarks>
    /// Exists so a test can prove the package loads on something other than the newest SDK. That is
    /// not a hypothetical: shipping a net10.0 task assembly selected on $(MSBuildRuntimeType) ==
    /// 'Core' broke every consumer on an older SDK, and no test built with anything but the repo's
    /// own SDK could have noticed.
    /// </remarks>
    public static string? OlderThanRepo()
    {
        var current = Version.Parse(Numeric(DotnetCliRunner.SdkVersion(TestEnvironment.RepoRoot)));

        return All()
            .Select(_ => new { Text = _, Parsed = Version.Parse(Numeric(_)) })
            .Where(_ => _.Parsed.Major < current.Major)
            .OrderByDescending(_ => _.Parsed)
            .Select(_ => _.Text)
            .FirstOrDefault();
    }

    static IEnumerable<string> All()
    {
        var info = new ProcessStartInfo("dotnet", "--list-sdks")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(_ => _.Split(' ')[0].Trim())
            // Prereleases would resolve to an SDK that is newer than the repo's despite a lower
            // major, and global.json needs allowPrerelease to select one at all.
            .Where(_ => _.Length > 0 && !_.Contains('-'));
    }

    static string Numeric(string version) =>
        version.Split('-')[0];
}
