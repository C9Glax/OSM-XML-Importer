using Microsoft.Extensions.Logging;
using Node = Graph.Node;

namespace OSM_XML_Importer;

public static class Program
{
    public static void Main(string[] args)
    {
        float regionSize = 0.01f;
        GlaxLogger.Logger logger = new(LogLevel.Trace, consoleOut: Console.Out);
        bool filterHighways;

        if (args.Length != 2)
        {
            logger.LogError("Invalid number of arguments.");
            PrintUsage(Console.Out);
            return;
        }else if (File.Exists(args[0]) == false)
        {
            logger.LogError("File does not exist.");
            return;
        }else if(bool.TryParse(args[1], out filterHighways) == false)
        {
            logger.LogError($"Could not parse {args[1]} to boolean.");
            return;
        }
        
        OSMFileSplitter o = new (regionSize, logger: logger);
        o.SplitFileIntoRegions(args[0], filterHighways: filterHighways, logger: logger);
        o.CleanBakFiles();
        
        
        RegionLoader r = new (regionSize, logger: logger);

        float lat = 48.793347f;
        float lon = 9.832301f;
        long regionId = RegionUtils.GetRegionId(lat, lon, regionSize);
        Graph.Graph g = r.GetRegion(regionId);
        KeyValuePair<ulong, Node> node = g.ClosestNodeToCoordinates(lat, lon);
        logger.LogInformation($"{lat} {lon} -> Region {regionId} Closest Node: {node}");
    }

    private static void PrintUsage(TextWriter textWriter)
    {
        
    }
}