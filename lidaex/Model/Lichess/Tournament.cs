using System.Text.Json.Serialization;

namespace lidaex.Model.Lichess;

// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
public class Sheet
{
    [JsonPropertyName("scores")] public string? Scores { get; set; }
}

public class Nb
{
    [JsonPropertyName("game")] public int Game { get; set; }

    [JsonPropertyName("berserk")] public int Berserk { get; set; }

    [JsonPropertyName("win")] public int Win { get; set; }
}

public class Podium
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("rank")] public int Rank { get; set; }

    [JsonPropertyName("rating")] public int Rating { get; set; }

    [JsonPropertyName("score")] public int Score { get; set; }

    [JsonPropertyName("sheet")] public Sheet? Sheet { get; set; }

    [JsonPropertyName("team")] public string? Team { get; set; }

    [JsonPropertyName("nb")] public Nb? Nb { get; set; }

    [JsonPropertyName("performance")] public int Performance { get; set; }
}

public class Stats
{
    [JsonPropertyName("games")] public int Games { get; set; }

    [JsonPropertyName("moves")] public int Moves { get; set; }

    [JsonPropertyName("whiteWins")] public int WhiteWins { get; set; }

    [JsonPropertyName("blackWins")] public int BlackWins { get; set; }

    [JsonPropertyName("draws")] public int Draws { get; set; }

    [JsonPropertyName("berserks")] public int Berserks { get; set; }

    [JsonPropertyName("averageRating")] public int AverageRating { get; set; }
}

public class User
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }
}

public class Player
{
    [JsonPropertyName("user")] public User? User { get; set; }

    [JsonPropertyName("score")] public int Score { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("rank")] public int Rank { get; set; }

    [JsonPropertyName("rating")] public int Rating { get; set; }

    [JsonPropertyName("sheet")] public Sheet? Sheet { get; set; }

    [JsonPropertyName("team")] public string? Team { get; set; }
}

public class TeamStanding
{
    [JsonPropertyName("rank")] public int Rank { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("score")] public int Score { get; set; }

    [JsonPropertyName("players")] public List<Player>? Players { get; set; }
}

public class DuelTeams
{
}

public class Standing
{
    [JsonPropertyName("page")] public int Page { get; set; }

    [JsonPropertyName("players")] public List<Player>? Players { get; set; }
}

public class Perf
{
    [JsonPropertyName("key")] public string? Key { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("icon")] public string? Icon { get; set; }
}

public class Clock
{
    [JsonPropertyName("limit")] public int Limit { get; set; }

    [JsonPropertyName("increment")] public int Increment { get; set; }
}

public class Verdicts
{
    [JsonPropertyName("list")] public List<object>? List { get; set; }

    [JsonPropertyName("accepted")] public bool Accepted { get; set; }
}

public class TeamBattle
{
    [JsonPropertyName("teams")] public IDictionary<string, string>? Teams { get; set; }
}

public class Root
{
    [JsonPropertyName("nbPlayers")] public int NbPlayers { get; set; }

    [JsonPropertyName("duels")] public List<object>? Duels { get; set; }

    [JsonPropertyName("isFinished")] public bool IsFinished { get; set; }

    [JsonPropertyName("podium")] public List<Podium>? Podium { get; set; }

    [JsonPropertyName("pairingsClosed")] public bool PairingsClosed { get; set; }

    [JsonPropertyName("stats")] public Stats? Stats { get; set; }

    [JsonPropertyName("teamStanding")] public List<TeamStanding>? TeamStanding { get; set; }

    [JsonPropertyName("duelTeams")] public DuelTeams? DuelTeams { get; set; }

    [JsonPropertyName("standing")] public Standing? Standing { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("createdBy")] public string? CreatedBy { get; set; }

    [JsonPropertyName("startsAt")] public DateTime StartsAt { get; set; }

    [JsonPropertyName("system")] public string? System { get; set; }

    [JsonPropertyName("fullName")] public string? FullName { get; set; }

    [JsonPropertyName("minutes")] public int Minutes { get; set; }

    [JsonPropertyName("perf")] public Perf? Perf { get; set; }

    [JsonPropertyName("clock")] public Clock? Clock { get; set; }

    [JsonPropertyName("variant")] public string? Variant { get; set; }

    [JsonPropertyName("berserkable")] public bool Berserkable { get; set; }

    [JsonPropertyName("noStreak")] public bool NoStreak { get; set; }

    [JsonPropertyName("verdicts")] public Verdicts? Verdicts { get; set; }

    [JsonPropertyName("teamBattle")] public TeamBattle? TeamBattle { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }
}