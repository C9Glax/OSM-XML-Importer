using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace OSM_XML_Importer;

public class OSMFileSplitter(float regionSize, string? nodesDirectory = null, string? waysDirectory = null, ILogger? logger = null)
{
    
    /*
     * Region naming format:
     * Latitude/Longitude.dat
     * Latitude/Longitude are modulo regionSize, e.g. regionSize = 0.01 Lat = 34.345 => LatMod = 34.34
     */
    
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        IgnoreWhitespace = true,
        IgnoreComments = true
    };

    private static readonly NumberFormatInfo Ni = new()
    {
        NumberDecimalSeparator = "."
    };
    
    private string NodesMapFile => Path.Join(_nodesDirectory, _regionSize.ToString(Ni), "NodesMapFile");
    private string WaysMapFile => Path.Join(_waysDirectory, _regionSize.ToString(Ni), "WaysMapFile");
    private string NodesDirectory => Path.Join(_nodesDirectory, _regionSize.ToString(Ni), "nodes");
    private string WaysDirectory => Path.Join(_waysDirectory, _regionSize.ToString(Ni), "ways");
    private readonly string _nodesDirectory = nodesDirectory ?? Environment.CurrentDirectory;
    private readonly string _waysDirectory = waysDirectory ?? Environment.CurrentDirectory;
    private readonly ILogger? _logger = logger;
    private readonly float _regionSize = regionSize;
    private readonly TimeSpan _logInterval = TimeSpan.FromSeconds(3);

    public void SplitFileIntoRegions(string filePath, bool filterHighways = false, ILogger? logger = null)
    {
        _logger?.LogInformation($"Input: {filePath} Output-Nodes: {NodesDirectory} Output-Ways: {WaysDirectory}");
        _logger?.LogDebug("Opening File...");
        Stream mapData = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        ValueTuple<int, ulong> numRegionsAndNodes = Nodes(mapData);
        Ways(mapData, numRegionsAndNodes, filterHighways);
        CleanUnusedNodes(numRegionsAndNodes.Item2);
    }
    
    /*
     * Node-File format
     * ID-Latitude-Longitude\n
     */
    private ValueTuple<int, ulong> Nodes(Stream mapData)
    {
        _logger?.LogDebug("Splitting Nodes...");
        Dictionary<long, FileStream> nodesRegionFileStreams = new();
        ulong numNodes = 0;

        if (!Directory.Exists(NodesDirectory))
            Directory.CreateDirectory(NodesDirectory);
        
        FileStream nodesMapFileStream = new(NodesMapFile, FileMode.Create, FileAccess.Write);
        mapData.Position = 0;
        XmlReader reader = XmlReader.Create(mapData, ReaderSettings);
        reader.MoveToContent();
        
        DateTime log = DateTime.Now;
        DateTime start = DateTime.Now;
        long? startPos = null;
        while (reader.ReadToFollowing("node"))
        {
            startPos ??= mapData.Position;
            string? id = reader.GetAttribute("id");
            string? lat = reader.GetAttribute("lat");
            string? lon = reader.GetAttribute("lon");
            if(id is null || lat is null || lon is null)
                continue;
            long regionFileStream = GetRegionFileStream(lat, lon, ref nodesRegionFileStreams);
            FileStream f = nodesRegionFileStreams[regionFileStream];
            //ID-Latitude-Longitude\n
            string line = $"{id}-{lat}-{lon}\n";
            f.Write(Encoding.ASCII.GetBytes(line));

            //nodeId-{regionId}\n
            string map = $"{id}-{regionFileStream}\n";
            nodesMapFileStream.Write(Encoding.ASCII.GetBytes(map));
            if(DateTime.Now.Subtract(log) > _logInterval){
                float finished = (float)(mapData.Position  - startPos.Value) / (mapData.Length - startPos.Value);
                TimeSpan elapsed = DateTime.Now.Subtract(start);
                TimeSpan remaining = elapsed / finished * (1 - finished);
                _logger?.LogDebug($"{finished:P} {elapsed:hh\\:mm\\:ss} elapsed {remaining:hh\\:mm\\:ss} remaining ({mapData.Position:N0}/{mapData.Length:N0} bytes)");
                log = DateTime.Now;
            }
            _logger?.LogTrace($"{line} -> {regionFileStream} = {Path.Join(NodesDirectory, regionFileStream.ToString())}");
        }
        foreach(FileStream fs in nodesRegionFileStreams.Values)
            fs.Close();
        nodesMapFileStream.Close();

        return new(nodesRegionFileStreams.Count, numNodes);
    }

    private long GetRegionFileStream(string lat, string lon, ref Dictionary<long, FileStream> nodesRegionFileStreams)
    {
        long ret = RegionUtils.GetRegionId(lat, lon, _regionSize);
        if(!nodesRegionFileStreams.ContainsKey(ret))
            nodesRegionFileStreams.Add(ret, new FileStream(Path.Join(NodesDirectory, ret.ToString()), FileMode.Create, FileAccess.Write));

        return ret;
    }
    
    /*
     * Way-File format
     * ID-{nodeId,}+-{tagkey@tagvalue,}+\n
     */
    private void Ways(Stream mapData, ValueTuple<int, ulong> numRegionsAndNodes, bool filterToHighways = false)
    {
        _logger?.LogDebug("Splitting Ways...");
        Dictionary<long, FileStream> waysRegionFileStreams = new((int)(numRegionsAndNodes.Item1 * 1.2));
        
        if (!Directory.Exists(WaysDirectory))
            Directory.CreateDirectory(WaysDirectory);
        
        FileStream waysMapFileStream = new(WaysMapFile, FileMode.Create, FileAccess.Write);
        Dictionary<ulong, long> nodeIdMap = GetNodeIdMapRegionId(numRegionsAndNodes.Item2);
        mapData.Position = 0;
        XmlReader reader = XmlReader.Create(mapData, ReaderSettings);
        reader.MoveToContent();
        
        DateTime log = DateTime.Now;
        DateTime start = DateTime.Now;
        long? startPos = null;
        while (reader.ReadToFollowing("way"))
        {
            startPos ??= mapData.Position;
            string? id = reader.GetAttribute("id");
            if(id is null)
                continue;
            List<ulong> nodeIds = new();
            Dictionary<string, string> tags = new();
            tags.Add("id", id);
            using (XmlReader wayReader = reader.ReadSubtree())
            {
                while (wayReader.Read())
                {
                    if (reader.Name == "tag")
                    {
                        string? key = reader.GetAttribute("k");
                        string? value = reader.GetAttribute("v");
                        if(value is null || key is null)
                            continue;
                        tags.Add(key, value);
                    }
                    else if (reader.Name == "nd")
                    {
                        string? nodeId = reader.GetAttribute("ref");
                        if(nodeId is null)
                            continue;
                        nodeIds.Add(ulong.Parse(nodeId));
                    }
                }
            }
            if(id is null)
                continue;
            if(filterToHighways && !tags.ContainsKey("highway"))
                continue;//We are filtering all ways that aren't highways
            
            //ID-{nodeId,}+-{tagkey@tagvalue,}+\n
            string line = $"{id}-{string.Join(',',nodeIds)}-{string.Join(',', tags.Select(t => $"{t.Key}@{t.Value}".Replace(",", ";").Replace("-","=")))}\n";
            List<long> regionIds = nodeIds.Select(nId => nodeIdMap[nId]).Distinct().ToList();
            foreach (long regionId in regionIds)
            {
                if(!waysRegionFileStreams.ContainsKey(regionId))
                    waysRegionFileStreams.Add(regionId, new FileStream(Path.Join(WaysDirectory, regionId.ToString()), FileMode.Create, FileAccess.Write));
                FileStream f = waysRegionFileStreams[regionId];
                f.Write(Encoding.UTF8.GetBytes(line));
                _logger?.LogTrace($"{line} -> {regionId} = {Path.Join(WaysDirectory, regionId.ToString())}");
            }
            
            //wayId-{regionId,}+\n
            string map = $"{id}-{string.Join(',', regionIds)}\n";
            waysMapFileStream.Write(Encoding.ASCII.GetBytes(map));
            
            if(DateTime.Now.Subtract(log) > _logInterval){
                float finished = (float)(mapData.Position  - startPos.Value) / (mapData.Length - startPos.Value);
                TimeSpan elapsed = DateTime.Now.Subtract(start);
                TimeSpan remaining = elapsed / finished * (1 - finished);
                _logger?.LogDebug($"{finished:P} {elapsed:hh\\:mm\\:ss} elapsed {remaining:hh\\:mm\\:ss} remaining ({mapData.Position:N0}/{mapData.Length:N0} bytes)");
                log = DateTime.Now;
            }
        }
        foreach(FileStream fs in waysRegionFileStreams.Values)
            fs.Close();
        waysMapFileStream.Close();
    }

    private Dictionary<ulong, long> GetNodeIdMapRegionId(ulong numNodes = 1024)
    {
        Dictionary<ulong, long> ret = new((int)(numNodes * 1.2));
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
                    ret.Add(ulong.Parse(split[0]), long.Parse(split[1]));
                }
            }
        }
        return ret;
    }

    /*
     * If we filtered to highways, not all nodes will be used
     * Also 
     */
    private void CleanUnusedNodes(ulong numNodes = 1024)
    {
        _logger?.LogInformation("Removing unnecessary nodes from regions...");
        string[] regionFiles = Directory.GetFiles(WaysDirectory);
        
        File.Copy(NodesMapFile, $"{NodesMapFile}.bak", true);
        Dictionary<ulong, long> nodesMap = GetNodeIdMapRegionId();
        string newNodesMapFile = $"{NodesMapFile}.new";
        FileStream nodesMapFileStream = new(newNodesMapFile, FileMode.Create, FileAccess.Write);
        
        foreach (string region in regionFiles)
        {
            FileInfo fi = new (region);
            string regionId = fi.Name;
            string nodeRegionFile = Path.Join(NodesDirectory, regionId);
            
            if(!File.Exists(nodeRegionFile))
                continue;
            
            HashSet<ulong> nodeIds = new((int)(numNodes * 1.2)); //All the nodeIds in the region of the way
            using (FileStream fs = new (region, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader sr = new(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        string? line = sr.ReadLine();
                        if(line is null)
                            continue;
                        //ID-{nodeId,}+-{tagkey@tagvalue,}+\n
                        string[] split = line.Split('-');
                        if(split.Length != 3)
                            continue;
                        string[] ids = split[1].Split(',');
                        foreach (string id in ids)
                            nodeIds.Add(ulong.Parse(id));
                    }
                }
            }
            _logger?.LogTrace($"Region {regionId}\n" +
                              $"\tIds:\t{string.Join("\n\t\t", nodeIds)}");
            
            File.Copy(nodeRegionFile, $"{nodeRegionFile}.bak", true);
            string newNodeFile = $"{nodeRegionFile}.new";
            using (FileStream fs = new(nodeRegionFile, FileMode.Open, FileAccess.Read))
            {
                using (FileStream fsn = new(newNodeFile, FileMode.Create, FileAccess.Write))
                {
                    using (StreamReader sr = new(fs))
                    {
                        while (!sr.EndOfStream)
                        {
                            string? line = sr.ReadLine();
                            if(line is null)
                                continue;
                            //ID-Latitude-Longitude\n
                            string[] split = line.Split('-');
                            if(split.Length != 3)
                                continue;
                            string id = split[0];
                            if(nodeIds.Contains(ulong.Parse(id)))
                                fsn.Write(Encoding.ASCII.GetBytes($"{line}\n"));
                            else
                                _logger?.LogTrace($"Region {regionId} removed Node {id}");
                        }
                    }
                }
            }
            if(new FileInfo(newNodeFile).Length > 0)
                File.Move(newNodeFile, nodeRegionFile, true);
            else
                File.Delete(newNodeFile);

            foreach (ulong nodeId in nodeIds)
            {
                if (nodesMap.ContainsKey(nodeId))
                {
                    //nodeId-{regionId}\n
                    string line = $"{nodeId}-{regionId}\n";
                    nodesMapFileStream.Write(Encoding.ASCII.GetBytes(line));
                    nodesMap.Remove(nodeId);
                }
            }
        }
        nodesMapFileStream.Close();
        File.Move(newNodesMapFile, NodesMapFile, true);
    }

    public void CleanBakFiles()
    {
        _logger?.LogInformation("Deleting Backup files...");
        string[] nodeFiles = Directory.GetFiles(NodesDirectory, "*.bak");
        string[] wayFiles = Directory.GetFiles(WaysDirectory, "*.bak");
        foreach (string file in nodeFiles.Union(wayFiles))
        {
            _logger?.LogDebug($"Deleting {file}");
            File.Delete(file);
        }
    }
}