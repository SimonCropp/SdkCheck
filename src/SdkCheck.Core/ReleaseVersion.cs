namespace SdkCheck;

/// <summary>
/// Version handling for the release feed, where prerelease versions are common and
/// <see cref="Version.Parse(string)"/> throws on them.
/// </summary>
public static class ReleaseVersion
{
    /// <summary>
    /// The numeric prefix of a version: "11.0.100-rc.1.26431.118" becomes "11.0.100".
    /// </summary>
    public static string Numeric(string version)
    {
        var dash = version.IndexOf('-');
        if (dash < 0)
        {
            return version;
        }

        return version[..dash];
    }

    public static bool IsPrerelease(string version) =>
        version.IndexOf('-') >= 0;

    /// <summary>
    /// Parses the numeric part, or returns null when there isn't one that <see cref="Version"/>
    /// accepts.
    /// </summary>
    /// <remarks>
    /// Version.Parse("8.0.0-rc.2") throws FormatException("The input string '0-rc' was not in a
    /// correct format"), which is why nothing here may hand a raw feed value to Version directly.
    /// The feed carries prerelease release-versions in every channel that has had a preview.
    /// </remarks>
    public static Version? ParseNumeric(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        // ReSharper disable once RedundantSuppressNullableWarningExpression
        return Version.TryParse(Numeric(version!), out var parsed) ? parsed : null;
    }

    /// <summary>
    /// The SDK feature band a version sits on: "8.0.106" and "8.0.199" both give 1, "8.0.204" gives
    /// 2. Null for anything without a patch component.
    /// </summary>
    /// <remarks>
    /// A global.json pinned to a band does not roll across one, so the band is what decides whether
    /// an upgrade recommendation is something the reader can act on.
    /// </remarks>
    public static int? FeatureBand(string? version)
    {
        var parsed = ParseNumeric(version);
        if (parsed == null ||
            parsed.Build < 0)
        {
            return null;
        }

        return parsed.Build / 100;
    }

    /// <summary>
    /// The release channel a version belongs to: "8.0.424" and "8.0.0-rc.2" both give "8.0".
    /// </summary>
    public static string? Channel(string? version)
    {
        var parsed = ParseNumeric(version);
        if (parsed == null)
        {
            return null;
        }

        return $"{parsed.Major}.{Math.Max(parsed.Minor, 0)}";
    }
}
