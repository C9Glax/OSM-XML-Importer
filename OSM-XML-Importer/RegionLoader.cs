using System.Globalization;
using Graph;
using Microsoft.Extensions.Logging;

namespace OSM_XML_Importer;

public class RegionLoader
{
    
    private static readonly NumberFormatInfo Ni = new()
    {
        NumberDecimalSeparator = "."
    };
    private string NodesMapFile => Path.Join(_nodesDirectory, _regionSize.ToString(Ni), "NodesMapFile");
    private string WaysMapFile => Path.Join(_waysDirectory, _regionSize.ToString(Ni), "WaysMapFile");
    private string NodesDirectory => Path.Join(_nodesDirectory, _regionSize.ToString(Ni), "nodes");
    private string WaysDirectory => Path.Join(_waysDirectory, _regionSize.ToString(Ni), "ways");
    private string _nodesDirectory, _waysDirectory;
    private ILogger? _logger;
    private float _regionSize;

    private Dictionary<ulong, string> _nodesMap;
    private Dictionary<ulong, string[]> _waysMap;
    
    public RegionLoader(float regionSize, string nodesDirectory, string waysDirectory, ILogger? logger = null)
    {
        _logger = logger;
        this._nodesDirectory = nodesDirectory;
        this._waysDirectory = waysDirectory;

        this._regionSize = regionSize;
        
        _logger?.LogInformation($"Nodes: {NodesDirectory} Ways: {WaysDirectory}");

        _nodesMap = GetNodeIdMap();
        _waysMap = GetWaysMap();
    }
    
    private Dictionary<ulong, string> GetNodeIdMap()
    {
        if (!File.Exists(NodesMapFile))
            throw new FileNotFoundException($"NodesMap not found {NodesMapFile}");
        Dictionary<ulong, string> ret = new();
        using (FileStream f = new(NodesMapFile, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader s = new(f))
            {
                while (!s.EndOfStream)
                {
                    string? line = s.ReadLine();
                    if(line is null)
                        continue;
                    string[] split = line.Split('-');
                    if(split.Length != 2)
                        continue;
                    ret.Add(ulong.Parse(split[0]), split[1]);
                }
            }
        }
        return ret;
    }
    
    private Dictionary<ulong, string[]> GetWaysMap()
    {
        if (!File.Exists(WaysMapFile))
            throw new FileNotFoundException($"WaysMap not found {WaysMapFile}");
        Dictionary<ulong, string[]> ret = new();
        using (FileStream f = new(WaysMapFile, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader s = new(f))
            {
                while (!s.EndOfStream)
                {
                    string? line = s.ReadLine();
                    if(line is null)
                        continue;
                    string[] split = line.Split('-');
                    if(split.Length != 2)
                        continue;
                    ret.Add(ulong.Parse(split[0]), split[1].Split(','));
                }
            }
        }
        return ret;
    }

    public long? GetRegionIdFromNodeId(ulong nodeId)
    {
        if (!_nodesMap.TryGetValue(nodeId, out string? value))
            return null;
        return long.Parse(value);
    }

    public Graph.Graph? GetRegionFromNodeId(ulong nodeId)
    {
        long? regionId = GetRegionIdFromNodeId(nodeId);
        if (regionId is null)
            return null;
        return GetRegion((long)regionId);
    }

    public long[]? GetRegionIdsFromWayId(ulong wayId)
    {
        if (!_waysMap.TryGetValue(wayId, out string[]? value))
            return null;
        return value.Select(long.Parse).ToArray();
    }

    public Graph.Graph? GetRegionsFromWayId(ulong wayId)
    {
        long[]? regionIds = GetRegionIdsFromWayId(wayId);
        if (regionIds is null)
            return null;

        Graph.Graph g = new ();

        foreach (long regionId in regionIds)
            g.ConcatGraph(GetRegion(regionId));

        return g;
    }

    public Graph.Graph GetRegion(long regionId)
    {
        string nodePath = Path.Join(NodesDirectory, regionId.ToString());
        string wayPath = Path.Join(WaysDirectory, regionId.ToString());
        if (!File.Exists(nodePath) || !File.Exists(wayPath))
            throw new FileNotFoundException($"Region not found {regionId}");
        
        Graph.Graph g = new();
        using (FileStream nfs = new(nodePath, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader nsr = new(nfs))
            {
                while (!nsr.EndOfStream)
                {
                    string? line = nsr.ReadLine();
                    if (line is null)
                        continue;
                    //ID-Latitude-Longitude\n
                    string[] split = line.Split('-');
                    if (split.Length != 3)
                        continue;
                    g.Nodes.Add(ulong.Parse(split[0]), new Node(float.Parse(split[1]), float.Parse(split[2])));
                }
            }
        }

        using (FileStream wfs = new(wayPath, FileMode.Open, FileAccess.Read))
        {
            using (StreamReader wsr = new(wfs))
            {
                while (!wsr.EndOfStream)
                {
                    string? line = wsr.ReadLine();
                    if (line is null)
                        continue;
                    //ID-{nodeId,}+-{tagkey@tagvalue,}+\n
                    string[] split = line.Split('-');
                    if (split.Length != 3)
                        continue;
                    ulong wayId = ulong.Parse(split[0]);
                    Way way = new (split[2].Split(',').Select(tagStr => tagStr.Split('@'))
                        .ToDictionary(x => x[0], x => x[1]));
                    g.Ways.Add(wayId, way);
                    ulong[] nodeIds = split[1].Split(',').Select(ulong.Parse).ToArray();
                    for (int i = 0; i < nodeIds.Length - 1; i++)
                    {
                        if(g.Nodes.ContainsKey(nodeIds[i]) && g.Nodes.ContainsKey(nodeIds[i + 1]))
                            g.Nodes[nodeIds[i]].Neighbors.TryAdd(nodeIds[i+1], new(wayId, way.Tags.TryGetValue("forward", out string? fwd) && bool.Parse(fwd)));
                    }
                }
            }
        }
        return g;
    }
}