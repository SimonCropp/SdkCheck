namespace SdkCheck;

/// <summary>
/// Parses the output of "dotnet --list-sdks" and "dotnet --list-runtimes". Kept separate from the
/// process invocation so it can be tested against captured output.
/// </summary>
public static class DotnetListParser
{
    /// <summary>
    /// Lines look like: 8.0.424 [C:\Program Files\dotnet\sdk]
    /// </summary>
    public static IReadOnlyList<Component> ParseSdks(string output) =>
        Lines(output)
            .Select(_ => _.Split(' ')[0])
            .Where(_ => _.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(_ => new Component(ComponentKind.Sdk, _))
            .ToList();

    /// <summary>
    /// Lines look like: Microsoft.AspNetCore.App 8.0.30 [C:\Program Files\dotnet\shared\...]
    /// </summary>
    public static IReadOnlyList<Component> ParseRuntimes(string output)
    {
        var components = new List<Component>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines(output))
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = parts[0];
            var version = parts[1];
            if (!seen.Add($"{name}|{version}"))
            {
                continue;
            }

            components.Add(new(KindFor(name), version, name));
        }

        return components;
    }

    static ComponentKind KindFor(string name) =>
        name switch
        {
            "Microsoft.AspNetCore.App" => ComponentKind.AspNetCoreRuntime,
            "Microsoft.WindowsDesktop.App" => ComponentKind.WindowsDesktopRuntime,
            _ => ComponentKind.Runtime
        };

    static IEnumerable<string> Lines(string output) =>
        output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(_ => _.Trim())
            .Where(_ => _.Length > 0);
}
