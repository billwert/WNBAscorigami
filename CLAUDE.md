# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

WNBA Scorigami tracker — a D3.js heatmap showing every unique final score combination in WNBA history. Built on the "Scorigami" concept by Jon Bois. The site is a Node/Express app fed by an Azure Function that scrapes Basketball Reference nightly.

## Commands

### Website (frontend)
```bash
cd src/website
npm install
npm start        # Runs Express server on $PORT (default 8081)
```

### TypeScript Azure Function (in-progress)
```bash
cd src/typescriptfunc
npm install
npm run build    # tsc
npm run watch    # tsc -w
npm start        # runs prestart (build) then func start
npm test         # Jest + ts-jest (minimal coverage currently)
```

### C# Azure Function (production)
```bash
cd src/func
func start       # Azure Functions Core Tools required
# Trigger locally:
curl -X POST -H "Content-Type: application/json" -d '{}' \
  http://localhost:7071/admin/functions/Scrape
```

## Architecture

### Data pipeline
1. `src/func/Scrape.cs` — timer trigger (daily 10:00 AM UTC) starts the run
2. `src/func/Scraper.cs` — fetches WNBA game logs from basketball-reference.com (1997–present), parses HTML with HtmlAgilityPack, builds a 150×150 score-combination array
3. `src/func/Storage.cs` — uploads `scorigamidata.json` to Azure Blob Storage

The TypeScript function in `src/typescriptfunc/` is an incomplete rewrite of the C# scraper using axios + cheerio. It has stub implementations and is not yet production-ready.

### Frontend
- `src/website/js/Node/server.js` — Express server; exposes `/data` endpoint that proxies `DATA_URL`, cached hourly
- `src/website/js/Client/viz.js` — D3 heatmap; axes are winning score (Y) vs. losing score (X); cells colored by first-occurrence year or by count, switchable via UI toggle
- `src/website/datafile.json` — local static fallback data

### Data shape
Each cell in the JSON array:
```json
{ "pts_win": 85, "pts_lose": 72, "count": 3,
  "first_date": "2001-06-12", "last_date": "2022-07-04",
  "first_team_win": "LAL", "first_team_lose": "NYL",
  "last_team_win": "SEA", "last_team_lose": "CHI",
  "first_link": null, "last_link": null }
```

## Environment Variables

| Variable | Component | Purpose |
|---|---|---|
| `PORT` | website | Server port (default 8081) |
| `DATA_URL` | website | Where to fetch scorigami JSON (default: local datafile) |
| `NODE_ENV` | website | `production` enables HTTPS redirect |
| `DEBUGCONTAINER` | C# func | Azure Storage container override for local testing |

For the C# function, put Azure Storage credentials in `src/func/local.settings.json` (not committed). Add `"DEBUGCONTAINER": "debugleaguedata"` for local test runs against a debug container.
