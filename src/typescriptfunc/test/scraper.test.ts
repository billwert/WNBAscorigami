import Scraper from "../Scrape/scraper"
import {describe, expect, test} from '@jest/globals';
import * as assert from "assert";

test("it scrapes the web", () => {
    let scrape = new Scraper(null);
    scrape.loadGameData();
    assert.ok("it's fine");
});