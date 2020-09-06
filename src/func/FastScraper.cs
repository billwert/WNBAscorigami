using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace WNBAScorigami
{
    public static class FastScraper
    {
        [FunctionName("FastScraper")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            // https://data.wnba.com/data/5s/v2015/json/mobile_teams/wnba/2020/scores/10_todays_scores.json
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        /*
        game in progress:
        gid   : 1022001115
        gcode : 20200906/DALWAS
        p     : 2
        st    : 2
        stt   : 2nd Qtr
        cl    : 07:29
        seq   : 0
        lm    : @{gdte=2020-08-21; gres=DAL won 101-92; seri=DAL leads series 1-0; gid=1022001074}
        v     : @{ta=DAL; q1=19; s=26; q2=7; q3=0; q4=0; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Wings; tc=Dallas; tid=1611661321}
        h     : @{ta=WAS; q1=17; s=25; q2=8; q3=0; q4=0; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Mystics; tc=Washington; tid=1611661322}

        */

        /*
        box score json: https://data.wnba.com/data/5s/v2015/json/mobile_teams/wnba/2020/scores/gamedetail/$GID_gamedetail.json
        

        */
    }
}
