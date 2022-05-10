prereqs:

1) node/npm (apt install node, apt install npm)
2) az function tools (maybe sudo apt-get install azure-functions-core-tools-3? maybe just through extensions in vscode?)
3) create local.settings.json, get the settings from the portal
4) for local debugging add `"DEBUGCONTAINER": "debugleaguedata"`

To run website:
npm install (once)
npm start

to invoke running function:
curl -X POST -H "Content-Type: application/json" -d '{}' http://localhost:7071/admin/functions/Scrape
