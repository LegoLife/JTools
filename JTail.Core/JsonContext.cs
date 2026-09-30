using System.Text.Json.Serialization;

namespace JTail;

/// <summary>Compile-time JSON metadata, so saving settings works in a trimmed/AOT build.</summary>
[JsonSerializable(typeof(List<HighlightRule>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(LogThemeStore.ThemeData))]
internal sealed partial class JsonContext : JsonSerializerContext;
