using GeoGraph;
using Microsoft.Extensions.Logging;

namespace OSM_XML_Importer;

public class RegionLoader
{
    
    private string NodesMapFile => Path.Join(_nodesDirectory, _regionSize.ToString(), "NodesMapFile");
    private string WaysMapFile => Path.Join(_waysDirectory, _regionSize.ToString(), "WaysMapFile");
    private string NodesDirectory => Path.Join(_nodesDirectory, _regionSize.ToString(), "nodes");
    private string WaysDirectory => Path.Join(_waysDirectory, _regionSize.ToString(), "ways");
    private string _nodesDirectory, _waysDirectory;
    private ILogger? _logger;
    private float _regionSize;

    private Dictionary<ulong, string> _nodesMap;
    private Dictionary<ulong, string[]> _waysMap;
    
    public RegionLoader(float regionSize, string? nodesDirectory = null, string? waysDirectory = null,
        ILogger? logger = null)
    {
        _logger = logger;
        this._nodesDirectory = nodesDirectory ?? Environment.CurrentDirectory;
        this._waysDirectory = waysDirectory ?? Environment.CurrentDirectory;

        this._regionSize = regionSize;
        
        _logger?.LogInformation($"Nodes: {NodesDirectory} Ways: {WaysDirectory}");

        _nodesMap = GetNodeIdMap();
        _waysMap = GetWaysMap();
    }
    
    private Dictionary<ulong, string> GetNodeIdMap()
    {
        if (!File.Exists(NodesMapFile))
            throw new FileNotFoundException("NodesMap not found");
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
            throw new FileNotFoundException("WaysMap not found");
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

    public Node? GetNode(ulong nodeId)
    {
        if (!_nodesMap.ContainsKey(nodeId))
            return null;

        using FileStream fs = File.OpenRead(Path.Join(NodesDirectory, _nodesMap[nodeId]));
        using StreamReader sr = new(fs);
        while (!sr.EndOfStream)
        {
            string? line = sr.ReadLine();
            if (line is null)
                continue;
            //ID-Latitude-Longitude\n
            string[] split = line.Split('-');
            if (split.Length != 3)
                continue;
            if(ulong.Parse(split[0]) != nodeId)
                continue;
            
            return new Node(split[1], split[2]);
        }
        return null;
    }

    public Graph? GetRegionNode(ulong nodeId)
    {
        if (!_nodesMap.ContainsKey(nodeId))
            return null;

        return GetRegion(long.Parse(_nodesMap[nodeId]));
    }

    public Way? GetWay(ulong wayId)
    {
        if (!_waysMap.ContainsKey(wayId))
            return null;

        using FileStream fs = File.OpenRead(Path.Join(WaysDirectory, _waysMap[wayId][0]));
        using StreamReader sr = new(fs);
        while (!sr.EndOfStream)
        {
            string? line = sr.ReadLine();
            if (line is null)
                continue;
            //ID-{nodeId,}+-{tagkey@tagvalue,}+\n
            string[] split = line.Split('-');
            if (split.Length != 3)
                continue;
            if(ulong.Parse(split[0]) != wayId)
                continue;

            return new Way(split[1].Split(',').Select(idStr => ulong.Parse(idStr)).ToList(),
                split[2].Split(',').Select(tagStr => tagStr.Split('@')).ToDictionary(x => x[0], x => x[1]));
        }
        
        
        return null;
    }

    public Graph? GetRegionsWay(ulong wayId)
    {
        if (!_waysMap.ContainsKey(wayId))
            return null;

        Graph g = new ();

        foreach (string region in _waysMap[wayId])
            g.ConcatGraph(GetRegion(long.Parse(region)));

        return g;
    }

    public Graph GetRegion(long regionId)
    {
        string nodePath = Path.Join(NodesDirectory, regionId.ToString());
        string wayPath = Path.Join(WaysDirectory, regionId.ToString());
        if (!File.Exists(nodePath) || !File.Exists(wayPath))
            throw new FileNotFoundException($"Region not found {regionId}");

        Graph g = new();
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
                    g.AddNode(ulong.Parse(split[0]), new Node(split[1], split[2]));
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
                    g.AddWay(new Way(split[1].Split(',').Select(idStr => ulong.Parse(idStr)).ToList(),
                        split[2].Split(',').Select(tagStr => tagStr.Split('@')).ToDictionary(x => x[0], x => x[1])));
                }
            }
        }
        g.RecalculateIntersections();
        return g;
    }
}