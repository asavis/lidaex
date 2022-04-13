using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentFTP;
using lidaex.Model;
using lidaex.Model.Lichess;
using static System.String;

namespace lidaex;

public static class Processor
{
    private const string ConfigFileName1 = "Конфигурация.txt";
    private const string ConfigFileName2 = @"..\..\..\Конфигурация.txt";
    private const string UploadConfigFileName1 = "Конфигурация отправки.txt";
    private const string UploadConfigFileName2 = @"..\..\..\Конфигурация отправки.txt";

    private const string OutputFile = "standings.json";

    private static readonly IList<PointRule> PointRules = new List<PointRule>();
    private static readonly IList<TournamentSet> TournamentSets = new List<TournamentSet>();
    private static readonly IDictionary<string, Team> Teams = new Dictionary<string, Team>();

    private static readonly Regex PointRulesTitleRegex = new(@"Правила\s*начисления\s*очков", RegexOptions.IgnoreCase);
    private static readonly Regex PointRulesRegex = new(@"^(?:(.+?)\s*(?:\(s*(.+?)s*\)))\s*:s*(?:\s*([\d.,]+))+$");
    private static readonly Regex LichessTournamentUriRegex = new(@".+/(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex HostRegex = new(@"^\s*FTP_Host\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex UserRegex = new(@"^\s*User\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex PasswordRegex = new(@"^\s*Password\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex NewTournamentDefinitionRegex =
        new(@"^\s*Турнир\s*(.+?)\s*\(s*(.+?)s*\)\s*\[s*(.+?)s*\]\s*\:$", RegexOptions.IgnoreCase);

    private static TournamentSet? CurrentTournamentSet => TournamentSets.LastOrDefault();
    private static bool IsFirstTournamentSet => TournamentSets.Count < 2;
    private static int NumberOfTournamentsInFirstTournamentSet => TournamentSets.First().NumberOfTournaments;

    private static string UploadHost { get; set; } = Empty;
    private static string UploadUser { get; set; } = Empty;
    private static string UploadPassword { get; set; } = Empty;

    private static void Main(string[] args)
    {
        var isSilent = args.Length > 0 && args[0] == "/s";

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            var configFileName = GetFullConfigFileName();

            ParseAndProcess(configFileName);
            WriteResults();

            var uploadConfigFileName = GetFullUploadConfigFileName();
            if (!IsNullOrEmpty(uploadConfigFileName))
            {
                ReadUploadConfig(uploadConfigFileName);
                if (isSilent || HaveUserUploadConfirmation()) UploadResults();
            }
        }
        catch (ApplicationException e)
        {
            Con.Error($"Ошибка: {e.Message}");
        }
        catch (Exception e)
        {
            Con.Error($"Критическая ошибка: {e}");
        }

        if (!isSilent)
        {
            Con.Info("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
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

    private static string GetFullUploadConfigFileName()
    {
        var path1 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, UploadConfigFileName1));
        var path2 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, UploadConfigFileName2));

        Con.Info($"Ищем файл конфигурации отправки: \"{path1}\"...");

        if (File.Exists(path1)) return path1;

        Con.Info($"Ищем файл конфигурации отправки: \"{path2}\"...");

        if (File.Exists(path2)) return path2;

        Con.Warn("Файл конфигурации отправки не найден");

        return Empty;
    }

    private static void ReadUploadConfig(string uploadConfigFileName)
    {
        var uploadConfigText = File.ReadAllText(uploadConfigFileName);

        var hostMatch = HostRegex.Match(uploadConfigText);
        var userMatch = UserRegex.Match(uploadConfigText);
        var passwordMatch = PasswordRegex.Match(uploadConfigText);

        if (!hostMatch.Success) throw new ApplicationException("Определение FTP_Host не найдено в файле конфигурации отправки");
        if (!userMatch.Success) throw new ApplicationException("Определение User не найдено в файле конфигурации отправки");
        if (!passwordMatch.Success) throw new ApplicationException("Определение Password не найдено в файле конфигурации отправки");

        UploadHost = hostMatch.Groups[1].Value;
        UploadUser = userMatch.Groups[1].Value;
        UploadPassword = passwordMatch.Groups[1].Value;
    }

    private static bool HaveUserUploadConfirmation()
    {
        if (Con.WarnCounter > 0 || Con.ErrorCounter > 0) Con.Warn("Внимание! Во время обработки данных возникли предупреждения.");

        Con.Info("");

        string? response;
        do
        {
            Con.Info($"Хотите отправить файл данных \"{OutputFile}\" по адресу \"{UploadHost}\"? [д/н]");
            response = Console.ReadLine()?.ToLower(CultureInfo.CurrentCulture);
            if (response == "д") return true;
        } while (response != "н");

        return false;
    }

    private static void UploadResults()
    {
        try
        {
            var client = new FtpClient(UploadHost, UploadUser, UploadPassword);
            client.AutoConnect();
            client.UploadFile(OutputFile, OutputFile);
            client.Disconnect();
        }
        catch (Exception)
        {
            Con.Error($"Ошибка отправки файла \"{OutputFile}\" по адресу \"{UploadHost}\"");
            throw;
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

                if (IsNullOrWhiteSpace(l) || l.StartsWith("#")) continue;

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
            File.WriteAllText(OutputFile, JsonSerializer.Serialize(orderedTeams, new JsonSerializerOptions {WriteIndented = true}));
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Ошибка записи файла с результатом: {e.Message}", e);
        }
    }

    private static void ParseAndProcessPointRules(string line)
    {
        var match = PointRulesTitleRegex.Match(line);
        if (match.Success) return;

        match = PointRulesRegex.Match(line);
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
        var match = NewTournamentDefinitionRegex.Match(line);
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
                    $"Невозможно определить лигу турнира \"{lichessTournament.FullName}\", так как в названии отсутствует один из идентификаторов лиг, описанных в конфигурации: {Join('/', PointRules.Select(r => r.Id))}");

            foreach (var team in lichessTournament.TeamBattle.Teams)
                if (!Teams.ContainsKey(team.Key))
                    Teams.Add(team.Key, new Team(team.Key, team.Value));

            foreach (var teamStanding in lichessTournament.TeamStanding)
            {
                if (!Teams.TryGetValue(teamStanding.Id!, out var team))
                    throw new ApplicationException(
                        $"TeamBattle.Teams не содержит идентификатор команды \"{teamStanding.Id}\" из TeamStanding");

                team.LichessScore += teamStanding.Score;
                var score = ApplyPointRule(pointRule, teamStanding);
                team.Score += score;
                team.Results.Add(new TeamTournamentResult(
                    lichessTournament.Id,
                    CurrentTournamentSet.Id,
                    teamStanding.Rank,
                    score,
                    teamStanding.Score,
                    pointRule.Name,
                    lichessTournament.StartsAt.Date));
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
        var match = LichessTournamentUriRegex.Match(line);
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