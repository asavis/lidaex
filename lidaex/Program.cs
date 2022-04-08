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
    private const string OutputFile = "Турнирная Таблица.json";

    private static readonly IList<PointRule> PointRules = new List<PointRule>();
    private static readonly IList<TournamentSet> TournamentSets = new List<TournamentSet>();
    private static readonly IDictionary<string, Team> Teams = new Dictionary<string, Team>();

    private static TournamentSet? CurrentTournamentSet => TournamentSets.LastOrDefault();
    private static bool IsFirstTournamentSet => TournamentSets.Count < 2;
    private static int NumberOfTournamentsInFirstTournamentSet => TournamentSets.First().NumberOfTournaments;

    private static void Main()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            var configFileName = GetFullConfigFileName();

            ParseAndProcess(configFileName);
            WriteResults();
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
            }
            catch (Exception)
            {
                Con.Error($"Ошибка при обработке строки {curLineNumber}: {curLineValue}");
                throw;
            }
        }

        CheckNumberOrTournamentsInTournamentSet();
    }

    private static void WriteResults()
    {
        Con.Info($"Подготавливаем и записываем \"{OutputFile}\"...");

        var orderedTeams = Teams.Values.OrderByDescending(x => x.Score).ThenByDescending(c => c.LichessScore).ToList();

        var rank = 1;
        foreach (var t in orderedTeams) t.Rank = rank++;

        try
        {
            File.WriteAllText(OutputFile, JsonSerializer.Serialize(orderedTeams));
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Ошибка записи файла с результатом: {e.Message}", e);
        }
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
            decimal v;
            try
            {
                if (!decimal.TryParse(c.Value, NumberStyles.Any, null, out v))
                    v = decimal.Parse(c.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                throw new ApplicationException($"Невозможно разобрать число \"{c.Value}\": {e.Message}", e);
            }

            rule.Points.Add(v);
        }

        if (rule.Points.Count < 3) throw new ApplicationException($"Слишком мало правил начисления очков за места: {rule.Points.Count}");

        PointRules.Add(rule);

        Con.Info(rule.ToString());
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

        if (TournamentSets.Any(x => x.Id == id))
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

        CheckNumberOrTournamentsInTournamentSet();

        TournamentSets.Add(new TournamentSet(name, id, date));

        Con.Info(CurrentTournamentSet!.ToString());
    }

    private static void CheckNumberOrTournamentsInTournamentSet()
    {
        if (CurrentTournamentSet != null && !IsFirstTournamentSet &&
            CurrentTournamentSet.NumberOfTournaments != NumberOfTournamentsInFirstTournamentSet)
            Con.Warn(
                $"Количество турниров в \"_curTournamentSet\" ({CurrentTournamentSet.NumberOfTournaments}) не равно количеству турниров в первом наборе турниров ({NumberOfTournamentsInFirstTournamentSet}). Проверьте корректность данных.");
    }

    private static void CheckIfHavePointRules()
    {
        if (!PointRules.Any()) throw new ApplicationException("Правил начисления очков не найдено");
    }

    private static void ParseAndProcessTournament(string line)
    {
        if (CurrentTournamentSet == null)
            throw new ApplicationException("Отсутствует заголовок турнира перед ссылками на арены");

        ++CurrentTournamentSet.NumberOfTournaments;

        var lichessTournament = GetLichessTournament(line);

        try
        {
            if (lichessTournament == null) throw new ApplicationException("Корень данных отсутствует");

            Con.Info($"{line} {lichessTournament.FullName} {lichessTournament.StartsAt}");

            if (!lichessTournament.IsFinished) throw new ApplicationException("Турнир не окончен");
            if (lichessTournament.Id == null) throw new ApplicationException("Id отсутствует");
            if (lichessTournament.TeamBattle == null) throw new ApplicationException("TeamBattle отсутствует");
            if (lichessTournament.TeamBattle.Teams == null) throw new ApplicationException("TeamBattle.Teams отсутствует");
            if (lichessTournament.TeamStanding == null) throw new ApplicationException("TeamStanding отсутствует");
            if (lichessTournament.FullName == null) throw new ApplicationException("FullName отсутствует");

            if (DateOnly.FromDateTime(lichessTournament.StartsAt.Date) != CurrentTournamentSet.Date)
                Con.Warn(
                    $"Дата турнира \"{DateOnly.FromDateTime(lichessTournament.StartsAt.Date)}\" не соответствует дате из конфигурации \"{CurrentTournamentSet.Date}\". Проверьте корректность данных.");

            if (!lichessTournament.FullName.Contains(CurrentTournamentSet.Id, StringComparison.CurrentCultureIgnoreCase))
                Con.Warn(
                    $"В имени турнира \"{lichessTournament.FullName}\" отсутствует идентификатор из конфигурации \"{CurrentTournamentSet.Id}\". Проверьте корректность данных.");

            var pointRule =
                PointRules.FirstOrDefault(x => lichessTournament.FullName.Contains(x.Id, StringComparison.CurrentCultureIgnoreCase));

            if (pointRule == null)
                throw new ApplicationException(
                    $"Невозможно определить лигу турнира \"{lichessTournament.FullName}\", так как в названии отсутствует один из идентификаторов лиг, описанных в конфигурации: {string.Join('/', PointRules.Select(r => r.Id))}");

            foreach (var team in lichessTournament.TeamBattle.Teams)
                if (!Teams.ContainsKey(team.Key))
                    Teams.Add(team.Key, new Team(team.Key, team.Value));

            foreach (var teamStanding in lichessTournament.TeamStanding)
            {
                if (!Teams.TryGetValue(teamStanding.Id!, out var team))
                    throw new ApplicationException(
                        $"TeamBattle.Teams не содержит идентификатор команды \"{teamStanding.Id}\" из TeamStanding");

                team.LichessScore += teamStanding.Score;
                team.Score += ApplyPointRule(pointRule, teamStanding);
                team.Results.Add(new TeamTournamentResult(lichessTournament.Id, CurrentTournamentSet.Id, teamStanding.Rank,
                    pointRule.Name));
            }
        }
        catch (Exception e)
        {
            throw new ApplicationException(
                $"Ошибка обработки информации о турнире \"{line}\": {e.Message}", e);
        }
    }

    private static Root? GetLichessTournament(string line)
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

        return lichessTournament;
    }

    private static decimal ApplyPointRule(PointRule pointRule, TeamStanding teamStanding)
    {
        if (teamStanding.Rank < 1)
            throw new ApplicationException($"Неверное место команды \"{teamStanding.Id}\" в данных lichess: {teamStanding.Rank}");

        var r = teamStanding.Rank;
        if (r > pointRule.Points.Count) r = pointRule.Points.Count;

        return pointRule.Points[r - 1];
    }

    private enum ConfigSections
    {
        PointRules,
        Tournaments
    }
}