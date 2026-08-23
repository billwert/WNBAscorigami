using HtmlAgilityPack;
using Newtonsoft.Json;

namespace WNBAScorigami;

class Scraper
{
    private readonly Scorigami[,] Scorigamis = new Scorigami[150, 150];
    private readonly List<WnbaGame> allGames = new(5000);
    private string dataDir = "data";

    /// <summary>
    /// True when the current season came from the cache because the live scrape failed.
    /// </summary>
    public bool CurrentYearFromCache { get; private set; }

    public static (List<ScorigamiData> data, bool currentYearFromCache) Run(string dataDir)
    {
        var scraper = new Scraper { dataDir = dataDir };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        scraper.LoadGameData();
        Console.WriteLine($"LoadGameData: {sw.Elapsed.TotalSeconds:F2}s");
        sw.Restart();

        scraper.CalculateScorigamis();
        Console.WriteLine($"CalculateScorigamis: {sw.Elapsed.TotalSeconds:F2}s");
        sw.Restart();

        var result = scraper.BuildScorigamiData();
        Console.WriteLine($"BuildScorigamiData: {sw.Elapsed.TotalSeconds:F2}s");
        return (result, scraper.CurrentYearFromCache);
    }

    private List<ScorigamiData> BuildScorigamiData()
    {
        var data = new List<ScorigamiData>();
        for (int i = 0; i < 150; i++)
        {
            for (int j = 0; j < 150; j++)
            {
                var scorigami = Scorigamis[i, j];
                if (scorigami == null)
                    continue;
                var first = scorigami.First!;
                var last = scorigami.Latest ?? scorigami.First!;
                data.Add(new ScorigamiData
                {
                    pts_lose = j,
                    pts_win = i,
                    count = scorigami.Count,
                    first_date = first.GameDate,
                    first_team_win = first.WinTeam,
                    first_team_lose = first.LoseTeam,
                    first_team_home = first.Home,
                    first_team_away = first.Away,
                    first_link = first.BoxScoreURL,
                    last_date = last.GameDate,
                    last_team_win = last.WinTeam,
                    last_team_lose = last.LoseTeam,
                    last_team_home = last.Home,
                    last_team_away = last.Away,
                    last_link = last.BoxScoreURL
                });
            }
        }
        return data;
    }

    public static void WriteScorigamiData(string dataDir, List<ScorigamiData> data)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var output = new { games = data, lastUpdated = DateTime.UtcNow };
        var json = JsonConvert.SerializeObject(output, Formatting.Indented);
        Directory.CreateDirectory(dataDir);
        var path = Path.Join(dataDir, "scorigamidata.json");
        File.WriteAllText(path, json);
        Console.WriteLine($"Wrote {data.Count} scorigami records to {path}");
        Console.WriteLine($"WriteScorigamiData: {sw.Elapsed.TotalSeconds:F2}s");
    }

    private void SaveYear(List<WnbaGame> games)
    {
        var gamesDir = Path.Join(dataDir, "games");
        Directory.CreateDirectory(gamesDir);
        var path = Path.Join(gamesDir, GameFileName(games[0].Year));
        File.WriteAllText(path, JsonConvert.SerializeObject(games, Formatting.Indented));
        Console.WriteLine($"Saved {games.Count} games for {games[0].Year} to {path}");
    }

    private List<WnbaGame>? ReadCachedYear(int year)
    {
        var path = Path.Join(dataDir, "games", GameFileName(year));
        if (!File.Exists(path))
        {
            Console.WriteLine($"Cache missing for {year}");
            return null;
        }
        var games = JsonConvert.DeserializeObject<List<WnbaGame>>(File.ReadAllText(path));
        if (games == null)
        {
            Console.WriteLine($"Failed to deserialize {path}");
            return null;
        }
        return games;
    }

    private List<WnbaGame> LoadYear(int year)
    {
        var games = ReadCachedYear(year);
        if (games == null)
            return ScrapeYear(year);
        return games;
    }

    /// <summary>
    /// The current season is always re-scraped, but a scrape that fails falls back to the
    /// cache rather than silently contributing zero games to the dataset.
    /// </summary>
    private List<WnbaGame> LoadCurrentYear(int year)
    {
        try
        {
            return ScrapeYear(year);
        }
        catch (ScrapeFailedException ex)
        {
            Console.WriteLine($"WARNING: live scrape of {year} failed: {ex.Message}");
            var cached = ReadCachedYear(year);
            if (cached == null)
                throw new ScrapeFailedException($"{year} could not be scraped and has no usable cache");
            Console.WriteLine($"Falling back to cached {year} data ({cached.Count} games)");
            CurrentYearFromCache = true;
            return cached;
        }
    }

    private List<WnbaGame> ScrapeYear(int year)
    {
        Console.WriteLine($"Scraping BBRef for {year}...");
        var url = $"https://www.basketball-reference.com/wnba/years/{year}_games.html";
        var games = new List<WnbaGame>();
        var failure = AddGamesFromBBRef(games, url);
        if (failure != null)
            throw new ScrapeFailedException(failure);
        Console.WriteLine($"Scraped {games.Count} games for {year}");
        if (games.Count != 0)
            SaveYear(games);
        return games;
    }

    private static string GameFileName(int year) => $"{year}_games.json";

    public void LoadGameData()
    {
        for (int i = LeagueInfo.START_YEAR; i <= DateTime.Now.Year; i++)
        {
            List<WnbaGame> games;
            if (i == DateTime.Now.Year)
                games = LoadCurrentYear(i);
            else
                games = LoadYear(i);

            allGames.AddRange(games);
        }
    }

    public void CalculateScorigamis()
    {
        foreach (var game in allGames)
        {
            int higherScore = game.WinScore;
            int lowerScore = game.LoseScore;

            Scorigamis[higherScore, lowerScore] ??= new Scorigami();
            Scorigamis[higherScore, lowerScore].Count++;

            if (Scorigamis[higherScore, lowerScore].First == null ||
                Scorigamis[higherScore, lowerScore].First!.GameDate >= game.GameDate)
            {
                Scorigamis[higherScore, lowerScore].First = game;
            }
            else if (Scorigamis[higherScore, lowerScore].Latest == null ||
                     Scorigamis[higherScore, lowerScore].Latest!.GameDate <= game.GameDate)
            {
                Scorigamis[higherScore, lowerScore].Latest = game;
            }
        }
    }

    /// <summary>
    /// Returns null on success, or a description of why the page could not be read as a
    /// schedule. An unreadable page must never be mistaken for a season with no games.
    /// </summary>
    private static string? AddGamesFromBBRef(List<WnbaGame> games, string seasonUrl)
    {
        var web = new HtmlWeb();
        HtmlDocument doc;
        try
        {
            doc = web.Load(seasonUrl);
        }
        catch (Exception ex)
        {
            return $"request to {seasonUrl} threw {ex.GetType().Name}: {ex.Message}";
        }

        int status = (int)web.StatusCode;
        if (status != 0 && status != 200)
            return $"{seasonUrl} unreadable{Diagnostics(doc, status)}";

        var gameRows = doc.DocumentNode?.SelectNodes("//*[@id=\"schedule\"]/tbody/tr");
        if (gameRows == null)
            return $"no #schedule table at {seasonUrl}{Diagnostics(doc, status)}";

        bool playoffGame = false;
        foreach (var gameRow in gameRows)
        {
            if (gameRow.ChildNodes[0].InnerText == "Playoffs")
            {
                playoffGame = true;
                continue;
            }

            // Empty score means unplayed future game — stop processing
            if (gameRow.ChildNodes[2].InnerText == "")
                break;

            var game = new WnbaGame
            {
                GameDate = DateTime.Parse(gameRow.ChildNodes[0].InnerText),
                Away = gameRow.ChildNodes[1].InnerText,
                AwayScore = int.Parse(gameRow.ChildNodes[2].InnerText),
                Home = gameRow.ChildNodes[3].InnerText,
                HomeScore = int.Parse(gameRow.ChildNodes[4].InnerText),
                IsPlayoffGame = playoffGame
            };

            var boxLink = gameRow.ChildNodes[5]?.FirstChild?.Attributes?[0]?.Value;
            if (boxLink != null)
                game.BoxScoreURL = "https://www.basketball-reference.com" + boxLink;

            games.Add(game);
        }
        return null;
    }

    /// <summary>Enough of the response to tell a rate limit from a markup change.</summary>
    private static string Diagnostics(HtmlDocument doc, int status)
    {
        // Every accessor here can throw on a document that never parsed, which is exactly
        // the case this runs in — so nothing in it is allowed to escape.
        string raw = "";
        try { raw = doc.Text ?? ""; } catch { }
        if (raw.Length == 0)
            return $" [HTTP {status}, empty response]";

        string? title = null;
        string body = raw;
        try
        {
            title = doc.DocumentNode?.SelectSingleNode("//title")?.InnerText?.Trim();
            // Visible text says far more than raw markup on an error or rate-limit page
            body = doc.DocumentNode?.SelectSingleNode("//body")?.InnerText ?? raw;
        }
        catch { }

        var text = System.Text.RegularExpressions.Regex.Replace(
            System.Net.WebUtility.HtmlDecode(body) ?? "", @"\s+", " ").Trim();
        if (text.Length > 300)
            text = text[..300] + "...";

        return $" [HTTP {status}, {raw.Length} bytes, title={title ?? "<none>"}, body=\"{text}\"]";
    }
}

/// <summary>
/// Thrown when BBRef could not be read — as distinct from a season that legitimately has
/// no games played yet.
/// </summary>
class ScrapeFailedException : Exception
{
    public ScrapeFailedException(string message) : base(message) { }
}

class Scorigami
{
    public WnbaGame? First { get; set; }
    public WnbaGame? Latest { get; set; }
    public int Count { get; set; }
}

class WnbaGame
{
    public int Year => GameDate.Year;
    public DateTime GameDate { get; set; } = DateTime.MinValue;
    public string Away { get; set; } = "";
    public string Home { get; set; } = "";
    public int AwayScore { get; set; }
    public int HomeScore { get; set; }
    public string BoxScoreURL { get; set; } = "";
    public bool IsPlayoffGame { get; set; }
    public bool HomeWon => HomeScore > AwayScore;
    public int WinScore => HomeWon ? HomeScore : AwayScore;
    public string WinTeam => HomeWon ? Home : Away;
    public int LoseScore => HomeWon ? AwayScore : HomeScore;
    public string LoseTeam => HomeWon ? Away : Home;
}

class ScorigamiData
{
    public int pts_win { get; set; }
    public int pts_lose { get; set; }
    public int count { get; set; }
    public DateTime first_date { get; set; }
    public string first_team_win { get; set; } = "";
    public string first_team_lose { get; set; } = "";
    public string first_team_home { get; set; } = "";
    public string first_team_away { get; set; } = "";
    public string first_link { get; set; } = "";
    public DateTime last_date { get; set; }
    public string last_team_win { get; set; } = "";
    public string last_team_lose { get; set; } = "";
    public string last_team_home { get; set; } = "";
    public string last_team_away { get; set; } = "";
    public string last_link { get; set; } = "";
}

static class LeagueInfo
{
    public const int START_YEAR = 1997;
}
