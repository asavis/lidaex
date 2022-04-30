namespace lidaex.Model;

public record TournamentSet(string Id, DateOnly Date)
{
    public int NumberOfTournaments { get; set; }

    public override string ToString()
    {
        return $"{nameof(Id)}: {Id}, {nameof(Date)}: {Date}";
    }
}