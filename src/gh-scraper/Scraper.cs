using HtmlAgilityPack;
using Newtonsoft.Json;

namespace WNBAScorigami;

class Scraper
{
    private readonly Scorigami[,] Scorigamis = new Scorigami[150, 150];
    private readonly List<WnbaGame> allGames = new(5000);
    private string dataDir = "data";

    public static List<ScorigamiData> Run(string dataDir)
    {
        var scraper = new Scraper { dataDir = dataDir };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        scraper.LoadGameData();
        Console.WriteLine($"LoadGameData: {sw.Elapsed.TotalSeconds:F2}s");
        sw.Restart();

        scraper.CalculateScorigamis();
        Console.WriteLine($"CalculateScorigamis: {sw.Elapsed.TotalSeconds:F2}s");
        sw.Restart();

        var result = scraper.WriteScorigamiData();
        Console.WriteLine($"WriteScorigamiData: {sw.Elapsed.TotalSeconds:F2}s");
        return result;
    }

    private List<ScorigamiData> WriteScorigamiData()
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

        var output = new { games = data, lastUpdated = DateTime.UtcNow };
        var json = JsonConvert.SerializeObject(output, Formatting.Indented);
        Directory.CreateDirectory(dataDir);
        var path = Path.Join(dataDir, "scorigamidata.json");
        File.WriteAllText(path, json);
        Console.WriteLine($"Wrote {data.Count} scorigami records to {path}");
        return data;
    }

    private void SaveYear(List<WnbaGame> games)
    {
        var gamesDir = Path.Join(dataDir, "games");
        Directory.CreateDirectory(gamesDir);
        var path = Path.Join(gamesDir, GameFileName(games[0].Year));
        File.WriteAllText(path, JsonConvert.SerializeObject(games, Formatting.Indented));
        Console.WriteLine($"Saved {games.Count} games for {games[0].Year} to {path}");
    }

    private List<WnbaGame> LoadYear(int year)
    {
        var path = Path.Join(dataDir, "games", GameFileName(year));
        if (!File.Exists(path))
        {
            Console.WriteLine($"Cache missing for {year}, will scrape from BBRef");
            return ScrapeYear(year);
        }
        var games = JsonConvert.DeserializeObject<List<WnbaGame>>(File.ReadAllText(path));
        if (games == null)
        {
            Console.WriteLine($"Failed to deserialize {path}, will scrape from BBRef");
            return ScrapeYear(year);
        }
        return games;
    }

    private List<WnbaGame> ScrapeYear(int year)
    {
        Console.WriteLine($"Scraping BBRef for {year}...");
        var url = $"https://www.basketball-reference.com/wnba/years/{year}_games.html";
        var games = new List<WnbaGame>();
        AddGamesFromBBRef(games, url);
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
                games = ScrapeYear(i);
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

    private static void AddGamesFromBBRef(List<WnbaGame> games, string seasonUrl)
    {
        var web = new HtmlWeb();
        var doc = web.Load(seasonUrl);
        var gameRows = doc.DocumentNode.SelectNodes("//*[@id=\"schedule\"]/tbody/tr");
        if (gameRows == null)
            return;

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
    }
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
