using System.Text.Json.Serialization;

namespace TextExpander;

/// <summary>Compile-time JSON metadata, so saving settings works in a trimmed/AOT build.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<Snippet>))]
internal sealed partial class JsonContext : JsonSerializerContext;
