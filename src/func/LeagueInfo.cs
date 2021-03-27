using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WNBAScorigami
{
    static class LeagueInfo
    {
        public static readonly int START_YEAR = 1997;
        private static List<Player> activePlayers = null;
        public const string DATA_DIRECTORY = @"datacache";

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
        public static async Task<List<Player>> GetActivePlayerList()
        {
            if (activePlayers != null)
                return activePlayers;

            WebClient wc = new WebClient();
            var json = await wc.DownloadStringTaskAsync("https://data.wnba.com/data/5s/v2015/json/mobile_teams/wnba/2020/players/10_player_info.json");
            var playerHash = new HashSet<Player>();
            foreach(var player in JsonConvert.DeserializeObject<PlayerRoot>(json).PlayerList.Players)
            {
                playerHash.Add(player);
            }
            activePlayers = new List<Player>(playerHash);
            return activePlayers;
        }
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
