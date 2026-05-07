# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

WNBA Scorigami tracker — a D3.js heatmap showing every unique final score combination in WNBA history. Built on the "Scorigami" concept by Jon Bois. The site is a static GitHub Pages site; data is scraped hourly by a GitHub Actions workflow.

## Commands

### Scraper (.NET 8 console app)
```bash
cd src/gh-scraper
dotnet build
dotnet run                                      # uses ../data as output dir
dotnet run -- --data-dir ../../src/data         # explicit path
dotnet run -- --what-if                         # detect new scorigamis but don't post
dotnet run -- --verbose                         # log all HTTP calls, env loading, diffs
dotnet run -- --what-if --verbose               # safest way to test locally
```

Always run from `src/gh-scraper/` with plain `dotnet run`, not from the repo root with `--project`.

### Frontend (static site)
```bash
cd src/gh-pages
npm start    # copies scorigamidata.json from ../data and serves on http://localhost:8082
```

## Architecture

### Data pipeline
1. `src/gh-scraper/Scraper.cs` — scrapes WNBA game logs from basketball-reference.com (1997–present), parses HTML with HtmlAgilityPack, builds a 150×150 score-combination array, writes `src/data/scorigamidata.json` and per-year game caches in `src/data/games/`
2. `src/gh-scraper/ScorigamiNotifier.cs` — diffs new scorigamidata.json against the previously committed version; any score pair that appears for the first time triggers a Bluesky post
3. `src/gh-scraper/BlueSkyPoster.cs` — authenticates with the Bluesky XRPC API and creates a post (`app.bsky.feed.post`)

### Frontend
- `src/gh-pages/js/viz.js` — D3.js v7 heatmap; Y axis = winning score, X axis = losing score; cells colored by first-occurrence year or by count, switchable via toggle; interactive tooltips show first and latest game
- `src/gh-pages/index.html` — markup, stats box, FAQ, Bluesky footer link
- `src/gh-pages/styles/main.css` — layout and styling

### GitHub Actions workflow (`.github/workflows/scrape.yml`)
Triggers: hourly cron (`0 * * * *`), push to `main`, manual dispatch.
- **scrape job**: checks out, builds and runs the scraper, commits any changed data files back to the branch
- **deploy job**: copies `src/gh-pages/` + `src/data/scorigamidata.json` into `_site/` and deploys to GitHub Pages

### Data files (`src/data/`)
- `scorigamidata.json` — main output, committed to the repo and served as a static file
- `scorigami-by-team.json` — scorigami count per team
- `games/{year}_games.json` — per-year game log cache; only the current year is re-scraped on each run

### Data shape
Root: `{ "games": [...], "lastUpdated": "<ISO-8601>" }`

Each entry in `games`:
```json
{
  "pts_win": 85, "pts_lose": 72, "count": 3,
  "first_date": "2001-06-12T00:00:00",
  "first_team_win": "Los Angeles Sparks", "first_team_lose": "New York Liberty",
  "first_team_home": "Los Angeles Sparks", "first_team_away": "New York Liberty",
  "first_link": "https://www.basketball-reference.com/wnba/boxscores/...",
  "last_date": "2022-07-04T00:00:00",
  "last_team_win": "Seattle Storm", "last_team_lose": "Chicago Sky",
  "last_team_home": "Seattle Storm", "last_team_away": "Chicago Sky",
  "last_link": "https://www.basketball-reference.com/wnba/boxscores/..."
}
```

## Bluesky Integration

New scorigamis are announced on [@wnbascorigami.com](https://bsky.app/profile/wnbascorigami.com). Post format:

```
SCORIGAMI!!!

With a score of 100 - 52 the New York Liberty and Connecticut Sun have completed the 1384th scorigami in league history.

#WNBA
```

Detection: the scraper loads the old `scorigamidata.json` before overwriting it, then finds any score pair present in the new data but not the old. Only genuinely new combinations are posted.

### Credential setup
**Local dev:** copy `.env.example` to `.env` at the repo root and fill in values. The scraper walks up the directory tree to find this file, so it works when running from `src/gh-scraper/`.

**CI:** add `BLUESKY_IDENTIFIER` and `BLUESKY_APP_PASSWORD` as GitHub Actions secrets (repo Settings → Secrets and variables → Actions). Create the app password at bsky.app → Settings → App Passwords.

| Variable | Purpose |
|---|---|
| `BLUESKY_IDENTIFIER` | Bluesky handle (`wnbascorigami.com`) |
| `BLUESKY_APP_PASSWORD` | App password (not the main account password) |

If either variable is missing the scraper runs normally and skips posting silently.
