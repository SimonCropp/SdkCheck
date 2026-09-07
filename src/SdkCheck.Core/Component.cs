namespace SdkCheck;

/// <summary>
/// One thing to check: a version, and enough context to say what it is in a diagnostic.
/// </summary>
public class Component(ComponentKind kind, string version, string? label = null)
{
    public ComponentKind Kind { get; } = kind;
    public string Version { get; } = version;

    /// <summary>
    /// Extra identity for kinds where the version alone is ambiguous - the RID of a runtime pack,
    /// or the target framework a runtime was resolved for.
    /// </summary>
    public string? Label { get; } = label;

    public string Describe() =>
        Kind switch
        {
            ComponentKind.Sdk => $"SDK {Version}",
            ComponentKind.Runtime => $"runtime {Version}",
            ComponentKind.AspNetCoreRuntime => $"ASP.NET Core runtime {Version}",
            ComponentKind.WindowsDesktopRuntime => $"Windows Desktop runtime {Version}",
            ComponentKind.RuntimePack => Label == null
                ? $"runtime pack {Version}"
                : $"runtime pack {Version} ({Label})",
            _ => Version
        };

    public bool IsSdk => Kind == ComponentKind.Sdk;
}
