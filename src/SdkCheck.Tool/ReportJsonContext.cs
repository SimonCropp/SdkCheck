namespace SdkCheck.Tool;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Report))]
partial class ReportJsonContext : JsonSerializerContext;
