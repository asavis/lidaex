namespace lidaex.Model;

public record PointRule(string Name, string Id)
{
    public IList<decimal> Points { get; init; } = new List<decimal>();

    public override string ToString()
    {
        return
            $"{nameof(Id)}: {Id}, {nameof(Name)}: {Name}, {nameof(Points)}: {string.Join(' ', Points.Select(x => x.ToString("0.0")))}";
    }
}