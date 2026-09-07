namespace SdkCheck;

/// <summary>
/// Diagnostic codes, their short names, and the message shape shared by the MSBuild task and the
/// tool so both render a finding identically.
/// </summary>
public static class Diagnostics
{
    public const string Subcategory = "SdkCheck";

    public const string SdkCve = "SdkCheck001";
    public const string Eol = "SdkCheck002";
    public const string RuntimeCve = "SdkCheck003";
    public const string Unavailable = "SdkCheck004";

    const string docsBaseUrl = "https://github.com/SimonCropp/SdkCheck/blob/main/docs/DiagnosticCodes.md";

    public static string NameFor(string code) =>
        code switch
        {
            SdkCve => "SDK has published CVEs",
            Eol => "Channel is out of support",
            RuntimeCve => "Runtime has published CVEs",
            Unavailable => "Release metadata unavailable",
            _ => code
        };

    public static string DocsUrl(string code) =>
        $"{docsBaseUrl}#{code.ToLowerInvariant()}";

    public static string Render(string code, string body) =>
        $"""
         {NameFor(code)}.
         {body}
         See: {DocsUrl(code)}
         """;
}
