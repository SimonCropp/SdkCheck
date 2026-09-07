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

        return version.Substring(0, dash);
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

        return Version.TryParse(Numeric(version!), out var parsed) ? parsed : null;
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
