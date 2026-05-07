using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WNBAScorigami;

class BlueSkyPoster
{
    private static readonly HttpClient http = new();
    private const string BaseUrl = "https://bsky.social/xrpc/";

    private readonly string identifier;
    private readonly string appPassword;

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
