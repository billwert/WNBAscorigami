using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace WNBAScorigami
{
    public static class Scrape
    {
        [FunctionName("Scrape")]
        public static async Task Run([TimerTrigger("0 0 10 * * *")]TimerInfo myTimer, ILogger log)
        {
            await Scraper.Run(log);
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
