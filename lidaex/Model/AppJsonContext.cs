using System.Text.Json.Serialization;
using lidaex.Model.Lichess;

namespace lidaex.Model;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Root))]             
[JsonSerializable(typeof(TeamsRoot))]         
[JsonSerializable(typeof(List<Team>))]        
internal partial class AppJsonContext : JsonSerializerContext
{
}
