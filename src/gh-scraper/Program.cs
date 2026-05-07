namespace WNBAScorigami;

class Program
{
    static void Main(string[] args)
    {
        string dataDir = "../data";
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--data-dir")
                dataDir = args[i + 1];
        }
        Scraper.Run(dataDir);
    }
}
