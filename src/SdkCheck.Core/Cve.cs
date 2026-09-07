namespace SdkCheck;

public class Cve
{
    [JsonPropertyName("cve-id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("cve-url")]
    public string? Url { get; set; }
}
