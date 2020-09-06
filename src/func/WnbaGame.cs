using System;
using System.Collections.Generic;

namespace WNBAScorigami
{
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
}
