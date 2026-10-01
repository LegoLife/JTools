using System.Text.Json.Serialization;

namespace JTools;

/// <summary>Compile-time JSON metadata, so saving settings works in a trimmed/AOT build.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(JToolsSettings))]
[JsonSerializable(typeof(RegexTesterState))]
internal sealed partial class JsonContext : JsonSerializerContext;
