namespace lidaex.Model;

public class PointRule
{
    public PointRule(string name, string id)
    {
        Name = name;
        Id = id;
        Points = new List<double>();
    }

    public string Name { get; set; }
    public string Id { get; set; }
    public IList<double> Points { get; set; }

    public override string ToString()
    {
        return
            $"{nameof(Id)}: {Id}, {nameof(Name)}: {Name}, {nameof(Points)}: {string.Join(' ', Points.Select(x => x.ToString("0.0")))}";
    }
}