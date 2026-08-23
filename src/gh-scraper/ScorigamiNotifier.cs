namespace WNBAScorigami;

static class ScorigamiNotifier
{
    public static async Task PostNewScorigamis(
        List<ScorigamiData> newData,
        HashSet<(int, int)> oldPairs,
        bool whatIf)
    {
        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Diffing: {newData.Count} new records vs {oldPairs.Count} old pairs");
        }

        var newScorigamis = newData
            .Where(s => !oldPairs.Contains((s.pts_win, s.pts_lose)))
            .OrderBy(s => s.pts_win).ThenBy(s => s.pts_lose)
            .ToList();

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] New scorigamis detected: {newScorigamis.Count}");
            foreach (var s in newScorigamis)
                Console.WriteLine($"[VERBOSE]   {s.pts_win}-{s.pts_lose}  {s.first_team_win} vs {s.first_team_lose}  first_date={s.first_date:yyyy-MM-dd}");
        }

        if (newScorigamis.Count == 0)
        {
            Console.WriteLine("No new scorigamis this run");
            return;
        }

        Console.WriteLine($"Found {newScorigamis.Count} new scorigami(s)");

        var poster = BlueSkyPoster.TryCreate();

        int totalCount = newData.Count;
        int newCount = newScorigamis.Count;

        // Oldest number in the batch, checked first so one feed walk serves them all.
        if (poster != null)
        {
            await poster.PrimeFeedAsync(totalCount - newCount);
            if (poster.FeedReadFailed && !whatIf)
            {
                Console.Error.WriteLine(
                    "Could not read the Bluesky timeline to check for duplicates — posting nothing this run");
                return;
            }
        }

        for (int i = 0; i < newScorigamis.Count; i++)
        {
            var s = newScorigamis[i];
            int rank = totalCount - newCount + i + 1;
            string text = FormatPost(s, rank);

            if (poster != null && !poster.FeedReadFailed &&
                await poster.AlreadyPostedAsync(rank - 1, s.pts_win, s.pts_lose))
                continue;

            if (whatIf)
            {
                Console.WriteLine("--- WHAT-IF POST ---");
                Console.WriteLine(text);
                Console.WriteLine("--------------------");
            }
            else
            {
                if (Config.Verbose)
                {
                    Console.WriteLine($"[VERBOSE] Post text ({text.Length} chars):");
                    Console.WriteLine(text);
                    Console.WriteLine("---");
                }
                if (poster != null)
                    await poster.PostAsync(text);
            }
        }
    }

    private static string FormatPost(ScorigamiData s, int rank) =>
        $"SCORIGAMI!!!\n\nWith a score of {s.pts_win} - {s.pts_lose} the {s.first_team_win} and {s.first_team_lose} have completed the {Ordinal(rank-1)} scorigami in league history.\n\n#WNBA";

    private static string Ordinal(int n) =>
        (n % 100 is 11 or 12 or 13)
            ? $"{n}th"
            : (n % 10) switch
            {
                1 => $"{n}st",
                2 => $"{n}nd",
                3 => $"{n}rd",
                _ => $"{n}th"
            };
}
