namespace lidaex.Model;

public record Team(string Id, string Name)
{
    public int? Rank { get; set; }
    public decimal Score { get; set; } = 0;
    public int LichessScore { get; set; } = 0;
    public IList<TeamTournamentResult> Results { get; } = new List<TeamTournamentResult>();
}