namespace lidaex.Model;

public record TeamTournamentResult(string LichessTournamentId, string TournamentSetId, int Rank, decimal Score, int LichessScore,
    string LeagueName, DateTime TournamentDate)
{
}