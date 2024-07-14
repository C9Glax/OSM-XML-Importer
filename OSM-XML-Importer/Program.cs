using GeoGraph;
using Microsoft.Extensions.Logging;

namespace OSM_XML_Importer;

public static class Program
{
    public static void Main(string[] args)
    {
        float regionSize = 0.01f;
        GlaxLogger.Logger logger = new(LogLevel.Trace, consoleOut: Console.Out);
        
        
        OSMFileSplitter o = new (regionSize, logger: logger);
        o.SplitFileIntoRegions(filterHighways: true, logger: logger);
        o.CleanBakFiles();
        
        
        RegionLoader r = new (regionSize, logger: logger);

        float lat = 48.793347f;
        float lon = 9.832301f;
        long regionId = Util.GetRegionId(lat, lon, regionSize);
        Graph g = r.GetRegion(regionId);
        ulong? node = g.ClosestNodeIdToCoordinates(lat, lon);
        Node? n = g.GetNode((ulong)node!);
        logger.LogInformation($"{lat} {lon} -> Region {regionId} Closest Node: {node} {n}");
    }
}