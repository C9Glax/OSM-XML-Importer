using Microsoft.Extensions.Logging;
using Half = SystemHalf.Half;

namespace OSM_XML_Importer;

public static class Program
{
    public static void Main(string[] args)
    {
        GlaxLogger.Logger logger = new(LogLevel.Trace, consoleOut: Console.Out);
        OSMFileSplitter o = new OSMFileSplitter();
        o.SplitFileIntoRegions(new Half(0.001), filterHighways: true, logger: logger);
        o.CleanBakFiles();
    }
}