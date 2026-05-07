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
        }

        LoadDotEnv();

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] data-dir={dataDir}  what-if={whatIf}");
        }

        var oldPairs = LoadOldScorigamiPairs(dataDir);
        var newData = Scraper.Run(dataDir);
        await ScorigamiNotifier.PostNewScorigamis(newData, oldPairs, whatIf);
    }

    private static HashSet<(int, int)> LoadOldScorigamiPairs(string dataDir)
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
            return new HashSet<(int, int)>();
        }
        try
        {
            var root = JsonConvert.DeserializeObject<ScorigamiRoot>(File.ReadAllText(path));
            var pairs = (root?.games ?? new()).Select(g => (g.pts_win, g.pts_lose)).ToHashSet();
            if (Config.Verbose)
            {
                Console.WriteLine($"[VERBOSE] Loaded {pairs.Count} existing scorigami pairs");
            }
            return pairs;
        }
        catch (Exception ex)
        {
            if (Config.Verbose)
            {
                Console.WriteLine($"[VERBOSE] Failed to parse old data: {ex.Message} — treating as empty");
            }
            return new HashSet<(int, int)>();
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
