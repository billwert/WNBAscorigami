using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WNBAScorigami
{
    class Scraper
    {
        private Scorigami[,] Scorigamis = new Scorigami[150, 150];
        private List<WnbaGame> allGames = new List<WnbaGame>(5000);
        private Dictionary<string, int> scorigamiByTeam = new Dictionary<string, int>();
        private HashSet<int> updatedYears = new HashSet<int>();
        private Storage storage; 

        public static async Task Run(ILogger log)
        {
            var scraper = new Scraper();
            scraper.storage = new Storage("WNBAStorage", Environment.GetEnvironmentVariable("DEBUGCONTAINER") ?? "leaguedata");
            Stopwatch sw = Stopwatch.StartNew();
            var timings = new Dictionary<string, double>();
            void reset(string name)
            {
                timings.Add(name, sw.Elapsed.TotalSeconds);
                sw.Restart();
            };
            await scraper.LoadGameData();
            reset("LoadGameData duration");
            scraper.CalculateScorigamis();
            reset("CalculateScorigamis duration");
            await scraper.SaveGameData();
            reset("SaveGameData duration");
            await scraper.WriteScorigamiData();
            // TODO: billwert: enable this functionality
            // Scraper.TabulateTeamScorigamiCount(@"output_teamscorigamicount.txt");
            // reset("TabulateTeamScorigamiCount duration");
            // Scraper.CalculateScorigamiByActivePlayer();
            // reset("CalculateScorigamiByActivePlayer duration");
            foreach (var kvp in timings)
            {
                log.LogInformation($"{kvp.Key} duration: {kvp.Value}");
            }
        }

        private async Task WriteScorigamiData()
        {
            var data = new List<ScorigamiData>();
            for(int i = 0; i < 150; i++)
            {
                for(int j = 0; j < 150; j++)
                {
                    var scorigami = Scorigamis[i,j];
                    if(scorigami == null)
                        continue;
                    var first = scorigami.First;
                    var last = scorigami.Latest ?? scorigami.First;
                    data.Add(new ScorigamiData{
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
            var upload = new { games = data, lastUpdated = DateTime.Now };
            var json = JsonConvert.SerializeObject(upload);
            await storage.UploadJson(json, "scorigamidata.json");
        }

        // TODO: billwert: need this to show up in a json somewhere.
        // public static void CalculateScorigamiByActivePlayer()
        // {
        //     Dictionary<string, int> scorigamiPerPlayer = new Dictionary<string, int>();
        //     foreach (var player in LeagueInfo.GetActivePlayerList())
        //     {
        //         scorigamiPerPlayer.Add(player, 0);
        //     }

        //     for(int i = 0; i < 150; i++)
        //     {
        //         for (int j = 0; j < 150; j++)
        //         {
        //             var game = Scorigamis[i, j]?.First;
        //             if (game == null)
        //                 continue;


        //             foreach (var player in game.Players)
        //             {
        //                 if (scorigamiPerPlayer.ContainsKey(player))
        //                 {

        //                     scorigamiPerPlayer[player]++;
        //                 }
        //             }
        //         }
        //     }

        //     using (var sw = new StreamWriter(Path.Join(LeagueInfo.DATA_DIRECTORY, @"output_playerScorigamis.txt")))
        //     {
        //         foreach (var kvp in scorigamiPerPlayer)
        //         {
        //             sw.WriteLine("{0},{1}", kvp.Key, kvp.Value);
        //         }
        //     }

        // }
        public async Task SaveGameData()
        {
            foreach (var year in updatedYears)
            {
                await SaveYear(allGames.Where(game => game.Year == year).ToList());
            }
        }

        private async Task SaveYear(List<WnbaGame> games)
        {
            string json = JsonConvert.SerializeObject(games);
            await storage.UploadJson(json, GameFileName(games.First().Year));
        }

        private async Task<List<WnbaGame>> LoadYear(int year)
        {
            var json = await storage.DownloadJson(GameFileName(year));
            return JsonConvert.DeserializeObject<List<WnbaGame>>(json);
        }

        private static string GameFileName(int year) => $"{year}_games.json";
        public async Task LoadGameData()
        {
            var scheduleURLFormat = @"https://www.basketball-reference.com/wnba/years/{0}_games.html";

            // Add all years of games. We look for a json blob first then pull from bbref.
            // For the current year we always pull from bbref as there may be new games to add.
            for (int i = LeagueInfo.START_YEAR; i <= DateTime.Now.Year; i++)
            {
                if (i != DateTime.Now.Year)
                {
                    var games = await LoadYear(i);
                    allGames.AddRange(games);
                    continue;
                }
                else
                {
                    string scheduleURL = String.Format(scheduleURLFormat, i.ToString());
                    List<WnbaGame> seasonOfGames = new List<WnbaGame>();
                    AddGamesFromBBRef(seasonOfGames, scheduleURL);
                    await SaveYear(seasonOfGames);
                    allGames.AddRange(seasonOfGames);
                }
            }
        }

        private static List<string> GetPlayersForGame(string boxScoreUrl)
        {
            var web = new HtmlWeb();
            var doc = web.Load(boxScoreUrl);
            var playerNames = new List<string>();

            // "//*[@id=\"box-score-nyl\"]/tbody/tr[1]/th/a"
            var nodes = doc.DocumentNode.SelectNodes("//*[contains(@id,\"game-basic\")]/tbody/tr//a");

            // https://www.basketball-reference.com/wnba/boxscores/201808030WAS.html this game is fucked for some reason 
            if (nodes == null)
            {
                return playerNames;
            }

            foreach (var node in nodes)
            {
                playerNames.Add(node.InnerText);
            }

            return playerNames;
        }

        private void UpdateGame(WnbaGame game, ScorigamiType type)
        {
            if (game.ScorigamiType == ScorigamiType.None)
            {
                game.ScorigamiType = type;
                game.Players = GetPlayersForGame(game.BoxScoreURL);
                updatedYears.Add(game.Year);
            }
        }

        public void CalculateScorigamis()
        {
            foreach (var game in allGames)
            {
                int higherScore = game.WinScore;
                int lowerScore = game.LoseScore;
                if (Scorigamis[higherScore, lowerScore] == null)
                {
                    Scorigamis[higherScore, lowerScore] = new Scorigami();
                }
                Scorigamis[higherScore, lowerScore].Count++;

                if (Scorigamis[higherScore, lowerScore].First == null || Scorigamis[higherScore, lowerScore].First.GameDate >= game.GameDate)
                {
                    Scorigamis[higherScore, lowerScore].First = game;
                    UpdateGame(game, ScorigamiType.First);

                }
                else if (Scorigamis[higherScore, lowerScore].Latest == null || Scorigamis[higherScore, lowerScore].Latest.GameDate >= game.GameDate)
                {
                    Scorigamis[higherScore, lowerScore].Latest = game;
                    UpdateGame(game, ScorigamiType.Latest);
                }
            }
        }

        // TODO: billwert: need this to show up in a json somewhere.
        // public static void TabulateTeamScorigamiCount(string path)
        // {
        //     using var list = new StreamWriter("output_list.txt");
        //     for (int i = 0; i < 150; i++)
        //     {
        //         for (int j = 0; j < 150; j++)
        //         {
        //             WnbaGame scoriGame = Scorigamis[i, j]?.First;
        //             if (scoriGame == null)
        //                 continue;
        //             list.WriteLine(FormatGame(scoriGame));

        //             // Some teams have moved and been renamed, their scorigamis stay with the franchise
        //             string modernTeamName1 = LeagueInfo.GetModernTeamName(scoriGame.Away);
        //             string modernTeamName2 = LeagueInfo.GetModernTeamName(scoriGame.Home);

        //             if (scorigamiByTeam.ContainsKey(modernTeamName1))
        //             {
        //                 scorigamiByTeam[modernTeamName1]++;
        //             }
        //             else
        //             {
        //                 scorigamiByTeam.Add(modernTeamName1, 1);
        //             }

        //             if (scorigamiByTeam.ContainsKey(modernTeamName2))
        //             {
        //                 scorigamiByTeam[modernTeamName2]++;
        //             }
        //             else
        //             {
        //                 scorigamiByTeam.Add(modernTeamName2, 1);
        //             }
        //         }
        //     }

        //     using (StreamWriter sw = new StreamWriter(path))
        //     {
        //         foreach (var team in scorigamiByTeam)
        //         {
        //             sw.WriteLine("{0},{1}", team.Key, team.Value);
        //         }
        //     }
        // }

        private static string FormatGame(WnbaGame game)
        {
            if (game == null)
            {
                return "<empty>";
            }
            string gameStr = game.Away + " v " + game.Home;
            string output = String.Format("{0},{1},{2},{3}", game.GameDate.ToString("yyyy-MM-dd"), gameStr, game.AwayScore, game.HomeScore);
            return output;
        }

        private static void AddGamesFromBBRef(List<WnbaGame> allGames, string seasonUrl)
        {
            var web2 = new HtmlWeb();
            var doc2 = web2.Load(seasonUrl);
            var gameRows = doc2.DocumentNode.SelectNodes("//*[@id=\"schedule\"]/tbody/tr");
            bool playoffGame = false;

            foreach (var gameRow in gameRows)
            {
                if (gameRow.ChildNodes[0].InnerText == "Playoffs")
                {
                    playoffGame = true;
                    continue;
                }

                // an empty score means we're in the current year and at games that have not been played. stop processing.
                if (gameRow.ChildNodes[2].InnerText == "")
                {
                    break;
                }

                WnbaGame game = new WnbaGame();
                game.GameDate = DateTime.Parse(gameRow.ChildNodes[0].InnerText);
                game.Away = gameRow.ChildNodes[1].InnerText;
                game.AwayScore = Int32.Parse(gameRow.ChildNodes[2].InnerText);
                game.Home = gameRow.ChildNodes[3].InnerText;
                game.HomeScore = Int32.Parse(gameRow.ChildNodes[4].InnerText);
                game.BoxScoreURL = @"https://www.basketball-reference.com" + gameRow.ChildNodes[5].FirstChild.Attributes[0].Value;
                game.IsPlayoffGame = playoffGame;

                allGames.Add(game);
            }
        }
    }

    class Scorigami
    {
        public WnbaGame First { get; set; }
        public WnbaGame Latest { get; set; }
        public int Count { get; set; }
    }

    enum ScorigamiType
    {
        None,
        First,
        Latest
    }

    class WnbaGame
    {
        public ScorigamiType ScorigamiType { get; set; }
        public int Year { get => GameDate.Year; }
        public DateTime GameDate { get; set; } = DateTime.MinValue;
        public List<string> Players { get; set; }
        public string Away { get; set; }
        public string Home { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public string BoxScoreURL { get; set; }
        public bool IsPlayoffGame { get; set; }
        public bool HomeWon { get { return HomeScore > AwayScore; } }
        public int WinScore { get { return HomeWon ? HomeScore : AwayScore; } }
        public string WinTeam { get { return HomeWon ? Home : Away; } }
        public int LoseScore { get { return HomeWon ? AwayScore : HomeScore; } }
        public string LoseTeam { get { return HomeWon ? Away : Home; } }
    }

    class WnbaTeam
    {
        public string TeamName { get; }
        public string TeamShortName { get; }
        public bool IsActive { get; }
        public string AltShortNames { get; }

        public WnbaTeam(string name, string shortname, bool active, string altShort = "")
        {
            TeamName = name;
            TeamShortName = shortname;
            IsActive = active;
            AltShortNames = altShort;
        }
    }

    class ScorigamiData
    {
        public int pts_win { get; set; }
        public int pts_lose { get; set; }
        public int count { get; set; }
        public DateTime first_date { get; set; }
        public string first_team_win { get; set; }
        public string first_team_lose { get; set; }
        public string first_team_home { get; set; }
        public string first_team_away { get; set; }
        public string first_link { get; set; }
        public DateTime last_date { get; set; }
        public string last_team_win { get; set; }
        public string last_team_lose { get; set; }
        public string last_team_home { get; set; }
        public string last_team_away { get; set; }
        public string last_link { get; set; }
    }

    static class LeagueInfo
    {
        public static readonly int START_YEAR = 1997;
        private static List<string> activePlayers = null;

        public static List<WnbaTeam> Teams { get; } = new List<WnbaTeam>()
        {
            new WnbaTeam("Atlanta Dream", "ATL",true),
            new WnbaTeam("Chicago Sky","CHI",true),
            new WnbaTeam("Connecticut Sun","CON",true,"ORL"),
            new WnbaTeam("Indiana Fever","IND", true),
            new WnbaTeam("Los Angeles Sparks","LAS", true),
            new WnbaTeam("Minnesota Lynx","MIN", true),
            new WnbaTeam("New York Liberty","NYL",true),
            new WnbaTeam("Phoenix Mercury","PHO",true),
            new WnbaTeam("Las Vegas Aces","LVA",true,"SAS;UTA"),
            new WnbaTeam("Seattle Storm","SEA",true),
            new WnbaTeam("Dallas Wings","DAL",true, "TUL;DET"),
            new WnbaTeam("Washington Mystsics","WAS",true),
            new WnbaTeam("Charlotte Sting","CHA",false),
            new WnbaTeam("Cleveland Rockers","CLE",false),
            new WnbaTeam("Houston Comets","HOU",false),
            new WnbaTeam("Miami Sol","MIA",false),
            new WnbaTeam("Portland Fire","POR", false),
            new WnbaTeam("Sacremento Monarchs", "SAC",false)
        };

        private static Dictionary<string, string> teamRenameMap = new Dictionary<string, string>()
        {
            {"Detroit Shock", "Dallas Wings" },
            {"Tulsa Shock", "Dallas Wings" },
            {"Orlando Miracle", "Connecticut Sun" },
            {"San Antonio Silver Stars", "Las Vegas Aces" },
            {"Utah Starzz", "Las Vegas Aces" },

        };
        // public static List<string> GetActivePlayerList()
        // {
        //     if (activePlayers != null)
        //         return activePlayers;

        //     activePlayers = new List<string>();
        //     foreach (var line in File.ReadAllLines(Path.Join(DATA_DIRECTORY, @"activePlayerList.txt")))
        //     {
        //         activePlayers.Add(line);
        //     }

        //     return activePlayers;
        // }
        public static string GetShortName(string teamName)
        {
            return GetShortName(teamName, DateTime.Now);
        }

        public static string GetModernTeamName(string name)
        {
            foreach (var namePair in teamRenameMap)
            {
                if (namePair.Key == name)
                {
                    return namePair.Value;
                }
            }

            return name;
        }

        public static string GetShortName(string teamName, DateTime gameDate)
        {
            foreach (var team in Teams)
            {
                if (team.TeamName == teamName)
                {
                    if (String.IsNullOrEmpty(team.AltShortNames))
                        return team.TeamShortName;
                    else
                    {
                        throw new NotImplementedException("need to implement short name lookup for dead teams");
                    }
                }
            }
            throw new ArgumentException("Teamname {0} is not found in league list", teamName);
        }

    }
}
