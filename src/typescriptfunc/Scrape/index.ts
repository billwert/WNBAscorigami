import { AzureFunction, Context } from "@azure/functions"
import { BlobServiceClient } from "@azure/storage-blob";
import { DefaultAzureCredential } from "@azure/identity"
import { blob } from "stream/consumers";
import Scraper from "./scraper";

const timerTrigger: AzureFunction = async function (context: Context, myTimer: any): Promise<void> {
    var timeStamp = new Date().toISOString();
    
    context.log("the new baby"); 
    // var blobClient = new BlobServiceClient("https://wnbascorigami.blob.core.windows.net/", new DefaultAzureCredential());
    // let containers = blobClient.listContainers();
    // for await (const container of containers) {
    //     console.log(`${container.name}`);
    // }
    const scraper = new Scraper(context);
    await scraper.scrape();
    
};

export default timerTrigger;


