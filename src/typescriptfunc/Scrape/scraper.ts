import { Context } from "@azure/functions";
import * as cheerio from "cheerio"
import axios from "axios";

export default class Scraper {

    private context: Context;
    /**
     *
     */
    constructor(context: Context) {
        this.context = context;
    }

    async scrape() : Promise<void> {
        await this.loadGameData();
        await this.calculateScorigami();
        await this.saveScorigamiData();
    }
    async saveScorigamiData() : Promise<void> {
        throw new Error("Method not implemented.");
    }
    async calculateScorigami() {
        throw new Error("Method not implemented.");
    }

    async loadGameData() : Promise<void> {

        let startYear = 1997;
        let date = new Date();
        for (let index = startYear; index < date.getFullYear(); index++) {
            let scheduleURLFormat = `https://www.basketball-reference.com/wnba/years/${index}_games.html`;
            // let html = await axios.get(scheduleURLFormat, {
            //     headers: {  "User-Agent": "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:x.x.x) Gecko/20041107 Firefox/x.x" }
            // });
            // let body = html.data;


            let html = await fetch(scheduleURLFormat, { 
                headers: new Headers({ "User-Agent": "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:x.x.x) Gecko/20041107 Firefox/x.x" }) 
            });
            let body = await html.text();
            const $ = cheerio.load(body);
            const table = $("title");
            console.log(table.text());
            
        }

    }
}


