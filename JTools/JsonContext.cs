using System.Text.Json.Serialization;
using JTail;
using TextExpander;

namespace JTools;

/// <summary>Compile-time JSON metadata, so saving settings works in a trimmed/AOT build.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(JToolsSettings))]
[JsonSerializable(typeof(RegexTesterState))]
// The other tools' data, read only to check an import before it's written.
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<Snippet>))]
[JsonSerializable(typeof(List<HighlightRule>))]
internal sealed partial class JsonContext : JsonSerializerContext;
