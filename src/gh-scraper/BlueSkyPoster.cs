using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WNBAScorigami;

class BlueSkyPoster
{
    private static readonly HttpClient http = new();
    private const string BaseUrl = "https://bsky.social/xrpc/";
    private const string PublicApiUrl = "https://public.api.bsky.app/xrpc/";
    private const int FeedPageSize = 100;
    private const int MaxFeedPages = 20;

    private static readonly Regex ScoreRegex =
        new(@"With a score of\s+(\d+)\s*-\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex NumberRegex =
        new(@"completed the\s+(\d+)(?:st|nd|rd|th)\s+scorigami", RegexOptions.Compiled);

    private readonly string identifier;
    private readonly string appPassword;

    // Reverse-chronological scan of our own feed, built lazily and shared across checks.
    private readonly List<(int? Number, int? PtsWin, int? PtsLose)> scannedPosts = new();
    private string? feedCursor;
    private int feedPagesFetched;
    private bool feedExhausted;

    /// <summary>True if the feed could not be read, so duplicates cannot be ruled out.</summary>
    public bool FeedReadFailed { get; private set; }

    private BlueSkyPoster(string identifier, string appPassword)
    {
        this.identifier = identifier;
        this.appPassword = appPassword;
    }

    public static BlueSkyPoster? TryCreate()
    {
        var id = Environment.GetEnvironmentVariable("BLUESKY_IDENTIFIER");
        var pw = Environment.GetEnvironmentVariable("BLUESKY_APP_PASSWORD");

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] BLUESKY_IDENTIFIER={(string.IsNullOrEmpty(id) ? "<not set>" : id)}");
            Console.WriteLine($"[VERBOSE] BLUESKY_APP_PASSWORD={(string.IsNullOrEmpty(pw) ? "<not set>" : $"{pw[..2]}***{pw[^2..]} (len={pw.Length})")}");
        }

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            Console.WriteLine("BLUESKY_IDENTIFIER or BLUESKY_APP_PASSWORD not set — skipping Bluesky posts");
            return null;
        }
        return new BlueSkyPoster(id, pw);
    }

    public async Task<bool> PostAsync(string text)
    {
        Console.WriteLine("Authenticating with Bluesky...");
        var (token, did) = await CreateSessionAsync();
        if (token == null || did == null)
        {
            return false;
        }

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Authenticated as DID={did}");
        }

        var body = JsonConvert.SerializeObject(new
        {
            repo = did,
            collection = "app.bsky.feed.post",
            record = new
            {
                text,
                createdAt = DateTime.UtcNow.ToString("O"),
                langs = new[] { "en" }
            }
        }, Formatting.Indented);

        var url = BaseUrl + "com.atproto.repo.createRecord";
        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] POST {url}");
            Console.WriteLine($"[VERBOSE] Authorization: Bearer {token[..Math.Min(12, token.Length)]}... (len={token.Length})");
            Console.WriteLine($"[VERBOSE] Request body:\n{body}");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

        var response = await http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Response status: {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"[VERBOSE] Response body:\n{PrettyJson(responseBody)}");
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Bluesky post failed: {(int)response.StatusCode} {response.StatusCode}");
            if (!Config.Verbose)
            {
                Console.WriteLine($"Response body: {responseBody}");
            }
            return false;
        }
        Console.WriteLine("Posted to Bluesky successfully");
        return true;
    }

    /// <summary>
    /// Walks the feed back far enough to decide <paramref name="oldestNumber"/>, so a batch
    /// can learn up front whether <see cref="FeedReadFailed"/> is set.
    /// </summary>
    public Task PrimeFeedAsync(int oldestNumber) => ScanFeedUntilAsync(oldestNumber);

    /// <summary>
    /// True if this score pair is already on the timeline. Identity is the score pair, not
    /// the number: the number is derived from the dataset, so a run off a bad dataset
    /// renumbers everything and a number match would hit an unrelated post. The number is
    /// only good enough to tell the scan when to stop.
    /// </summary>
    public async Task<bool> AlreadyPostedAsync(int number, int ptsWin, int ptsLose)
    {
        await ScanFeedUntilAsync(number);

        if (scannedPosts.Any(p => p.PtsWin == ptsWin && p.PtsLose == ptsLose))
        {
            Console.WriteLine($"Already posted {ptsWin}-{ptsLose} — skipping");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pulls feed pages newest-first until we reach the target number, on the assumption that
    /// anything older was posted before it. That bound is only as good as the numbering, so
    /// the first page is always fetched — which covers any realistic renumbering drift.
    /// </summary>
    private async Task ScanFeedUntilAsync(int number)
    {
        while (!feedExhausted && feedPagesFetched < MaxFeedPages &&
               !scannedPosts.Any(p => p.Number is int n && n <= number))
        {
            if (!await FetchNextFeedPageAsync())
                return;
        }

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Feed scan for #{number}: {scannedPosts.Count} post(s) over " +
                              $"{feedPagesFetched} page(s), exhausted={feedExhausted}");
        }
    }

    private async Task<bool> FetchNextFeedPageAsync()
    {
        // The public AppView serves author feeds unauthenticated, so the duplicate check
        // still works when auth is broken — and a run with broken auth posts nothing anyway.
        var url = $"{PublicApiUrl}app.bsky.feed.getAuthorFeed" +
                  $"?actor={Uri.EscapeDataString(identifier)}&limit={FeedPageSize}&filter=posts_no_replies";
        if (feedCursor != null)
        {
            url += $"&cursor={Uri.EscapeDataString(feedCursor)}";
        }

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] GET {url}");
        }

        JObject json;
        try
        {
            var response = await http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Bluesky feed read failed: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"Response body: {body}");
                FeedReadFailed = true;
                return false;
            }
            json = JObject.Parse(body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bluesky feed read failed: {ex.GetType().Name}: {ex.Message}");
            FeedReadFailed = true;
            return false;
        }

        var feed = json["feed"] as JArray ?? new JArray();
        foreach (var item in feed)
        {
            // "reason" marks a repost — not one of our own announcements
            if (item["reason"] != null)
                continue;
            if (item["post"]?["record"]?["text"]?.Value<string>() is not string text)
                continue;

            var score = ScoreRegex.Match(text);
            var num = NumberRegex.Match(text);
            scannedPosts.Add((
                num.Success ? int.Parse(num.Groups[1].Value) : null,
                score.Success ? int.Parse(score.Groups[1].Value) : null,
                score.Success ? int.Parse(score.Groups[2].Value) : null));
        }

        feedPagesFetched++;
        feedCursor = json["cursor"]?.Value<string>();
        if (feedCursor == null || feed.Count == 0)
        {
            feedExhausted = true;
        }
        return true;
    }

    private async Task<(string? token, string? did)> CreateSessionAsync()
    {
        var body = JsonConvert.SerializeObject(new
        {
            identifier,
            password = appPassword
        }, Formatting.Indented);

        // Redact password in verbose output
        var redactedBody = body.Replace(appPassword, "***REDACTED***");
        var url = BaseUrl + "com.atproto.server.createSession";

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] POST {url}");
            Console.WriteLine($"[VERBOSE] Request body:\n{redactedBody}");
        }

        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (Config.Verbose)
        {
            Console.WriteLine($"[VERBOSE] Response status: {(int)response.StatusCode} {response.StatusCode}");
            // Truncate accessJwt in the logged response so it doesn't flood the terminal
            var truncated = TruncateJwtInJson(responseBody);
            Console.WriteLine($"[VERBOSE] Response body:\n{truncated}");
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Bluesky auth failed: {(int)response.StatusCode} {response.StatusCode}");
            if (!Config.Verbose)
            {
                Console.WriteLine($"Response body: {responseBody}");
            }
            return (null, null);
        }

        var json = JObject.Parse(responseBody);
        return (json["accessJwt"]?.Value<string>(), json["did"]?.Value<string>());
    }

    private static string PrettyJson(string raw)
    {
        try
        {
            return JToken.Parse(raw).ToString(Formatting.Indented);
        }
        catch
        {
            return raw;
        }
    }

    private static string TruncateJwtInJson(string raw)
    {
        try
        {
            var obj = JObject.Parse(raw);
            foreach (var key in new[] { "accessJwt", "refreshJwt" })
            {
                if (obj[key]?.Value<string>() is string jwt && jwt.Length > 16)
                {
                    obj[key] = jwt[..12] + $"...<truncated, len={jwt.Length}>";
                }
            }
            return obj.ToString(Formatting.Indented);
        }
        catch
        {
            return raw;
        }
    }
}
