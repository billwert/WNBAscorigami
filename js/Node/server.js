"use strict";

var express = require("express");
var app = express();
var path = require("path");
var request = require("request");
var sslRedirect = require('heroku-ssl-redirect');

app.use(sslRedirect())


var DATA_URL = process.env.DATA_URL;
if(!DATA_URL)
{
	DATA_URL = "http://localhost:" + (process.env.PORT || 8081) + "/datafile.json";
}

console.log(DATA_URL);

app.use(express.static(__dirname + "/../.."));

var matrix = [];
var maxpts = 0;
var maxlosepts = 0;
var maxcount = 0;
var maxcount = 0;
var lastUpdated;
var newScorigami = [];

function getData()
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

	request(DATA_URL, function(err, res, data)
	{
		if(!err)
		{
			data = JSON.parse(data);

			var newScores = [];
			var newmatrix = [];
			for(var i = 0; i < data.length; i++)
			{
				var row = data[i];
				newScores.push(row);
				if(row.pts_lose > maxlosepts)
				{
					maxlosepts = row.pts_lose;
				}
				if(row.pts_win > maxpts)
				{
					maxpts = row.pts_win;
				}
				if(row.count > maxcount)
				{
					maxcount = row.count;
				}
			}
			
			//create matrix with length and width equal to the max points, fill it with 0's
			for (var i = 0; i <= maxpts; i++)
			{
				newmatrix[i] = [];
				for(var j = 0; j <= maxpts; j++)
				{
					newmatrix[i][j] = {count: 0};
				}
			}
			//fill matrix with useful data
			for(var i = 0; i < newScores.length; i++)
			{
				newmatrix[newScores[i].pts_lose][newScores[i].pts_win] = newScores[i];
			}
			matrix = newmatrix;
			var dateOptions = { weekday: "short", year:"numeric", month:"short", day:"numeric", hour:"numeric", minute:"numeric", second:"numeric", timeZoneName:"short"};
			//lastUpdated = new Date().toUTCString();
			lastUpdated = new Date().toLocaleDateString("en-US", dateOptions);
			
			console.log("done " + lastUpdated);
		}
		else
		{
			console.log("There was an error getting data");
			throw err;
		}
		//renderPage();
	});
}

function tick()
{
	getData();
}

tick();

setInterval(tick, 1000 * 60 * 60 * 24); // daily
	
app.get("/data", function(req, res)
{
	var data = {
		matrix: matrix,
		maxpts: maxpts,
		maxlosepts: maxlosepts,
		maxcount: maxcount,
		lastUpdated: lastUpdated,
		newScorigami: newScorigami
	};
	//console.log(data);
	res.json(data);
});

app.get("/*", function(req, res)
{
	res.sendFile(path.join(__dirname+"/../../view/index.html"));
});

app.listen(process.env.PORT || 8081);
