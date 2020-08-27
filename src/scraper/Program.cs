using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace WNBA_Scorigami
{
    class Program
    {
        private static Scorigami[,] Scorigamis = new Scorigami[150,150];
        private static List<WnbaGame> allGames = new List<WnbaGame>(5000);
        private static Dictionary<string, int> scorigamiByTeam = new Dictionary<string, int>();
        private static HashSet<int> updatedYears = new HashSet<int>();

        static void Main(string[] args)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var timings = new Dictionary<string, double>();
            void reset(string name)
            {
                timings.Add(name, sw.Elapsed.TotalSeconds);
                sw.Restart();
            }
            LoadGameData();
            reset("LoadGameData duration");
            CalculateScorigamis();
            reset("CalculateScorigamis duration");
            SaveGameData();
            reset("SaveGameData duration");
            TabulateTeamScorigamiCount(@"output_teamscorigamicount.txt");
            reset("TabulateTeamScorigamiCount duration");
            CalculateScorigamiByActivePlayer();
            reset("CalculateScorigamiByActivePlayer duration");
            foreach(var kvp in timings)
            {
                Console.WriteLine($"{kvp.Key} duration: {kvp.Value}");
            }
        }


        private static void CalculateScorigamiByActivePlayer()
        {
            Dictionary<string, int> scorigamiPerPlayer = new Dictionary<string, int>();
            foreach (var player in LeagueInfo.GetActivePlayerList())
            {
                scorigamiPerPlayer.Add(player, 0);
            }
            
            for(int i = 0; i < 150; i++)
            {
                for (int j = 0; j < 150; j++)
                {
                    var game = Scorigamis[i, j]?.First;
                    if (game == null)
                        continue;

                     
                    foreach (var player in game.Players)
                    {
                        if (scorigamiPerPlayer.ContainsKey(player))
                        {

                            scorigamiPerPlayer[player]++;
                        }
                    }
                }
            }

            using (var sw = new StreamWriter(Path.Join(LeagueInfo.DATA_DIRECTORY, @"output_playerScorigamis.txt")))
            {
                foreach (var kvp in scorigamiPerPlayer)
                {
                    sw.WriteLine("{0},{1}", kvp.Key, kvp.Value);
                }
            }

        }
        private static void SaveGameData()
        {
            foreach(var year in updatedYears)
            {
                SerializeYear(allGames.Where(game => game.Year == year).ToList());
            }
        }

        private static void SerializeYear(List<WnbaGame> games)
        {
            string json = JsonConvert.SerializeObject(games);
            File.WriteAllText(GameFilePath(games.First().Year), json);
        }

        private static string GameFilePath(int year) => Path.Join(LeagueInfo.DATA_DIRECTORY, year + "_games.json");
        private static void LoadGameData()
        {
            var scheduleURLFormat = @"https://www.basketball-reference.com/wnba/years/{0}-schedule.html";

            // Add all years of games. We look for a json blob first then pull from bbref.
            // For the current year we always pull from bbref as there may be new games to add.\
            if(!File.Exists(LeagueInfo.DATA_DIRECTORY))
            {
                Directory.CreateDirectory(LeagueInfo.DATA_DIRECTORY);
            }
            for (int i = LeagueInfo.START_YEAR; i <= DateTime.Now.Year; i++)
            {
                string gameFilePath = GameFilePath(i);

                if (File.Exists(gameFilePath) && i != DateTime.Now.Year)
                {
                    var games = JsonConvert.DeserializeObject<List<WnbaGame>>(File.ReadAllText(gameFilePath));
                    allGames.AddRange(games);
                    continue;
                }
                else
                {
                    string scheduleURL = String.Format(scheduleURLFormat, i.ToString());
                    List<WnbaGame> seasonOfGames = new List<WnbaGame>();
                    AddGamesFromBBRef(seasonOfGames, scheduleURL);
                    SerializeYear(seasonOfGames);
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
            var nodes = doc.DocumentNode.SelectNodes("//*[starts-with(@id,\"box-score\")]/tbody/tr//a");

            // https://www.basketball-reference.com/wnba/boxscores/201808030WAS.html this game is fucked for some reason 
            if (nodes == null)
            {
                Console.WriteLine("WARNING, no players found for game");
                return playerNames;
            }


            foreach (var node in nodes)
            {
                playerNames.Add(node.InnerText);
            }

            return playerNames;
        }


        private static void UpdateGame(WnbaGame game, ScorigamiType type)
        {
            if(game.ScorigamiType == ScorigamiType.None)
            {
                game.ScorigamiType = type;
                game.Players = GetPlayersForGame(game.BoxScoreURL);
                updatedYears.Add(game.Year);
            }
        }
        private static void CalculateScorigamis()
        {
            foreach (var game in allGames)
            {
                int higherScore = game.WinScore;
                int lowerScore = game.LoseScore;
                if(Scorigamis[higherScore, lowerScore] == null)
                {
                    Scorigamis[higherScore, lowerScore] = new Scorigami();
                }
                Scorigamis[higherScore, lowerScore].Count++;

                if (Scorigamis[higherScore, lowerScore].First == null || Scorigamis[higherScore, lowerScore].First.GameDate >= game.GameDate)
                {
                    Console.WriteLine("First Game updated for {0},{1}", higherScore, lowerScore);
                    Console.WriteLine("Was {0}", FormatGame(Scorigamis[higherScore, lowerScore].First));
                    Console.WriteLine("Now {0}", FormatGame(game));
                    Scorigamis[higherScore, lowerScore].First = game;
                    UpdateGame(game, ScorigamiType.First);

                }
                else if(Scorigamis[higherScore, lowerScore].Latest == null || Scorigamis[higherScore, lowerScore].Latest.GameDate >= game.GameDate)
                {
                    Console.WriteLine("Latest Game updated for {0},{1}", higherScore, lowerScore);
                    Console.WriteLine("Was {0}", FormatGame(Scorigamis[higherScore, lowerScore].Latest));
                        Console.WriteLine("Now {0}", FormatGame(game));
                    Scorigamis[higherScore, lowerScore].Latest = game;
                    UpdateGame(game, ScorigamiType.Latest);
                    }
                    else
                    {
                        Console.WriteLine("Game is not a Scorigami: {0}", FormatGame(game));
                    Console.WriteLine("Does not replace:{0}\t{1}{2}\t{3}",Environment.NewLine, 
                        FormatGame(Scorigamis[higherScore, lowerScore].First), 
                        Environment.NewLine,
                        FormatGame(Scorigamis[higherScore, lowerScore].Latest));
                    }
                }
            }

        private static void TabulateTeamScorigamiCount(string path)
        {
            using var list = new StreamWriter("output_list.txt");
            for (int i = 0; i < 150; i++)
            {
                for (int j = 0; j < 150; j++)
                {
                    WnbaGame scoriGame = Scorigamis[i, j]?.First;
                    if (scoriGame == null)
                        continue;
                    list.WriteLine(FormatGame(scoriGame));

                    // Some teams have moved and been renamed, their scorigamis stay with the franchise
                    string modernTeamName1 = LeagueInfo.GetModernTeamName(scoriGame.Away);
                    string modernTeamName2 = LeagueInfo.GetModernTeamName(scoriGame.Home);

                    if (scorigamiByTeam.ContainsKey(modernTeamName1))
                    {
                        scorigamiByTeam[modernTeamName1]++;
                    }
                    else
                    {
                        scorigamiByTeam.Add(modernTeamName1, 1);
                    }

                    if (scorigamiByTeam.ContainsKey(modernTeamName2))
                    {
                        scorigamiByTeam[modernTeamName2]++;
                    }
                    else
                    {
                        scorigamiByTeam.Add(modernTeamName2, 1);
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(path))
            {
                foreach (var team in scorigamiByTeam)
                {
                    sw.WriteLine("{0},{1}", team.Key, team.Value);
                }
            }
        }

        private static string FormatGame(WnbaGame game)
        {
            if(game == null)
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
        public ScorigamiType ScorigamiType {get; set; }
        public int Year { get => GameDate.Year; }
        public DateTime GameDate { get; set; } = DateTime.MinValue;
        public List<string> Players {get; set; }
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
        public string LoseTeam {get { return HomeWon ? Away : Home; } }
    }

    class WnbaTeam
    {
        public string TeamName { get; }
        public string TeamShortName { get; }
        public bool IsActive {get; }
        public string AltShortNames { get; }

        public WnbaTeam(string name, string shortname, bool active, string altShort = "")
        {
            TeamName = name;
            TeamShortName = shortname;
            IsActive = active;
            AltShortNames = altShort;
        }
    }

    static class LeagueInfo
    {
        public static readonly int START_YEAR = 1997;
        private static List<string> activePlayers = null;
        public  const string DATA_DIRECTORY = @"datacache";

        public static List<WnbaTeam> Teams {get; } = new List<WnbaTeam>()
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
        public static List<string> GetActivePlayerList()
        {
            if (activePlayers != null)
                return activePlayers;

            activePlayers = new List<string>();
            foreach (var line in File.ReadAllLines(Path.Join(DATA_DIRECTORY, @"activePlayerList.txt")))
            {
                activePlayers.Add(line);
            }

            return activePlayers;
        }
        public static string GetShortName(string teamName)
        {
            return GetShortName(teamName, DateTime.Now);
        }

        public static string GetModernTeamName(string name)
        {
            foreach(var namePair in teamRenameMap)
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
