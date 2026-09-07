using SdkCheck;
using Task = Microsoft.Build.Utilities.Task;

/// <summary>
/// Reports .NET SDK and runtime versions that have published CVEs, or that sit on an out-of-support
/// channel.
/// </summary>
/// <remarks>
/// In the global namespace so UsingTask can name it unqualified, matching SponsorCheck.
/// </remarks>
public class SdkCheckTask :
    Task,
    ICancelableTask
{
    readonly CancellationTokenSource cancellation = new();

    /// <summary>
    /// $(NETCoreSdkVersion) - the SDK running this build. Empty on all but the first target
    /// framework of a multi-targeted project, since the SDK does not vary by TFM. Not [Required]
    /// for that reason: MSBuild rejects an empty value for a required parameter.
    /// </summary>
    public string SdkVersion { get; set; } = "";

    /// <summary>
    /// @(ResolvedFrameworkReference). Passed whole rather than as %(metadata), which would make
    /// MSBuild batch the task once per item.
    /// </summary>
    public ITaskItem[] FrameworkReferences { get; set; } = [];

    /// <summary>
    /// @(ResolvedRuntimePack). Empty unless this is a self-contained or AOT publish.
    /// </summary>
    public ITaskItem[] RuntimePacks { get; set; } = [];

    public string TreatAsError { get; set; } = "";
    public string EolAsError { get; set; } = "";
    public string IncludeRuntime { get; set; } = "";
    public string IncludeRuntimePacks { get; set; } = "";
    public string CacheHours { get; set; } = "";
    public string TimeoutSeconds { get; set; } = "";
    public string Offline { get; set; } = "";
    public string CacheDirectory { get; set; } = "";
    public string FeedOverride { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            var checker = new SdkChecker(BuildOptions(), _ => Log.LogMessage(MessageImportance.Low, _));
            foreach (var finding in checker.Check(Components()))
            {
                Emit(finding);
            }
        }
        catch (Exception exception)
        {
            // Nothing this task can discover is worth breaking a build over, so an unexpected
            // failure is reported the same way an unreachable feed is: as a message.
            Log.LogMessage(
                MessageImportance.Low,
                $"SdkCheck: check skipped after an unexpected failure: {exception}");
        }

        return !Log.HasLoggedErrors;
    }

    public void Cancel() =>
        cancellation.Cancel();

    IEnumerable<Component> Components()
    {
        if (!string.IsNullOrWhiteSpace(SdkVersion))
        {
            yield return new(ComponentKind.Sdk, SdkVersion.Trim());
        }

        if (Flag(IncludeRuntime, true))
        {
            foreach (var item in FrameworkReferences)
            {
                // Framework-dependent builds resolve a targeting pack and no runtime pack, so the
                // targeting pack version is the runtime this output is built against. Self-contained
                // builds also carry RuntimePackVersion here, which is preferred when present because
                // it is what actually ships.
                var version = First(item, "RuntimePackVersion", "TargetingPackVersion");
                if (version != null)
                {
                    yield return new(ComponentKind.Runtime, version, item.ItemSpec);
                }
            }
        }

        if (Flag(IncludeRuntimePacks, true))
        {
            foreach (var item in RuntimePacks)
            {
                var version = First(item, "NuGetPackageVersion", "PackageVersion", "RuntimePackVersion", "Version");
                if (version != null)
                {
                    yield return new(ComponentKind.RuntimePack, version, item.ItemSpec);
                }
            }
        }
    }

    FeedOptions BuildOptions()
    {
        var options = new FeedOptions
        {
            Offline = Flag(Offline, false)
        };

        if (!string.IsNullOrWhiteSpace(FeedOverride))
        {
            options.OverrideDirectory = FeedOverride.Trim();
        }

        if (!string.IsNullOrWhiteSpace(CacheDirectory))
        {
            options.CacheDirectory = CacheDirectory.Trim();
        }

        if (double.TryParse(CacheHours, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) &&
            hours > 0)
        {
            options.CacheTtl = TimeSpan.FromHours(hours);
        }

        if (double.TryParse(TimeoutSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0)
        {
            options.Timeout = TimeSpan.FromSeconds(seconds);
        }

        return options;
    }

    void Emit(Finding finding)
    {
        if (AlreadyReported(finding))
        {
            return;
        }

        var message = finding.Message();

        if (finding.Code == Diagnostics.Unavailable)
        {
            // Logged through the coded overload rather than LogMessage(importance, text) so the
            // message carries SdkCheck004 as a real diagnostic code, and a build log can be grepped
            // for it the same way as for a warning.
            Log.LogMessage(
                Diagnostics.Subcategory,
                finding.Code,
                "",
                "",
                0,
                0,
                0,
                0,
                MessageImportance.Low,
                message);
            return;
        }

        var asError = finding.Code == Diagnostics.Eol
            ? Flag(EolAsError, true)
            : Flag(TreatAsError, false);

        if (asError)
        {
            Log.LogError(Diagnostics.Subcategory, finding.Code, "", "", 0, 0, 0, 0, message);
        }
        else
        {
            Log.LogWarning(Diagnostics.Subcategory, finding.Code, "", "", 0, 0, 0, 0, message);
        }
    }

    /// <summary>
    /// One report per finding per build, not one per project. A solution of forty projects shares
    /// one SDK.
    /// </summary>
    bool AlreadyReported(Finding finding)
    {
        if (BuildEngine is not IBuildEngine4 engine)
        {
            return false;
        }

        var key = $"SdkCheck|{finding.Code}|{finding.Component.Version}";
        if (engine.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build) != null)
        {
            return true;
        }

        engine.RegisterTaskObject(key, key, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
        return false;
    }

    static string? First(ITaskItem item, params string[] names)
    {
        foreach (var name in names)
        {
            var value = item.GetMetadata(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// MSBuild hands every property across as a string, and an unset one arrives as empty rather
    /// than as the default the consumer would have written.
    /// </summary>
    static bool Flag(string value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return bool.TryParse(value.Trim(), out var parsed) ? parsed : fallback;
    }
}
