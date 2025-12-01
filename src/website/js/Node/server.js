"use strict";

var express = require("express");
var app = express();
var path = require("path");

app.use((req, res, next) => {
  if (process.env.NODE_ENV === 'production' && req.header('x-forwarded-proto') !== 'https') {
    res.redirect(`https://${req.header('host')}${req.url}`);
  } else {
    next();
  }
});


var DATA_URL = process.env.DATA_URL;
if(!DATA_URL)
{
	DATA_URL = "http://localhost:" + (process.env.PORT || 8081) + "/datafile.json";
}

console.log(DATA_URL);

app.use(express.static(__dirname + "/../.."));

var retdata; // this is the cache from the hourly update.

async function getData()
{
	/*
	pts_win
	pts_lose
	count
	first_date
	first_team_win
	first_team_lose
	first_team_home
	first_team_away
	first_link
	last_date
	last_team_win
	last_team_lose
	last_team_home
	last_team_away
	last_link
	*/

	try {
		const response = await fetch(DATA_URL);
		if (!response.ok) {
			throw new Error(`HTTP error! status: ${response.status}`);
		}
		retdata = await response.json();
	}
	catch (err) {
		console.log("There was an error getting data");
		console.error(err);
		throw err;
	}
}

async function tick()
{
	await getData();
}

tick().catch(err => console.error("Failed to fetch initial data:", err));

setInterval(tick, 1000 * 60 * 60); // hourly
	
app.get("/data", function(req, res)
{
	//console.log(data);
	res.json(retdata);
});

app.get("/*", function(req, res)
{
	res.sendFile(path.join(__dirname+"/../../view/index.html"));
});

app.listen(process.env.PORT || 8081);
