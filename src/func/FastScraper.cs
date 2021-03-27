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

        complete games:
        gid   : 1022001115
        gcode : 20200906/DALWAS
        p     : 5
        st    : 3
        stt   : Final
        cl    : 00:00.0
        seq   : 0
        lm    : @{gdte=2020-08-21; gres=DAL won 101-92; seri=DAL leads series 1-0; gid=1022001074}
        v     : @{ta=DAL; q1=19; s=101; q2=19; q3=21; q4=27; ot1=15; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Wings; tc=Dallas; tid=1611661321}
        h     : @{ta=WAS; q1=17; s=94; q2=27; q3=16; q4=26; ot1=8; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Mystics; tc=Washington; tid=1611661322}

        gid   : 1022001116
        gcode : 20200906/SEAMIN
        p     : 4
        st    : 3
        stt   : Final
        cl    : 00:00.0
        seq   : 0
        lm    : @{gdte=2020-07-28; gres=SEA won 90-66; seri=SEA leads series 1-0; gid=1022001009}
        v     : @{ta=SEA; q1=26; s=103; q2=19; q3=31; q4=27; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Storm; tc=Seattle; tid=1611661328}
        h     : @{ta=MIN; q1=21; s=88; q2=17; q3=20; q4=30; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Lynx; tc=Minnesota; tid=1611661324}

        gid   : 1022001117
        gcode : 20200906/CHILAS
        p     : 4
        st    : 3
        stt   : Final
        cl    : 00:00.0
        seq   : 0
        lm    : @{gdte=2020-07-28; gres=CHI won 96-78; seri=CHI leads series 1-0; gid=1022001008}
        v     : @{ta=CHI; q1=30; s=80; q2=18; q3=22; q4=10; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Sky; tc=Chicago; tid=1611661329}
        h     : @{ta=LAS; q1=22; s=86; q2=25; q3=24; q4=15; ot1=0; ot2=0; ot3=0; ot4=0; ot5=0; ot6=0; ot7=0; ot8=0; ot9=0; ot10=0; tn=Sparks; tc=Los Angeles; tid=1611661320}

        */
    }
}
