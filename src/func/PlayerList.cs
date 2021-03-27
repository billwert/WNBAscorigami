using System.Collections.Generic;
using Newtonsoft.Json;

namespace WNBAScorigami
{
    public class PlayerRoot
    {
        [JsonProperty("pls")]
        public PlayerList PlayerList { get; set; }
    }
    public class PlayerList
    {
        [JsonProperty("pl")]
        public List<Player> Players { get; set; }
    }
    public class Player
    {
        [JsonProperty("fn")]
        public string FirstName { get; set; }
        [JsonProperty("ln")]
        public string LastName { get; set; }
    }
}

