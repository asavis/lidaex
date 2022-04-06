using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using lidaex.Model;
using lidaex.Model.Lichess;

namespace lidaex;

public static class Processor
{
    private const string ConfigFileName1 = "Конфигурация.txt";
    private const string ConfigFileName2 = @"..\..\..\Конфигурация.txt";

    private static readonly IList<PointRule> PointRules = new List<PointRule>();
    private static readonly IList<Tournament> Tournaments = new List<Tournament>();

    private static Tournament? _curTournament;

    private static void Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            var configFileName = GetFullConfigFileName();

            ParseAndProcess(configFileName);
        }
        catch (ApplicationException e)
        {
            Con.Error($"Ошибка: {e.Message}");
        }
        catch (HttpRequestException e)
        {
            Con.Error($"Критическая ошибка: {e}");
        }
    }

    private static void ParseAndProcess(string configFileName)
    {
        var curSection = ConfigSections.PointRules;
        var curLineNumber = 0;

        Con.Info("Правила начисления очков:");

        foreach (var line in File.ReadAllLines(configFileName))
        {
            var curLineValue = line;
            ++curLineNumber;

            try
            {
                var l = line.Trim();

                if (string.IsNullOrWhiteSpace(l) || l.StartsWith("#")) continue;

                if (l.StartsWith("Турнир ", StringComparison.CurrentCultureIgnoreCase))
                {
                    CheckIfHavePointRules();

                    if (curSection != ConfigSections.Tournaments)
                    {
                        curSection = ConfigSections.Tournaments;
                        Con.Info("Турниры:");
                    }

                    ParseAndProcessNewTournamentDefinition(l);
                    continue;
                }

                switch (curSection)
                {
                    case ConfigSections.PointRules:
                        ParseAndProcessPointRules(l);
                        break;
                    case ConfigSections.Tournaments:
                        ParseAndProcessTournament(l);
                        break;
                    default:
                        throw new ApplicationException($"Неизвестно как обрабатывать секцию {curSection}");
                }

                //TODO: for now we process just single tournament
                if (curSection == ConfigSections.Tournaments) break;
            }
            catch (Exception)
            {
                Con.Error($"Ошибка при обработке строки {curLineNumber}: {curLineValue}");
                throw;
            }
        }
    }

    private static void ParseAndProcessNewTournamentDefinition(string line)
    {
        var regex = new Regex(@"^\s*Турнир\s*(.+?)\s*\(s*(.+?)s*\)\s*\[s*(.+?)s*\]\s*\:$", RegexOptions.IgnoreCase);
        var match = regex.Match(line);
        if (!match.Success)
            throw new ApplicationException("Ожидался заголовок турнира. Например: Турнир 1 (97) [2021-12-10]:");

        var name = match.Groups[1].Value;
        var id = match.Groups[2].Value;
        var dateString = match.Groups[3].Value;

        if (Tournaments.Any(x => x.Id == id))
            throw new ApplicationException($"Идентификатор тура не уникальный: {id}");

        DateOnly date;
        try
        {
            date = DateOnly.Parse(dateString);
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Невозможно разобрать дату \"{dateString}\": {e.Message}", e);
        }

        _curTournament = new Tournament(name, id, date);

        Tournaments.Add(_curTournament);

        Con.Info(_curTournament.ToString());
    }

    private static void CheckIfHavePointRules()
    {
        if (!PointRules.Any()) throw new ApplicationException("Правил начисления очков не найдено");
    }

    private static void ParseAndProcessTournament(string line)
    {
        var regex = new Regex(@".+/(.+)$", RegexOptions.IgnoreCase);
        var match = regex.Match(line);
        if (!match.Success)
            throw new ApplicationException(
                "Ожидалась ссылка на турнир. Например: https://lichess.org/tournament/OfHw11H5");

        var id = match.Groups[1].Value;

        var httpClient = new HttpClient();

        var uri = $"https://lichess.org/api/tournament/{id}";

        Root? lichessTournament;

        try
        {
            lichessTournament = JsonSerializer.Deserialize<Root>(httpClient.GetStreamAsync(uri).Result);
        }
        catch (HttpRequestException e)
        {
            throw new ApplicationException($"Невозможно получить информацию о турнире по ссылке \"{uri}\": {e.Message}",
                e);
        }
        catch (JsonException e)
        {
            throw new ApplicationException(
                $"Невозможно разобрать информацию о турнире полученную по ссылке \"{uri}\": {e.Message}", e);
        }

        //TODO: process lichessTournament
    }

    private static void ParseAndProcessPointRules(string line)
    {
        var regex = new Regex(@"Правила\s*начисления\s*очков", RegexOptions.IgnoreCase);
        var match = regex.Match(line);
        if (match.Success) return;

        regex = new Regex(@"^(?:(.+?)\s*(?:\(s*(.+?)s*\)))\s*:s*(?:\s*([\d.,]+))+$");

        match = regex.Match(line);
        if (!match.Success)
            throw new ApplicationException(
                "Ожидалось правило начисления очков. Например: Вища ліга  (D0): 12.0 11.8 11.6 11.4 11.2 11.0 10.8 10.6 10.4 10.2");

        var name = match.Groups[1].Value;
        var id = match.Groups[2].Value.ToLowerInvariant();

        if (PointRules.Any(x => x.Id == id))
            throw new ApplicationException($"Идентификатор лиги турнира не уникальный: {id}");

        var rule = new PointRule(name, id);

        foreach (Capture c in match.Groups[3].Captures)
        {
            double v;
            try
            {
                if (!double.TryParse(c.Value, NumberStyles.Any, null, out v))
                    v = double.Parse(c.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                throw new ApplicationException($"Невозможно разобрать число \"{c.Value}\": {e.Message}", e);
            }

            rule.Points.Add(v);
        }

        PointRules.Add(rule);

        Con.Info(rule.ToString());
    }

    private static string GetFullConfigFileName()
    {
        var path1 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ConfigFileName1));
        var path2 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ConfigFileName2));

        Con.Info($"Ищем файл конфигурации: \"{path1}\"...");

        if (File.Exists(path1)) return path1;

        Con.Info($"Ищем файл конфигурации: \"{path2}\"...");

        if (File.Exists(path2)) return path2;

        throw new ApplicationException("Файл конфигурации не найден");
    }

    private enum ConfigSections
    {
        PointRules,
        Tournaments
    }
}