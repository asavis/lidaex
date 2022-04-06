namespace lidaex.Model;

public class Tournament
{
    public Tournament(string name, string id, DateOnly date)
    {
        Name = name;
        Id = id;
        Date = date;
    }

    public string Name { get; set; }
    public string Id { get; set; }
    public DateOnly Date { get; set; }

    public override string ToString()
    {
        return $"{nameof(Name)}: {Name}, {nameof(Id)}: {Id}, {nameof(Date)}: {Date}";
    }
}