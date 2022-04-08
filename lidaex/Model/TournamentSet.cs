namespace lidaex.Model;

public record TournamentSet(string Name, string Id, DateOnly Date)
{
    public int NumberOfTournaments { get; set; }

    public override string ToString()
    {
        return $"{nameof(Name)}: {Name}, {nameof(Id)}: {Id}, {nameof(Date)}: {Date}";
    }
}