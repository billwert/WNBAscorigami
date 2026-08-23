using Newtonsoft.Json;

namespace WNBAScorigami;

static class Config
{
    public static bool Verbose { get; set; }
}

class Program
{
    static async Task Main(string[] args)
    {
        string dataDir = "../data";
        bool whatIf = false;
        bool force = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--data-dir" && i + 1 < args.Length)
            {
                dataDir = args[++i];
            }
            else if (args[i] == "--what-if")
            {
                whatIf = true;
            }
            else if (args[i] == "--verbose")
            {
                Config.Verbose = true;
            }
            else if (args[i] == "--force")
            {
                force = true;
            }
        }

        LoadDotEnv();

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] data-dir={dataDir}  what-if={whatIf}  force={force}");
        }

        var oldData = LoadOldScorigamiData(dataDir);
        var oldPairs = oldData.Select(g => (g.pts_win, g.pts_lose)).ToHashSet();

        List<ScorigamiData> newData;
        bool currentYearFromCache;
        try
        {
            (newData, currentYearFromCache) = Scraper.Run(dataDir);
        }
        catch (ScrapeFailedException ex)
        {
            Console.Error.WriteLine($"Scrape failed: {ex.Message}");
            Environment.ExitCode = 1;
            return;
        }

        if (currentYearFromCache)
        {
            // Cached current season means this is last run's data. Rewriting it would only
            // move lastUpdated and imply a refresh that did not happen.
            Console.WriteLine("Current season served from cache — leaving scorigamidata.json untouched");
            return;
        }

        if (!IsSupersetOfPrevious(oldData, newData))
        {
            if (!force)
            {
                Console.Error.WriteLine("Refusing to write scorigamidata.json or post. Re-run with --force to override.");
                Environment.ExitCode = 1;
                return;
            }
            Console.Error.WriteLine("--force specified — continuing anyway");
        }

        Scraper.WriteScorigamiData(dataDir, newData);
        await ScorigamiNotifier.PostNewScorigamis(newData, oldPairs, whatIf);
    }

    /// <summary>
    /// A correct run can only add to the dataset. Losing a known score pair or a pair's
    /// games means the scrape came back incomplete — publishing it would also make the
    /// next run re-announce the difference.
    /// </summary>
    private static bool IsSupersetOfPrevious(List<ScorigamiData> oldData, List<ScorigamiData> newData)
    {
        var newByPair = newData.ToDictionary(g => (g.pts_win, g.pts_lose));

        var missing = oldData.Where(o => !newByPair.ContainsKey((o.pts_win, o.pts_lose))).ToList();
        var shrunk = oldData.Where(o => newByPair.TryGetValue((o.pts_win, o.pts_lose), out var n) && n.count < o.count).ToList();
        int oldTotal = oldData.Sum(g => g.count);
        int newTotal = newData.Sum(g => g.count);

        if (missing.Count == 0 && shrunk.Count == 0 && newTotal >= oldTotal)
        {
            Console.WriteLine($"Sanity check passed: {oldData.Count} -> {newData.Count} pairs, {oldTotal} -> {newTotal} games");
            return true;
        }

        Console.Error.WriteLine($"SANITY CHECK FAILED: {oldData.Count} -> {newData.Count} pairs, {oldTotal} -> {newTotal} games");
        foreach (var m in missing.Take(10))
            Console.Error.WriteLine($"  gone: {m.pts_win}-{m.pts_lose} (first seen {m.first_date:yyyy-MM-dd})");
        if (missing.Count > 10)
            Console.Error.WriteLine($"  ... and {missing.Count - 10} more gone");
        foreach (var sh in shrunk.Take(10))
            Console.Error.WriteLine($"  shrank: {sh.pts_win}-{sh.pts_lose}: {sh.count} -> {newByPair[(sh.pts_win, sh.pts_lose)].count}");
        return false;
    }

    private static List<ScorigamiData> LoadOldScorigamiData(string dataDir)
    {
        var path = Path.Join(dataDir, "scorigamidata.json");
        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Loading old scorigami pairs from {Path.GetFullPath(path)}");
        }
        if (!File.Exists(path))
        {
            if (Config.Verbose)
            {
                Console.WriteLine("[VERBOSE] File not found — treating as empty (first run)");
            }
            return new List<ScorigamiData>();
        }
        try
        {
            var root = JsonConvert.DeserializeObject<ScorigamiRoot>(File.ReadAllText(path));
            var games = root?.games ?? new();
            if (Config.Verbose)
            {
                Console.WriteLine($"[VERBOSE] Loaded {games.Count} existing scorigami pairs");
            }
            return games;
        }
        catch (Exception ex)
        {
            if (Config.Verbose)
            {
                Console.WriteLine($"[VERBOSE] Failed to parse old data: {ex.Message} — treating as empty");
            }
            return new List<ScorigamiData>();
        }
    }

    private static void LoadDotEnv()
    {
        // Walk up from CWD so `dotnet run` from src/gh-scraper/ finds .env at repo root
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Config.Verbose)
            {
                Console.WriteLine($"[VERBOSE] Looking for .env in {dir}");
            }
            var candidate = Path.Join(dir, ".env");
            if (File.Exists(candidate))
            {
                if (Config.Verbose)
                {
                    Console.WriteLine($"[VERBOSE] Found .env at {candidate}");
                }
                foreach (var line in File.ReadAllLines(candidate))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }
                    var idx = trimmed.IndexOf('=');
                    if (idx < 0)
                    {
                        continue;
                    }
                    var key = trimmed[..idx].Trim();
                    var val = trimmed[(idx + 1)..].Trim();
                    Environment.SetEnvironmentVariable(key, val);
                    if (Config.Verbose)
                    {
                        Console.WriteLine($"[VERBOSE] Set {key}={Mask(key, val)}");
                    }
                }
                return;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        if (Config.Verbose)
        {
            Console.WriteLine("[VERBOSE] No .env file found anywhere up the directory tree");
        }
    }

    private static string Mask(string key, string val)
    {
        if (key.Contains("PASSWORD") || key.Contains("SECRET") || key.Contains("TOKEN"))
        {
            return val.Length <= 4 ? new string('*', val.Length) : $"{val[..2]}***{val[^2..]} (len={val.Length})";
        }
        return val;
    }
}

class ScorigamiRoot
{
    public List<ScorigamiData> games { get; set; } = new();
}
