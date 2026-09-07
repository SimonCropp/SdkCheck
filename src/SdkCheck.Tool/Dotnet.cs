namespace SdkCheck.Tool;

static class Dotnet
{
    public static string Run(string arguments)
    {
        var info = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(info);
        if (process == null)
        {
            throw new InvalidOperationException($"Could not start 'dotnet {arguments}'.");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'dotnet {arguments}' exited with {process.ExitCode}.");
        }

        return output;
    }
}
