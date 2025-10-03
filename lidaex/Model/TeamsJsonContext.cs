using lidaex.Model;
using System.Text.Json.Serialization;

namespace lidaex;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Team>))]
internal partial class TeamsJsonContext : JsonSerializerContext
{
}
