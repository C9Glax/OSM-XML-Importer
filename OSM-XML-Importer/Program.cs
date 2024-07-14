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
    }
}