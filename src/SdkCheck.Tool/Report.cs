namespace SdkCheck.Tool;

class Report
{
    [JsonPropertyName("checked")]
    public List<CheckedComponent> Checked { get; set; } = [];

    [JsonPropertyName("findings")]
    public List<ReportedFinding> Findings { get; set; } = [];
}

class CheckedComponent
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

class ReportedFinding
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("fixedIn")]
    public string? FixedIn { get; set; }

    [JsonPropertyName("eolDate")]
    public string? EolDate { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("cves")]
    public List<ReportedCve> Cves { get; set; } = [];
}

class ReportedCve
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
