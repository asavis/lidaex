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
    private const string ConfigFileName1 = "Конфігурація.txt";
    private const string ConfigFileName2 = @"..\..\..\Конфігурація.txt";
    private const string UploadConfigFileName1 = "Конфігурація відправлення.txt";
    private const string UploadConfigFileName2 = @"..\..\..\Конфігурація відправлення.txt";
    private const string OutputFile = "standings.json";

    private static readonly IList<PointRule> PointRules = new List<PointRule>();
    private static readonly IList<TournamentSet> TournamentSets = new List<TournamentSet>();
    private static readonly IDictionary<string, Team> Teams = new Dictionary<string, Team>();

    private static readonly Regex PointRulesTitleRegex = new(@"Правила\s*нарахування\s*очок", RegexOptions.IgnoreCase);
    private static readonly Regex PointRulesRegex = new(@"^(?:(.+?)\s*(?:\(s*(.+?)s*\)))\s*:s*(?:\s*([\d.,]+))+$");
    private static readonly Regex LichessTournamentUriRegex = new(".+/(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex HostRegex = new(@"^\s*FTP_Host\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex UserRegex = new(@"^\s*User\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex PasswordRegex = new(@"^\s*Password\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex NewTournamentDefinitionRegex =
        new(@"^\s*Турнір\s*\(s*(.+?)s*\)\s*\[s*(.+?)s*\]\s*\:$", RegexOptions.IgnoreCase);

    private static TournamentSet? CurrentTournamentSet => TournamentSets.LastOrDefault();
    private static bool IsFirstTournamentSet => TournamentSets.Count < 2;
    private static int NumberOfTournamentsInFirstTournamentSet => TournamentSets.First().NumberOfTournaments;
    private static string UploadHost { get; set; } = Empty;
    private static string UploadUser { get; set; } = Empty;
    private static string UploadPassword { get; set; } = Empty;
    private static bool UnfinishedTournamentsFound { get; set; }

    private static void Main(string[] args)
    {
        var isSilent = (args.Length > 0 && args[0] == "/s") || (args.Length > 1 && args[1] == "/s");
        var isUploadOnlyMode = (args.Length > 0 && args[0] == "/u") || (args.Length > 1 && args[1] == "/u");

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            var configFileName = GetFullConfigFileName();

            if (!isUploadOnlyMode) ParseAndProcess(configFileName);

            if (UnfinishedTournamentsFound)
            {
                Con.Warn("Знайдено один або декілька незакінчених турнірів. Результати записуватися не будуть.");
            }
            else
            {
                if (!isUploadOnlyMode) WriteResults();

                var uploadConfigFileName = GetFullUploadConfigFileName();
                if (!IsNullOrEmpty(uploadConfigFileName))
                {
                    ReadUploadConfig(uploadConfigFileName);
                    if (isSilent || HaveUserUploadConfirmation()) UploadResults();
                }
            }
        }
        catch (ApplicationException e)
        {
            Con.Error($"Помилка: {e.Message}");
        }
        catch (Exception e)
        {
            Con.Error($"Критична помилка: {e}");
        }

        if (!isSilent)
        {
            Con.Info("\nНатисніть будь-яку клавішу для виходу...");
            Console.ReadKey();
        }
    }

    private static string GetFullConfigFileName()
    {
        var path1 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ConfigFileName1));
        var path2 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ConfigFileName2));

        Con.Info($"Шукаємо файл конфігурації: \"{path1}\"...");

        if (File.Exists(path1)) return path1;

        Con.Info($"Шукаємо файл конфігурації: \"{path2}\"...");

        if (File.Exists(path2)) return path2;

        throw new ApplicationException("Файл конфігурації не знайдено");
    }

    private static string GetFullUploadConfigFileName()
    {
        var path1 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, UploadConfigFileName1));
        var path2 = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, UploadConfigFileName2));

        Con.Info($"Шукаємо файл конфігурації відправлення: \"{path1}\"...");

        if (File.Exists(path1)) return path1;

        Con.Info($"Шукаємо файл конфігурації відправлення: \"{path2}\"...");

        if (File.Exists(path2)) return path2;

        Con.Warn("Файл конфігурації відправлення не знайдено");

        return Empty;
    }

    private static void ReadUploadConfig(string uploadConfigFileName)
    {
        var uploadConfigText = File.ReadAllText(uploadConfigFileName);

        var hostMatch = HostRegex.Match(uploadConfigText);
        var userMatch = UserRegex.Match(uploadConfigText);
        var passwordMatch = PasswordRegex.Match(uploadConfigText);

        if (!hostMatch.Success) throw new ApplicationException("Елемент FTP_Host не знайдено в файлі конфігурації відправлення");
        if (!userMatch.Success) throw new ApplicationException("Елемент User не знайдено в файлі конфігурації відправлення");
        if (!passwordMatch.Success) throw new ApplicationException("Елемент Password не знайдено в файлі конфігурації відправлення");

        UploadHost = hostMatch.Groups[1].Value;
        UploadUser = userMatch.Groups[1].Value;
        UploadPassword = passwordMatch.Groups[1].Value;
    }

    private static bool HaveUserUploadConfirmation()
    {
        if (Con.WarnCounter > 0 || Con.ErrorCounter > 0) Con.Warn("Увага! Під час обробки даних виникли попередження.");

        Con.Info("");

        string? response;
        do
        {
            Con.Info($"Бажаєте відправити файл даних \"{OutputFile}\" за адресою \"{UploadHost}\"? [т/н]");
            response = Console.ReadLine()?.ToLower(CultureInfo.CurrentCulture);
            if (response == "т") return true;
        } while (response != "н");

        return false;
    }

    private static void UploadResults()
    {
        try
        {
            Con.Info($"Відправляємо файл даних \"{OutputFile}\" за адресою \"{UploadHost}\"...");

            var client = new FtpClient(UploadHost, UploadUser, UploadPassword);
            client.Connect();

            var nextProgressLog = 10;
            const int progressLogStep = 10;

            client.UploadFile(OutputFile,
                OutputFile,
                FtpRemoteExists.Overwrite,
                false,
                FtpVerify.Retry,
                delegate(FtpProgress progress)
                {
                    if (progress.Progress > nextProgressLog)
                    {
                        Con.Info($"{nextProgressLog}%");
                        nextProgressLog += progressLogStep;
                    }
                });

            client.Disconnect();
            Con.Info("100%");
            Con.Info($"Файл \"{OutputFile}\" успішно доставлено за адресою \"{UploadHost}\"");
        }
        catch (Exception)
        {
            Con.Error($"Помилка відправки файлу \"{OutputFile}\" за адресою \"{UploadHost}\"");
            throw;
        }
    }

    private static void ParseAndProcess(string configFileName)
    {
        var curSection = ConfigSections.PointRules;
        var curLineNumber = 0;

        Con.Info("Правила нарахування очок:");

        foreach (var line in File.ReadAllLines(configFileName))
        {
            var curLineValue = line;
            ++curLineNumber;

            try
            {
                var l = line.Trim();

                if (IsNullOrWhiteSpace(l) || l.StartsWith("#")) continue;

                if (l.StartsWith("Турнір ", StringComparison.CurrentCultureIgnoreCase))
                {
                    CheckIfHavePointRules();

                    if (curSection != ConfigSections.Tournaments)
                    {
                        curSection = ConfigSections.Tournaments;
                        Con.Info("Турніри:");
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
                        throw new ApplicationException($"Невідомо, як обробляти секцію {curSection}");
                }
            }
            catch (Exception)
            {
                Con.Error($"Помилка при обробці рядка {curLineNumber}: {curLineValue}");
                throw;
            }
        }

        CheckNumberOrTournamentsInTournamentSet();
    }

    private static void WriteResults()
    {
        Con.Info($"Підготовлюємо та записуємо \"{OutputFile}\"...");

        var orderedTeams = Teams.Values.OrderByDescending(x => x.Score).ThenByDescending(c => c.LichessScore).ToList();

        var rank = 1;
        foreach (var t in orderedTeams) t.Rank = rank++;

        try
        {
            File.WriteAllText(OutputFile, JsonSerializer.Serialize(orderedTeams, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Помилка запису файла з результатом: {e.Message}", e);
        }
    }

    private static void ParseAndProcessPointRules(string line)
    {
        var match = PointRulesTitleRegex.Match(line);
        if (match.Success) return;

        match = PointRulesRegex.Match(line);
        if (!match.Success)
            throw new ApplicationException(
                "Очікувалося правило нарахування очок. Наприклад: Вища ліга  (D0): 12.0 11.8 11.6 11.4 11.2 11.0 10.8 10.6 10.4 10.2");

        var name = match.Groups[1].Value;
        var id = match.Groups[2].Value.ToUpper(CultureInfo.CurrentCulture);

        if (PointRules.Any(x => x.Id == id))
            throw new ApplicationException($"Ідентифікатор ліги турніру не є унікальним: {id}");

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
                throw new ApplicationException($"Неможливо розібрати число \"{c.Value}\": {e.Message}", e);
            }

            rule.Points.Add(v);
        }

        if (rule.Points.Count < 3) throw new ApplicationException($"Занадто мало правил нарахування очок за місця: {rule.Points.Count}");

        PointRules.Add(rule);

        Con.Info(rule.ToString());
    }


    private static void ParseAndProcessNewTournamentDefinition(string line)
    {
        var match = NewTournamentDefinitionRegex.Match(line);
        if (!match.Success)
            throw new ApplicationException("Очікувався заголовок турніру. Наприклад: Турнір 1 (97) [2021-12-10]:");

        var id = match.Groups[1].Value;
        var dateString = match.Groups[2].Value;

        if (TournamentSets.Any(x => x.Id == id))
            throw new ApplicationException($"Ідентифікатор туру не є унікальним: {id}");

        DateOnly date;
        try
        {
            date = DateOnly.Parse(dateString);
        }
        catch (Exception e)
        {
            throw new ApplicationException($"Неможливо розібрати дату \"{dateString}\": {e.Message}", e);
        }

        CheckNumberOrTournamentsInTournamentSet();

        TournamentSets.Add(new TournamentSet(id, date));

        Con.Info($"Турнір {CurrentTournamentSet}");
    }


    private static void CheckNumberOrTournamentsInTournamentSet()
    {
        if (CurrentTournamentSet != null && !IsFirstTournamentSet &&
            CurrentTournamentSet.NumberOfTournaments != NumberOfTournamentsInFirstTournamentSet)
            Con.Warn(
                $"Кількість турнірів в \"_curTournamentSet\" ({CurrentTournamentSet.NumberOfTournaments}) не дорівнює кількості турнірів у першому наборі турнірів ({NumberOfTournamentsInFirstTournamentSet}). Перевірте коректність даних.");
    }

    private static void CheckIfHavePointRules()
    {
        if (!PointRules.Any()) throw new ApplicationException("Правил начислення очок не знайдено");
    }


    private static void ParseAndProcessTournament(string line)
    {
        if (CurrentTournamentSet == null)
            throw new ApplicationException("Відсутній заголовок турніру перед посиланнями на арени");

        ++CurrentTournamentSet.NumberOfTournaments;

        var lichessTournament = GetLichessTournament(line);

        try
        {
            if (lichessTournament == null) throw new ApplicationException("Корінь даних відсутній");

            Con.Info($"{line} {lichessTournament.FullName} {lichessTournament.StartsAt.ToLocalTime():g}");

            if (!lichessTournament.IsFinished)
            {
                UnfinishedTournamentsFound = true;
                Con.Warn($"Турнір не завершено: \"{line}\"");
            }

            if (lichessTournament.Id == null) throw new ApplicationException("Id відсутній");
            if (lichessTournament.TeamBattle == null) throw new ApplicationException("TeamBattle відсутній");
            if (lichessTournament.TeamBattle.Teams == null) throw new ApplicationException("TeamBattle.Teams відсутній");
            if (lichessTournament.TeamStanding == null) throw new ApplicationException("TeamStanding відсутній");
            if (lichessTournament.FullName == null) throw new ApplicationException("FullName відсутній");

            if (DateOnly.FromDateTime(lichessTournament.StartsAt.Date) != CurrentTournamentSet.Date)
                Con.Warn(
                    $"Дата турніру \"{DateOnly.FromDateTime(lichessTournament.StartsAt.Date)}\" не відповідає даті з конфігурації \"{CurrentTournamentSet.Date}\". Перевірте коректність даних.");

            if (!lichessTournament.FullName.Contains(CurrentTournamentSet.Id, StringComparison.CurrentCultureIgnoreCase))
                Con.Warn(
                    $"У назві турніру \"{lichessTournament.FullName}\" відсутній ідентифікатор з конфігурації \"{CurrentTournamentSet.Id}\". Перевірте коректність даних.");

            var pointRule =
                PointRules.FirstOrDefault(x => lichessTournament.FullName.Contains(x.Id, StringComparison.CurrentCultureIgnoreCase));

            if (pointRule == null)
            {
                pointRule =
                    PointRules.FirstOrDefault(x => lichessTournament.FullName.Contains(
                        x.Id.Substring(0, 1) + 
                        "-" + 
                        x.Id.Substring(1), StringComparison.CurrentCultureIgnoreCase));

                if (pointRule == null)
                {
                    throw new ApplicationException(
                        $"Неможливо визначити лігу турніру \"{lichessTournament.FullName}\", так як у назві відсутній один з ідентифікаторів ліг, описаних у конфігурації: {Join('/', PointRules.Select(r => r.Id))}");
                }

                Con.Warn($"Тимчасове рішення використано! Визначена ліга: \"{pointRule.Name}\". Ідентифікатор ліги має бути одним із описаних у конфігурації: {Join('/', PointRules.Select(r => r.Id))}");
            }

            foreach (var team in lichessTournament.TeamBattle.Teams)
                if (!Teams.ContainsKey(team.Key))
                    Teams.Add(team.Key, new Team(team.Key, team.Value.First())); 

            foreach (var teamStanding in lichessTournament.TeamStanding)
            {
                if (!Teams.TryGetValue(teamStanding.Id!, out var team))
                    throw new ApplicationException(
                        $"TeamBattle.Teams не містить ідентифікатор команди \"{teamStanding.Id}\" з TeamStanding");

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
                $"Помилка обробки інформації про турнір \"{line}\": {e.Message}", e);
        }
    }


    private static Root? GetLichessTournament(string line)
    {
        var match = LichessTournamentUriRegex.Match(line);
        if (!match.Success)
            throw new ApplicationException(
                "Очікувалось посилання на турнір. Наприклад: https://lichess.org/tournament/OfHw11H5");

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
            throw new ApplicationException($"Неможливо отримати інформацію про турнір за посиланням \"{uri}\": {e.Message}",
                e);
        }
        catch (JsonException e)
        {
            throw new ApplicationException(
                $"Неможливо розібрати інформацію про турнір, отриману за посиланням \"{uri}\": {e.Message}", e);
        }

        if (lichessTournament is { TeamBattle.Teams.Count: > 10 })
        {
            uri += "/teams";

            try
            {
                lichessTournament.TeamStanding = JsonSerializer.Deserialize<TeamsRoot>(httpClient.GetStreamAsync(uri).Result)!.TeamStanding;
            }
            catch (HttpRequestException e)
            {
                throw new ApplicationException($"Неможливо отримати інформацію про команди турніру за посиланням \"{uri}\": {e.Message}",
                    e);
            }
            catch (JsonException e)
            {
                throw new ApplicationException(
                    $"Неможливо розібрати інформацію про команди турніру, отриману за посиланням \"{uri}\": {e.Message}", e);
            }
        }

        return lichessTournament;
    }

    private static decimal ApplyPointRule(PointRule pointRule, TeamStanding teamStanding)
    {
        if (teamStanding.Score == 0) return 0;

        if (teamStanding.Rank < 1)
            throw new ApplicationException($"Невірне місце команди \"{teamStanding.Id}\" у даних lichess: {teamStanding.Rank}");

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